using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Progression;

public sealed record CombatRewardApplicationResult(
    bool Granted,
    int XpEarned,
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

        if (progressionResult.LeveledUp)
        {
            CharacterVitals vitals = await dbContext.CharacterVitals.SingleAsync(
                candidate => candidate.CharacterId == characterId,
                cancellationToken);
            CharacterStats stats = await CalculateStatsAsync(character, cancellationToken);
            vitals.Checkpoint(stats.MaxHp, vitals.CurrentResource, now);
        }

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
            loot.Select(ToRewardItem).ToArray());
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

    private async Task<CharacterStats> CalculateStatsAsync(
        Character character,
        CancellationToken cancellationToken)
    {
        Guid[] equippedItemIds = await dbContext.CharacterEquipment
            .Where(item => item.CharacterId == character.Id)
            .Select(item => item.CharacterItemId)
            .ToArrayAsync(cancellationToken);
        string[] equippedDefinitionIds = await dbContext.CharacterItems
            .Where(item => equippedItemIds.Contains(item.Id))
            .Select(item => item.ItemDefinitionId)
            .ToArrayAsync(cancellationToken);
        HashSet<string> equippedDefinitions = equippedDefinitionIds.ToHashSet(StringComparer.Ordinal);
        PrimaryStats equipment = EquipmentStatModifierResolver.Resolve(
            (content.Items ?? []).Where(item => equippedDefinitions.Contains(item.Id)));

        TalentPrimaryStatPercentages talentPercentages = TalentPrimaryStatPercentages.Empty;
        TalentStatModifiers talentDerived = new();
        TalentTreeDefinition? tree = content.TalentTrees?.SingleOrDefault(candidate =>
            string.Equals(candidate.ClassId, character.ClassId, StringComparison.Ordinal));
        if (tree is not null)
        {
            CharacterTalentState? talentState = await dbContext.CharacterTalentStates
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
            if (talentState is not null)
            {
                ResolvedTalentModifiers talents = TalentModifierResolver.Resolve(
                    tree,
                    talentState.GetRanks(talentState.ActiveLoadoutId));
                talentPercentages = new TalentPrimaryStatPercentages(
                    talents.Stats.StrengthPercent,
                    0,
                    0,
                    talents.Stats.StaminaPercent);
                talentDerived = talents.Stats;
            }
        }

        return new CharacterStatCalculator(
            content.StatFormula
                ?? throw new InvalidOperationException("Stat formula content is required."),
            content.ClassProfiles
                ?? throw new InvalidOperationException("Class profiles are required."))
            .Calculate(
                character.ClassId,
                character.Level,
                CharacterStatInputs.Empty with
                {
                    Equipment = equipment,
                    TalentPercentages = talentPercentages,
                    TalentDerived = talentDerived
                });
    }

    private CombatRewardItemResult ToRewardItem(LootRoll roll)
    {
        ItemDefinition definition = (content.Items ?? []).Single(item =>
            string.Equals(item.Id, roll.ItemId, StringComparison.Ordinal));
        return new CombatRewardItemResult(
            definition.Id,
            definition.Name,
            definition.Type,
            definition.Rarity,
            roll.Quantity);
    }
}
