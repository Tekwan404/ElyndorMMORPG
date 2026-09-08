using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Quests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Progression;

public sealed record CombatRewardApplicationResult(
    bool Granted,
    int XpEarned,
    int GoldEarned,
    CharacterProgressionResult? Progression,
    IReadOnlyList<CombatRewardItemResult> Items,
    IReadOnlyList<string>? CompletedContractIds = null,
    IReadOnlyList<CombatLootRollResult>? LootRolls = null);

public sealed record CombatLootRollResult(
    Guid LootRollId,
    string ItemId,
    string Name,
    ItemRarity Rarity,
    int Quantity,
    DateTimeOffset EndsAtUtc,
    IReadOnlyList<Guid> EligibleCharacterIds,
    bool CanNeed);

public sealed record CombatRewardItemResult(
    string ItemId,
    string Name,
    ItemType Type,
    ItemRarity Rarity,
    int Quantity);

public sealed class CombatRewardService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService,
    IGameRandomFactory randomFactory,
    TimeProvider timeProvider)
{
    public CombatRewardService(
        GameDbContext dbContext,
        GameContentPackage content,
        CharacterDerivedStateService derivedStateService,
        IGameRandomFactory randomFactory,
        TimeProvider timeProvider)
        : this(
            dbContext,
            new StaticContentSnapshotProvider(content),
            derivedStateService,
            randomFactory,
            timeProvider)
    {
    }

    public Task<CombatRewardApplicationResult> ApplyVictoryAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken) =>
        ApplyVictoryAsync(
            characterId,
            snapshot,
            contentProvider.GetCurrent(),
            cancellationToken);

    public async Task<CombatRewardApplicationResult> ApplyVictoryAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentSnapshot);
        if (snapshot.Status != CombatSessionStatus.Victory)
            return new CombatRewardApplicationResult(false, 0, 0, null, []);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            () => ApplyVictoryCoreAsync(
                characterId,
                snapshot,
                contentSnapshot,
                cancellationToken));
    }

    private async Task<CombatRewardApplicationResult> ApplyVictoryCoreAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize permanent rewards per character before checking the session grant.
        // This turns concurrent finalization into a normal replay instead of allowing both
        // transactions to race until the unique CombatSessionId constraint rejects one.
        Character character = await dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"Id\" = {characterId} FOR UPDATE")
            .SingleAsync(cancellationToken);

        CombatRewardGrant? existingGrant = await dbContext.CombatRewardGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                grant => grant.CombatSessionId == snapshot.SessionId
                    && grant.CharacterId == characterId,
                cancellationToken);
        if (existingGrant is not null)
        {
            CombatLootRollResult[] pendingRolls = await LoadPendingLootRollsAsync(
                snapshot.SessionId,
                character,
                contentSnapshot,
                indexes: contentSnapshot.Indexes,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CombatRewardApplicationResult(
                false,
                existingGrant.XpEarned,
                existingGrant.GoldEarned,
                null,
                [],
                LootRolls: pendingRolls);
        }

        GameContentPackage content = contentSnapshot.Package;
        GameContentIndexes indexes = contentSnapshot.Indexes;
        ResolvedRewardSource[] rewardSources = ResolveRewardSources(snapshot, indexes);
        LevelProgressionDefinition progression = content.LevelProgression
            ?? throw new InvalidOperationException("Level progression content is required for combat rewards.");

        List<CombatRewardSourceAudit> sourceAudits = [];
        List<LootRoll> rolledPersonalLoot = [];
        int xpEarned = 0;
        int goldEarned = 0;
        foreach (ResolvedRewardSource source in rewardSources)
        {
            int sourceXp = source.Monster.XpReward;
            int sourceGold = RollGold(source.Monster);
            IReadOnlyList<LootRoll> sourceLoot = RollLoot(source.Monster, indexes)
                .Where(roll => !IsValuableLoot(roll, indexes))
                .ToArray();

            xpEarned = checked(xpEarned + sourceXp);
            goldEarned = checked(goldEarned + sourceGold);
            rolledPersonalLoot.AddRange(sourceLoot);
            sourceAudits.Add(new CombatRewardSourceAudit(
                source.Enemy.ActorId,
                source.Monster.Id,
                sourceXp,
                sourceGold,
                source.EncounterOrder,
                sourceLoot.Select(roll =>
                    new CombatRewardSourceItemAudit(
                        roll.ItemId,
                        roll.Quantity)).ToArray()));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<LootRoll> sharedValuableLoot =
            await GetOrCreateSharedValuableLootAsync(
                snapshot,
                rewardSources,
                indexes,
                now,
                cancellationToken);

        QuestProgressUpdateResult questProgress =
            await QuestProgression.ApplyKillsAsync(
                dbContext,
                character.Id,
                snapshot.SessionId,
                rewardSources.Select(source => source.Monster.Id).ToArray(),
                content,
                now,
                cancellationToken);

        CharacterProgressionResult progressionResult = CharacterProgression.GrantExperience(
            character,
            xpEarned,
            progression);
        await dbContext.Characters
            .Where(candidate => candidate.Id == characterId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.Gold,
                    candidate => candidate.Gold + goldEarned),
                cancellationToken);

        LootRoll[] loot = AggregateLoot(rolledPersonalLoot);
        List<LootRoll> personalLoot = [];
        List<CombatLootRollResult> pendingLootRolls = [];
        CharacterDerivedState? derivedForLoot = null;
        foreach (LootRoll roll in loot)
        {
            if (!indexes.ItemsById.TryGetValue(roll.ItemId, out ItemDefinition? item))
                throw new InvalidOperationException($"Item '{roll.ItemId}' is missing from game content.");

            await AddItemAsync(characterId, snapshot.SessionId, roll, now, contentSnapshot, cancellationToken);
            personalLoot.Add(roll);
        }

        foreach (LootRoll roll in sharedValuableLoot)
        {
            if (!indexes.ItemsById.TryGetValue(roll.ItemId, out ItemDefinition? item))
                throw new InvalidOperationException($"Item '{roll.ItemId}' is missing from game content.");

            derivedForLoot ??= await derivedStateService.ResolveAsync(
                character.Id,
                character.ClassId,
                character.Level,
                contentSnapshot,
                cancellationToken);
            CombatLootRollResult groupRoll = await CreateOrLoadLootRollAsync(
                snapshot,
                item,
                roll.Quantity,
                now,
                CanNeed(item, character, derivedForLoot),
                cancellationToken);
            pendingLootRolls.Add(groupRoll);
        }

        if (progressionResult.LeveledUp)
        {
            CharacterVitals vitals = await dbContext.CharacterVitals.SingleAsync(
                candidate => candidate.CharacterId == characterId,
                cancellationToken);
            CharacterDerivedState derived = await derivedStateService.ResolveAsync(
                character.Id,
                character.ClassId,
                character.Level,
                contentSnapshot,
                cancellationToken);
            vitals.Checkpoint(
                derived.Stats.MaxHp,
                Math.Min(vitals.CurrentResource, derived.EffectiveResourceProfile.MaxValue),
                now);
        }

        dbContext.CombatRewardGrants.Add(new CombatRewardGrant(
            snapshot.SessionId,
            characterId,
            rewardSources[0].Monster.Id,
            xpEarned,
            goldEarned,
            now,
            JsonSerializer.Serialize(sourceAudits)));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CombatRewardApplicationResult(
            true,
            xpEarned,
            goldEarned,
            progressionResult,
            personalLoot.Select(roll => ToRewardItem(roll, indexes)).ToArray(),
            questProgress.CompletedLegacyContractIds,
            pendingLootRolls);
    }

    private async Task<CombatLootRollResult> CreateOrLoadLootRollAsync(
        CombatSessionSnapshot snapshot,
        ItemDefinition item,
        int quantity,
        DateTimeOffset now,
        bool canNeed,
        CancellationToken cancellationToken)
    {
        await AcquireLootRollLockAsync(
            snapshot.SessionId,
            item.Id,
            cancellationToken);
        CombatLootRoll? existing = await dbContext.CombatLootRolls
            .SingleOrDefaultAsync(
                roll => roll.CombatSessionId == snapshot.SessionId
                    && roll.ItemDefinitionId == item.Id,
                cancellationToken);
        if (existing is not null)
            return ToLootRollResult(existing, item, canNeed);

        Guid[] eligibleCharacterIds = ResolveEligibleCharacterIds(snapshot);
        Guid dungeonRunId = await dbContext.DungeonEncounters
            .Where(encounter => encounter.CombatSessionId == snapshot.SessionId)
            .Select(encounter => (Guid?)encounter.RunId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? snapshot.SessionId;
        CombatLootRoll created = new(
            Guid.NewGuid(),
            dungeonRunId,
            snapshot.SessionId,
            item.Id,
            item.Rarity,
            quantity,
            Guid.NewGuid(),
            now.AddSeconds(25),
            JsonSerializer.Serialize(eligibleCharacterIds));
        dbContext.CombatLootRolls.Add(created);
        return ToLootRollResult(created, item, canNeed);
    }

    private async Task<IReadOnlyList<LootRoll>> GetOrCreateSharedValuableLootAsync(
        CombatSessionSnapshot snapshot,
        IReadOnlyList<ResolvedRewardSource> rewardSources,
        GameContentIndexes indexes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await AcquireSharedLootLockAsync(snapshot.SessionId, cancellationToken);
        CombatSharedLootResolution? existing = await dbContext.CombatSharedLootResolutions
            .SingleOrDefaultAsync(
                resolution => resolution.CombatSessionId == snapshot.SessionId,
                cancellationToken);
        if (existing is not null)
        {
            return JsonSerializer.Deserialize<LootRoll[]>(existing.GroupLootJson) ?? [];
        }

        List<LootRoll> rolled = [];
        foreach (ResolvedRewardSource source in rewardSources)
        {
            rolled.AddRange(
                RollLoot(source.Monster, indexes)
                    .Where(roll => IsValuableLoot(roll, indexes)));
        }

        LootRoll[] sharedLoot = AggregateLoot(rolled);
        dbContext.CombatSharedLootResolutions.Add(
            new CombatSharedLootResolution(
                snapshot.SessionId,
                JsonSerializer.Serialize(sharedLoot),
                now));
        return sharedLoot;
    }

    private async Task AcquireSharedLootLockAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"shared:{sessionId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private async Task AcquireLootRollLockAsync(
        Guid sessionId,
        string itemDefinitionId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"{sessionId:N}:{itemDefinitionId}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private static bool IsValuableLoot(LootRoll roll, GameContentIndexes indexes) =>
        indexes.ItemsById.TryGetValue(roll.ItemId, out ItemDefinition? item)
        && LootRollRules.IsValuableWearable(item);

    private static Guid[] ResolveEligibleCharacterIds(CombatSessionSnapshot snapshot)
    {
        Guid[] eligibleCharacterIds = snapshot.ParticipantContributions is { Count: > 0 }
            ? snapshot.ParticipantContributions
                .Where(participant => participant.IsEligible)
                .Select(participant => participant.Snapshot.CharacterId)
                .Distinct()
                .OrderBy(characterId => characterId)
                .ToArray()
            : [snapshot.PlayerContribution?.CharacterId ?? snapshot.Player.ActorId];

        return eligibleCharacterIds;
    }

    private async Task<CombatLootRollResult[]> LoadPendingLootRollsAsync(
        Guid sessionId,
        Character character,
        GameContentSnapshot contentSnapshot,
        GameContentIndexes indexes,
        CancellationToken cancellationToken)
    {
        CombatLootRoll[] rolls = await dbContext.CombatLootRolls
            .AsNoTracking()
            .Where(roll => roll.CombatSessionId == sessionId
                && roll.State == CombatLootRollState.Open)
            .ToArrayAsync(cancellationToken);
        CombatLootRoll[] validRolls = rolls
            .Where(roll => indexes.ItemsById.ContainsKey(roll.ItemDefinitionId))
            .ToArray();
        if (validRolls.Length == 0)
            return [];

        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        return validRolls
            .Select(roll => ToLootRollResult(
                roll,
                indexes.ItemsById[roll.ItemDefinitionId],
                CanNeed(indexes.ItemsById[roll.ItemDefinitionId], character, derived)))
            .ToArray();
    }

    private static CombatLootRollResult ToLootRollResult(
        CombatLootRoll roll,
        ItemDefinition item,
        bool canNeed)
    {
        Guid[] eligible = JsonSerializer.Deserialize<Guid[]>(roll.EligibleCharacterIdsJson) ?? [];
        return new CombatLootRollResult(
            roll.LootRollId,
            item.Id,
            item.Name,
            item.Rarity,
            roll.Quantity,
            roll.EndsAtUtc,
            eligible,
            canNeed);
    }

    private static bool CanNeed(
        ItemDefinition item,
        Character character,
        CharacterDerivedState derived)
    {
        bool hasDualWieldPermission = derived.TalentTree is not null
            && TalentEquipmentPermissionResolver.HasPermission(
                derived.TalentTree,
                derived.ActiveTalentRanks,
                EquipmentPermissionIds.DualWieldOneHandWeapon);
        return LootRollRules.CanNeed(
            item,
            character.ClassId,
            character.Level,
            derived.ClassProfile,
            hasDualWieldPermission);
    }

    private async Task<WorldContractCompletionReward> CompleteWorldContractsAsync(
        Character character,
        Guid combatSessionId,
        IReadOnlyList<ResolvedRewardSource> rewardSources,
        IReadOnlyList<WorldContractDefinition> contracts,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        if (contracts.Count == 0)
            return WorldContractCompletionReward.Empty;

        HashSet<string> defeatedMonsterIds = rewardSources
            .Select(source => source.Monster.Id)
            .ToHashSet(StringComparer.Ordinal);
        string[] completedContractIds = await dbContext.CharacterContractCompletions
            .Where(state => state.CharacterId == character.Id)
            .Select(state => state.ContractId)
            .ToArrayAsync(cancellationToken);
        HashSet<string> completed = completedContractIds.ToHashSet(StringComparer.Ordinal);
        string[] acceptedContractIds = await dbContext.CharacterContractAcceptances
            .Where(state => state.CharacterId == character.Id)
            .Select(state => state.ContractId)
            .ToArrayAsync(cancellationToken);
        HashSet<string> accepted = acceptedContractIds.ToHashSet(StringComparer.Ordinal);

        int xp = 0;
        int gold = 0;
        List<string> contractIds = [];
        foreach (WorldContractDefinition contract in contracts)
        {
            if (character.Level < contract.RequiredLevel
                || !accepted.Contains(contract.Id)
                || !defeatedMonsterIds.Contains(contract.TargetMonsterId)
                || completed.Contains(contract.Id))
            {
                continue;
            }

            dbContext.CharacterContractCompletions.Add(
                new CharacterContractCompletion(
                    character.Id,
                    contract.Id,
                    contract.TargetMonsterId,
                    combatSessionId,
                    completedAtUtc));
            completed.Add(contract.Id);
            contractIds.Add(contract.Id);
            xp = checked(xp + contract.RewardXp);
            gold = checked(gold + contract.RewardGold);
        }

        return new WorldContractCompletionReward(xp, gold, contractIds.ToArray());
    }

    private static ResolvedRewardSource[] ResolveRewardSources(
        CombatSessionSnapshot snapshot,
        GameContentIndexes indexes)
    {
        CombatActorSnapshot[] enemies =
            snapshot.Enemies?.ToArray() ?? [snapshot.Enemy];
        if (enemies.Length == 0)
            throw new InvalidOperationException(
                "Victory snapshot must contain at least one defeated enemy.");
        if (enemies.Select(enemy => enemy.ActorId).Distinct().Count() != enemies.Length)
            throw new InvalidOperationException(
                "Victory snapshot contains duplicate enemy actor identifiers.");
        if (enemies.Any(enemy =>
                enemy.Kind != CombatActorKind.Monster
                || enemy.Hp > 0))
        {
            throw new InvalidOperationException(
                "Victory rewards require only defeated monster participants.");
        }

        ResolvedRewardSource[] result = new ResolvedRewardSource[enemies.Length];
        for (var index = 0; index < enemies.Length; index++)
        {
            CombatActorSnapshot enemy = enemies[index];
            if (!indexes.MonstersById.TryGetValue(
                    enemy.DefinitionId,
                    out MonsterDefinition? monster))
            {
                throw new InvalidOperationException(
                    $"Monster '{enemy.DefinitionId}' is missing from game content.");
            }

            result[index] = new ResolvedRewardSource(enemy, monster, index);
        }

        return result;
    }

    private static LootRoll[] AggregateLoot(IEnumerable<LootRoll> rolls) =>
        rolls.GroupBy(roll => roll.ItemId, StringComparer.Ordinal)
            .Select(group => new LootRoll(
                group.Key,
                checked(group.Sum(roll => roll.Quantity))))
            .ToArray();

    private int RollGold(MonsterDefinition monster)
    {
        if (monster.GoldRewardMax <= 0 || monster.GoldRewardMax < monster.GoldRewardMin)
            return 0;
        int span = monster.GoldRewardMax - monster.GoldRewardMin + 1;
        int offset = (int)decimal.Floor(randomFactory.Create().NextUnit() * span);
        return monster.GoldRewardMin + Math.Min(offset, span - 1);
    }

    private IReadOnlyList<LootRoll> RollLoot(
        MonsterDefinition monster,
        GameContentIndexes indexes)
    {
        if (string.IsNullOrWhiteSpace(monster.LootTableId))
            return [];

        if (!indexes.LootTablesById.TryGetValue(
                monster.LootTableId,
                out LootTableDefinition? table))
        {
            throw new InvalidOperationException(
                $"Loot table '{monster.LootTableId}' is missing from game content.");
        }
        return LootRoller.Roll(table, randomFactory.Create());
    }

    private async Task AddItemAsync(
        Guid characterId,
        Guid rewardResolutionId,
        LootRoll roll,
        DateTimeOffset acquiredAtUtc,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        GameContentIndexes indexes = contentSnapshot.Indexes;
        if (!indexes.ItemsById.TryGetValue(
                roll.ItemId,
                out ItemDefinition? definition))
        {
            throw new InvalidOperationException(
                $"Item '{roll.ItemId}' is missing from game content.");
        }

        if (!definition.Stackable)
        {
            int freeSlots = await InventoryCapacity.FreeSlotsAsync(
                dbContext,
                characterId,
                contentSnapshot,
                cancellationToken);
            for (var index = 0; index < roll.Quantity; index++)
            {
                PrimaryStats? rolledStats = definition.Type == ItemType.Equipment
                    ? ItemInstanceStatRoller.Resolve(
                        definition,
                        randomFactory.Create())
                    : null;
                if (freeSlots > 0)
                {
                    dbContext.CharacterItems.Add(new CharacterItem(
                        Guid.NewGuid(),
                        characterId,
                        definition.Id,
                        1,
                        acquiredAtUtc,
                        definition.Version,
                        rolledStats));
                    freeSlots--;
                }
                else
                {
                    dbContext.PendingLootItems.Add(new PendingLootItem(
                        Guid.NewGuid(),
                        characterId,
                        rewardResolutionId,
                        definition.Id,
                        1,
                        definition.Version,
                        acquiredAtUtc,
                        rolledStats));
                }
            }
            return;
        }

        int remaining = roll.Quantity;
        CharacterItem[] stacks = await dbContext.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
                && item.DefinitionVersion == definition.Version
                && item.Quantity < definition.MaxStack)
            .OrderBy(item => item.AcquiredAtUtc)
            .ToArrayAsync(cancellationToken);

        foreach (CharacterItem stack in stacks)
        {
            if (remaining <= 0) break;
            int available = definition.MaxStack - stack.Quantity;
            int toAdd = Math.Min(available, remaining);
            if (toAdd <= 0) continue;
            stack.AddQuantity(toAdd, definition.MaxStack);
            remaining -= toAdd;
        }

        int freeStackSlots = await InventoryCapacity.FreeSlotsAsync(
            dbContext,
            characterId,
            contentSnapshot,
            cancellationToken);
        while (remaining > 0 && freeStackSlots > 0)
        {
            int quantity = Math.Min(definition.MaxStack, remaining);
            dbContext.CharacterItems.Add(new CharacterItem(
                Guid.NewGuid(),
                characterId,
                definition.Id,
                quantity,
                acquiredAtUtc,
                definition.Version));
            remaining -= quantity;
            freeStackSlots--;
        }

        if (remaining > 0)
        {
            dbContext.PendingLootItems.Add(new PendingLootItem(
                Guid.NewGuid(),
                characterId,
                rewardResolutionId,
                definition.Id,
                remaining,
                definition.Version,
                acquiredAtUtc));
        }
    }

    private sealed record ResolvedRewardSource(
        CombatActorSnapshot Enemy,
        MonsterDefinition Monster,
        int EncounterOrder);

    private sealed record WorldContractCompletionReward(
        int Xp,
        int Gold,
        IReadOnlyList<string> ContractIds)
    {
        public static WorldContractCompletionReward Empty { get; } =
            new(0, 0, []);
    }

    private static CombatRewardItemResult ToRewardItem(
        LootRoll roll,
        GameContentIndexes indexes)
    {
        if (!indexes.ItemsById.TryGetValue(roll.ItemId, out ItemDefinition? definition))
            throw new InvalidOperationException($"Item '{roll.ItemId}' is missing from game content.");
        return new CombatRewardItemResult(
            definition.Id,
            definition.Name,
            definition.Type,
            definition.Rarity,
            roll.Quantity);
    }
}
