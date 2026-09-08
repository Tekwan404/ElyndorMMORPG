using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Progression;

public sealed record CombatLootRollChoiceResult(
    bool Succeeded,
    string? ErrorCode,
    CombatLootRoll? Roll,
    Guid? WinnerCharacterId,
    bool CanNeed = false);

public sealed record CombatLootRollAvailability(
    CombatLootRoll Roll,
    bool CanNeed);

public sealed class CombatLootRollService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService,
    IGameRandomFactory randomFactory,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CombatLootRollAvailability>> GetOpenAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await ResolveExpiredAsync(cancellationToken);

        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
            return [];

        CombatLootRoll[] openRolls = await dbContext.CombatLootRolls
            .AsNoTracking()
            .Where(roll => roll.State == CombatLootRollState.Open)
            .OrderBy(roll => roll.EndsAtUtc)
            .ToArrayAsync(cancellationToken);
        CombatLootRoll[] eligibleRolls = openRolls
            .Where(roll => IsEligible(roll, character.Id))
            .ToArray();
        if (eligibleRolls.Length == 0)
            return [];

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        return eligibleRolls
            .Select(roll => new CombatLootRollAvailability(
                roll,
                contentSnapshot.Indexes.ItemsById.TryGetValue(
                    roll.ItemDefinitionId,
                    out ItemDefinition? item)
                    && CanNeed(item, character, derived)))
            .ToArray();
    }

    public Task<CombatLootRollChoiceResult> ChooseAsync(
        Guid accountId,
        Guid lootRollId,
        LootChoice choice,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ChooseCoreAsync(accountId, lootRollId, choice, cancellationToken));

    private async Task<CombatLootRollChoiceResult> ChooseCoreAsync(
        Guid accountId,
        Guid lootRollId,
        LootChoice choice,
        CancellationToken cancellationToken)
    {
        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? character = await dbContext.Characters
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        CombatLootRoll? roll = await dbContext.CombatLootRolls
            .FromSqlInterpolated($"SELECT * FROM game.combat_loot_rolls WHERE \"LootRollId\" = {lootRollId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null || roll is null)
            return new(false, "loot_roll_not_found", null, null);
        if (!IsEligible(roll, character.Id))
            return new(false, "loot_roll_not_eligible", null, null);
        if (roll.State != CombatLootRollState.Open)
            return new(true, null, null, roll.WinnerCharacterId);

        if (timeProvider.GetUtcNow() >= roll.EndsAtUtc)
            choice = LootChoice.Pass;

        if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                roll.ItemDefinitionId,
                out ItemDefinition? item))
            return new(false, "loot_item_not_found", roll, null);

        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        bool canNeed = CanNeed(item, character, derived);
        if (choice == LootChoice.Need)
        {
            if (!canNeed)
                return new(false, "loot_need_not_allowed", roll, null);
        }

        Dictionary<Guid, LootChoice> choices = ReadChoices(roll.ChoicesJson);
        choices[character.Id] = choice;
        Guid[] eligibleCharacterIds = ReadEligibleCharacterIds(roll);
        bool shouldResolve = timeProvider.GetUtcNow() >= roll.EndsAtUtc
            || eligibleCharacterIds.All(choices.ContainsKey);
        Guid? winner = null;
        if (shouldResolve)
        {
            foreach (Guid eligibleCharacterId in eligibleCharacterIds)
                choices.TryAdd(eligibleCharacterId, LootChoice.Pass);
            LootRollResolution resolution = LootRollRules.Resolve(choices, randomFactory.Create());
            winner = resolution.WinnerCharacterId;
            roll.Resolve(
                winner,
                JsonSerializer.Serialize(choices),
                JsonSerializer.Serialize(resolution.Entries.ToDictionary(
                    entry => entry.CharacterId,
                    entry => entry.Roll)),
                timeProvider.GetUtcNow());
            if (winner.HasValue)
            {
                await GrantWinnerAsync(
                    winner.Value,
                    roll,
                    item,
                    contentSnapshot,
                    cancellationToken);
            }
        }
        else
        {
            roll.RecordChoice(JsonSerializer.Serialize(choices));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, null, roll, winner, canNeed);
    }

    public Task ResolveExpiredAsync(CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ResolveExpiredCoreAsync(cancellationToken));

    private async Task ResolveExpiredCoreAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        const string openState = nameof(CombatLootRollState.Open);
        CombatLootRoll[] expired;
        await using (Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            expired = await dbContext.CombatLootRolls
                .FromSqlInterpolated($"SELECT * FROM game.combat_loot_rolls WHERE \"State\" = {openState} AND \"EndsAtUtc\" <= {now} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            if (expired.Length == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
            foreach (CombatLootRoll roll in expired)
            {
                if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                        roll.ItemDefinitionId,
                        out ItemDefinition? item))
                    continue;

                Dictionary<Guid, LootChoice> choices = ReadChoices(roll.ChoicesJson);
                Guid[] eligibleCharacterIds = ReadEligibleCharacterIds(roll);
                foreach (Guid eligibleCharacterId in eligibleCharacterIds)
                    choices.TryAdd(eligibleCharacterId, LootChoice.Pass);

                LootRollResolution resolution = LootRollRules.Resolve(
                    choices,
                    randomFactory.Create());
                roll.Resolve(
                    resolution.WinnerCharacterId,
                    JsonSerializer.Serialize(choices),
                    JsonSerializer.Serialize(resolution.Entries.ToDictionary(
                        entry => entry.CharacterId,
                        entry => entry.Roll)),
                    now);
                if (resolution.WinnerCharacterId.HasValue)
                {
                    await GrantWinnerAsync(
                        resolution.WinnerCharacterId.Value,
                        roll,
                        item,
                        contentSnapshot,
                        cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task GrantWinnerAsync(
        Guid characterId,
        CombatLootRoll roll,
        ItemDefinition item,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        int freeSlots = await InventoryCapacity.FreeSlotsAsync(
            dbContext,
            characterId,
            contentSnapshot,
            cancellationToken);
        for (var index = 0; index < roll.Quantity; index++)
        {
            PrimaryStats? stats = item.Type == ItemType.Equipment
                ? ItemInstanceStatRoller.Resolve(item, randomFactory.Create())
                : null;
            if (freeSlots > 0)
            {
                dbContext.CharacterItems.Add(new CharacterItem(
                    Guid.NewGuid(),
                    characterId,
                    item.Id,
                    1,
                    timeProvider.GetUtcNow(),
                    item.Version,
                    stats));
                freeSlots--;
            }
            else
            {
                dbContext.PendingLootItems.Add(new PendingLootItem(
                    Guid.NewGuid(),
                    characterId,
                    roll.LootRollId,
                    item.Id,
                    1,
                    item.Version,
                    timeProvider.GetUtcNow(),
                    stats));
            }
        }
    }

    private static bool IsEligible(CombatLootRoll roll, Guid characterId) =>
        ReadEligibleCharacterIds(roll).Contains(characterId);

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

    private static Guid[] ReadEligibleCharacterIds(CombatLootRoll roll) =>
        JsonSerializer.Deserialize<Guid[]>(roll.EligibleCharacterIdsJson) ?? [];

    private static Dictionary<Guid, LootChoice> ReadChoices(string json) =>
        JsonSerializer.Deserialize<Dictionary<Guid, LootChoice>>(json)
        ?? [];
}
