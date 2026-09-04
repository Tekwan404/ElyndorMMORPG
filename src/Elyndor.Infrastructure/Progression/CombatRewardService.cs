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

    public async Task<CombatRewardApplicationResult> ApplyVictoryAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status != CombatSessionStatus.Victory)
            return new CombatRewardApplicationResult(false, 0, 0, null, []);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            () => ApplyVictoryCoreAsync(characterId, snapshot, cancellationToken));
    }

    private async Task<CombatRewardApplicationResult> ApplyVictoryCoreAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

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

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        GameContentPackage content = contentSnapshot.Package;
        GameContentIndexes indexes = contentSnapshot.Indexes;

        Character character = await dbContext.Characters
            .SingleAsync(candidate => candidate.Id == characterId, cancellationToken);
        if (!indexes.MonstersById.TryGetValue(
                snapshot.Enemy.DefinitionId,
                out MonsterDefinition? monster))
        {
            throw new InvalidOperationException(
                $"Monster '{snapshot.Enemy.DefinitionId}' is missing from game content.");
        }
        LevelProgressionDefinition progression = content.LevelProgression
            ?? throw new InvalidOperationException("Level progression content is required for combat rewards.");

        CharacterProgressionResult progressionResult = CharacterProgression.GrantExperience(
            character,
            monster.XpReward,
            progression);
        int goldEarned = RollGold(monster);
        await dbContext.Characters
            .Where(candidate => candidate.Id == characterId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.Gold,
                    candidate => candidate.Gold + goldEarned),
                cancellationToken);

        IReadOnlyList<LootRoll> loot = RollLoot(monster, indexes);
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
                cancellationToken);
            vitals.Checkpoint(
                derived.Stats.MaxHp,
                Math.Min(vitals.CurrentResource, derived.EffectiveResourceProfile.MaxValue),
                now);
        }

        dbContext.CombatRewardGrants.Add(new CombatRewardGrant(
            snapshot.SessionId,
            characterId,
            monster.Id,
            monster.XpReward,
            goldEarned,
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CombatRewardApplicationResult(
            true,
            monster.XpReward,
            goldEarned,
            progressionResult,
            loot.Select(roll => ToRewardItem(roll, indexes)).ToArray());
    }

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
                    acquiredAtUtc));
            }
            return;
        }

        int remaining = roll.Quantity;
        CharacterItem[] stacks = await dbContext.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
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
                acquiredAtUtc));
            remaining -= quantity;
        }
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
