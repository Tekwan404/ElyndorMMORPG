using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Progression;

public sealed record CombatRewardApplicationResult(
    bool Granted,
    int XpEarned,
    CharacterProgressionResult? Progression,
    IReadOnlyList<LootRoll> Loot);

public sealed class CombatRewardService(
    GameDbContext dbContext,
    GameContentPackage content,
    IGameRandomFactory randomFactory,
    TimeProvider timeProvider)
{
    public async Task<CombatRewardApplicationResult> ApplyVictoryAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status != CombatSessionStatus.Victory)
            return new CombatRewardApplicationResult(false, 0, null, []);

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
                null,
                []);
        }

        Character character = await dbContext.Characters
            .SingleAsync(candidate => candidate.Id == characterId, cancellationToken);
        MonsterDefinition monster = (content.Monsters ?? [])
            .Single(candidate => string.Equals(
                candidate.Id,
                snapshot.Enemy.DefinitionId,
                StringComparison.Ordinal));
        LevelProgressionDefinition progression = content.LevelProgression
            ?? throw new InvalidOperationException("Level progression content is required for combat rewards.");

        CharacterProgressionResult progressionResult = CharacterProgression.GrantExperience(
            character,
            monster.XpReward,
            progression);

        IReadOnlyList<LootRoll> loot = RollLoot(monster);
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (LootRoll roll in loot)
            await AddItemAsync(characterId, roll, now, cancellationToken);

        dbContext.CombatRewardGrants.Add(new CombatRewardGrant(
            snapshot.SessionId,
            characterId,
            monster.Id,
            monster.XpReward,
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CombatRewardApplicationResult(
            true,
            monster.XpReward,
            progressionResult,
            loot);
    }

    private IReadOnlyList<LootRoll> RollLoot(MonsterDefinition monster)
    {
        if (string.IsNullOrWhiteSpace(monster.LootTableId))
            return [];

        LootTableDefinition table = (content.LootTables ?? [])
            .Single(candidate => string.Equals(
                candidate.Id,
                monster.LootTableId,
                StringComparison.Ordinal));
        return LootRoller.Roll(table, randomFactory.Create());
    }

    private async Task AddItemAsync(
        Guid characterId,
        LootRoll roll,
        DateTimeOffset acquiredAtUtc,
        CancellationToken cancellationToken)
    {
        ItemDefinition definition = (content.Items ?? [])
            .Single(candidate => string.Equals(candidate.Id, roll.ItemId, StringComparison.Ordinal));

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
}
