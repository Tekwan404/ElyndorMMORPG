using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Progression;

public sealed record CombatRewardApplicationResult(
    bool Granted,
    int XpEarned,
    int GoldEarned,
    CharacterProgressionResult? Progression,
    IReadOnlyList<CombatRewardItemResult> Items);

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
                grant => grant.CombatSessionId == snapshot.SessionId,
                cancellationToken);
        if (existingGrant is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CombatRewardApplicationResult(
                false,
                existingGrant.XpEarned,
                existingGrant.GoldEarned,
                null,
                []);
        }

        GameContentPackage content = contentSnapshot.Package;
        GameContentIndexes indexes = contentSnapshot.Indexes;
        ResolvedRewardSource[] rewardSources = ResolveRewardSources(snapshot, indexes);
        LevelProgressionDefinition progression = content.LevelProgression
            ?? throw new InvalidOperationException("Level progression content is required for combat rewards.");

        List<CombatRewardSourceAudit> sourceAudits = [];
        List<LootRoll> rolledLoot = [];
        int xpEarned = 0;
        int goldEarned = 0;
        foreach (ResolvedRewardSource source in rewardSources)
        {
            int sourceXp = source.Monster.XpReward;
            int sourceGold = RollGold(source.Monster);
            IReadOnlyList<LootRoll> sourceLoot = RollLoot(source.Monster, indexes);

            xpEarned = checked(xpEarned + sourceXp);
            goldEarned = checked(goldEarned + sourceGold);
            rolledLoot.AddRange(sourceLoot);
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

        LootRoll[] loot = AggregateLoot(rolledLoot);
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (LootRoll roll in loot)
            await AddItemAsync(characterId, roll, now, indexes, cancellationToken);

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
            loot.Select(roll => ToRewardItem(roll, indexes)).ToArray());
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
        LootRoll roll,
        DateTimeOffset acquiredAtUtc,
        GameContentIndexes indexes,
        CancellationToken cancellationToken)
    {
        if (!indexes.ItemsById.TryGetValue(roll.ItemId, out ItemDefinition? definition))
            throw new InvalidOperationException($"Item '{roll.ItemId}' is missing from game content.");

        if (!definition.Stackable)
        {
            for (var index = 0; index < roll.Quantity; index++)
            {
                dbContext.CharacterItems.Add(new CharacterItem(
                    Guid.NewGuid(),
                    characterId,
                    definition.Id,
                    1,
                    acquiredAtUtc,
                    definition.Version,
                    definition.Type == ItemType.Equipment
                        ? ItemInstanceStatRoller.Resolve(definition, randomFactory.Create())
                        : null));
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

        while (remaining > 0)
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
        }
    }

    private sealed record ResolvedRewardSource(
        CombatActorSnapshot Enemy,
        MonsterDefinition Monster,
        int EncounterOrder);

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
