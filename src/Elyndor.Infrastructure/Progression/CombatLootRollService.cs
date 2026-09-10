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

        Guid[] eligibleCharacterIds = ReadEligibleCharacterIds(roll);
        if (eligibleCharacterIds.Length == 1)
        {
            if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                    roll.ItemDefinitionId,
                    out ItemDefinition? soloItem))
            {
                return new(false, "loot_item_not_found", roll, null);
            }

            Guid winner = eligibleCharacterIds[0];
            roll.Resolve(winner, "{}", "{}", timeProvider.GetUtcNow());
            await GrantWinnerAsync(
                winner,
                roll,
                soloItem,
                contentSnapshot,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, null, null, winner);
        }

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
        bool shouldResolve = timeProvider.GetUtcNow() >= roll.EndsAtUtc
            || eligibleCharacterIds.All(choices.ContainsKey);
        Guid? resolvedWinner = null;
        if (shouldResolve)
        {
            foreach (Guid eligibleCharacterId in eligibleCharacterIds)
                choices.TryAdd(eligibleCharacterId, LootChoice.Pass);
            LootRollResolution resolution = LootRollRules.Resolve(choices, randomFactory.Create());
            resolvedWinner = resolution.WinnerCharacterId;
            roll.Resolve(
                resolvedWinner,
                JsonSerializer.Serialize(choices),
                JsonSerializer.Serialize(resolution.Entries.ToDictionary(
                    entry => entry.CharacterId,
                    entry => entry.Roll)),
                timeProvider.GetUtcNow());
            if (resolvedWinner.HasValue)
            {
                await GrantWinnerAsync(
                    resolvedWinner.Value,
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
        return new(true, null, roll, resolvedWinner, canNeed);
    }

    public Task ResolveExpiredAsync(CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ResolveExpiredCoreAsync(cancellationToken));

    private async Task ResolveExpiredCoreAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        const string openState = nameof(CombatLootRollState.Open);
        CombatLootRoll[] openRolls;
        await using (Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            openRolls = await dbContext.CombatLootRolls
                .FromSqlInterpolated($"SELECT * FROM game.combat_loot_rolls WHERE \"State\" = {openState} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            CombatLootRoll[] automaticRolls = openRolls
                .Where(roll => ReadEligibleCharacterIds(roll).Length <= 1 || roll.EndsAtUtc <= now)
                .ToArray();
            if (automaticRolls.Length == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
            foreach (CombatLootRoll roll in automaticRolls)
            {
                if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                        roll.ItemDefinitionId,
                        out ItemDefinition? item))
                    continue;

                Guid[] eligibleCharacterIds = ReadEligibleCharacterIds(roll);
                if (eligibleCharacterIds.Length == 1)
                {
                    Guid winner = eligibleCharacterIds[0];
                    roll.Resolve(winner, "{}", "{}", now);
                    await GrantWinnerAsync(
                        winner,
                        roll,
                        item,
                        contentSnapshot,
                        cancellationToken);
                    continue;
                }

                Dictionary<Guid, LootChoice> choices = ReadChoices(roll.ChoicesJson);
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
        ItemGenerationKey key = ItemGenerationKey.Create(roll.ItemInstanceSeed);
        GeneratedItemInstance? generated = string.IsNullOrWhiteSpace(roll.GeneratedItemJson)
            ? ProceduralItemPolicy.Generate(
                item,
                contentSnapshot.Package.Itemization,
                roll.SourceQualityProfileId,
                key)
            : JsonSerializer.Deserialize<GeneratedItemInstance>(roll.GeneratedItemJson);
        if (generated is not null && string.IsNullOrWhiteSpace(roll.GeneratedItemJson))
        {
            roll.SetGeneratedItemJson(JsonSerializer.Serialize(generated));
        }

        for (var index = 0; index < roll.Quantity; index++)
        {
            DateTimeOffset acquiredAt = timeProvider.GetUtcNow();
            PrimaryStats? legacyStats = generated is null && item.Type == ItemType.Equipment
                ? ItemInstanceStatRoller.Resolve(
                    item,
                    new SeededGameRandom(key.Seed))
                : null;
            if (freeSlots > 0)
            {
                CharacterItem characterItem = new(
                    Guid.CreateVersion7(),
                    characterId,
                    item.Id,
                    1,
                    acquiredAt,
                    item.Version,
                    legacyStats);
                if (generated is not null)
                {
                    characterItem.ApplyGeneratedInstance(
                        generated,
                        key.AuditHash,
                        "GROUP_LOOT",
                        roll.LootRollId,
                        item.Id);
                }
                dbContext.CharacterItems.Add(characterItem);
                freeSlots--;
            }
            else
            {
                dbContext.PendingLootItems.Add(new PendingLootItem(
                    Guid.CreateVersion7(),
                    characterId,
                    roll.LootRollId,
                    item.Id,
                    1,
                    item.Version,
                    acquiredAt,
                    legacyStats,
                    generated is null ? null : JsonSerializer.Serialize(generated),
                    generated is null ? null : key.AuditHash,
                    "GROUP_LOOT",
                    roll.LootRollId,
                    item.Id));
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
