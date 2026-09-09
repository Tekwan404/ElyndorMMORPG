using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Core.Talents;
using Elyndor.Core.Items;
using Elyndor.Core.Quests;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.World;

public sealed record BootstrapAbility(
    string Id,
    string DisplayName,
    string Description,
    string? IconId,
    decimal ResourceCost,
    decimal CooldownSeconds,
    string Type,
    string TargetType,
    string? SourceTalentId,
    string? SourceTalentName);

public sealed record BootstrapCharacter(
    Guid Id,
    string Name,
    string PublicCode,
    string RaceId,
    string GenderId,
    string ClassId,
    int Level,
    long Experience,
    int XpToNextLevel,
    long Gold,
    string PrimaryAttribute,
    string ClassProfileVersion,
    IReadOnlyList<string> KnownAbilityIds,
    IReadOnlyList<BootstrapAbility> KnownAbilities,
    CharacterStats Stats,
    IReadOnlyDictionary<string, CharacterStatBreakdown> StatBreakdown,
    BootstrapVitals Vitals,
    InventorySnapshot Inventory);

public sealed record BootstrapVitals(
    decimal CurrentHp,
    decimal MaxHp,
    string ResourceType,
    decimal CurrentResource,
    decimal MaxResource,
    DateTimeOffset CheckpointedAtUtc);

public sealed record BootstrapLocation(
    string Id,
    string DisplayName,
    string DangerLevel,
    int RecommendedLevel,
    int MinimumLevel,
    int MaximumLevel,
    string? RequiredContractId,
    string? ArtId,
    string Description,
    decimal TravelDurationSeconds);

public sealed record BootstrapWorldContract(
    string Id,
    string DisplayName,
    string Description,
    int RequiredLevel,
    string TargetMonsterId,
    string UnlockLocationId,
    string Status,
    string? OfferLocationId,
    int RewardXp,
    int RewardGold);

public sealed record BootstrapTravel(
    string FromLocationId,
    string TargetLocationId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc);

public sealed record BootstrapWorld(
    BootstrapLocation CurrentLocation,
    long Version,
    IReadOnlyList<BootstrapLocation> OutgoingTransitions,
    IReadOnlyList<BootstrapWorldContract> Contracts,
    BootstrapTravel? Travel);

public sealed record BootstrapSnapshot(
    Guid AccountId,
    BootstrapCharacter? Character,
    BootstrapWorld? World,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset ServerTimeUtc);

public sealed class BootstrapService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService,
    TimeProvider timeProvider,
    ILogger<BootstrapService>? logger = null)
{
    public BootstrapService(
        GameDbContext dbContext,
        GameContentPackage contentPackage,
        WorldMap worldMap,
        CharacterDerivedStateService derivedStateService,
        TimeProvider timeProvider)
        : this(
            dbContext,
            new StaticContentSnapshotProvider(contentPackage),
            derivedStateService,
            timeProvider,
            null)
    {
    }

    private const decimal StarterTownHpRegenPerSecond = 5m;
    private const string StartingEquipmentRepairOperation = "STARTING_EQUIPMENT_V1";
    private static readonly Guid StartingEquipmentRepairMutationId =
        Guid.Parse("8f6d78d5-8f60-4a0b-9c9f-2e36bca1d501");
    private static readonly string StartingEquipmentRepairFingerprint =
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(StartingEquipmentRepairOperation)));

    private static readonly Action<ILogger, Guid, string, Exception?>
        RepairedCharacterState =
            LoggerMessage.Define<Guid, string>(
                LogLevel.Warning,
                new EventId(2201, nameof(RepairedCharacterState)),
                "Repaired bootstrap state for character {CharacterId}: {Repair}.");


    public Task<BootstrapSnapshot> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken,
        bool checkpoint = false) =>
        GetAsync(
            accountId,
            contentProvider.GetCurrent(),
            cancellationToken,
            checkpoint);

    public async Task<BootstrapSnapshot> GetAsync(
        Guid accountId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken,
        bool checkpoint = false)
    {
        ArgumentNullException.ThrowIfNull(contentSnapshot);
        GameContentPackage contentPackage = contentSnapshot.Package;
        GameContentIndexes indexes = contentSnapshot.Indexes;
        WorldMap worldMap = contentSnapshot.WorldMap;

        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
        {
            return new BootstrapSnapshot(
                accountId,
                null,
                null,
                contentPackage.ContentVersion,
                contentPackage.BalanceVersion,
                timeProvider.GetUtcNow());
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await EnsureStartingEquipmentAsync(
                character,
                contentSnapshot,
                now,
                cancellationToken))
        {
            LogRepair(character.Id, "starting_equipment_repaired");
        }

        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        CharacterStats stats = derived.Stats;
        ClassProfile classProfile = derived.ClassProfile;
        ResourceProfile effectiveResourceProfile = derived.EffectiveResourceProfile;

        BootstrapAbility[] knownAbilities = derived.KnownAbilityIds
            .Select(abilityId => ToBootstrapAbility(
                abilityId,
                derived.TalentTree,
                derived.ActiveTalentRanks,
                derived.TalentModifiers,
                indexes))
            .ToArray();

        CharacterVitals? vitals = await dbContext.CharacterVitals
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        CharacterLocation? location = await dbContext.CharacterLocations
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        CharacterTravelState? activeTravel = await dbContext.CharacterTravelStates
            .SingleOrDefaultAsync(
                state => state.CharacterId == character.Id,
                cancellationToken);

        bool repaired = false;
        if (vitals is null)
        {
            vitals = new CharacterVitals(
                character.Id,
                stats.MaxHp,
                effectiveResourceProfile.StartValue,
                now,
                now);
            dbContext.CharacterVitals.Add(vitals);
            repaired = true;
            LogRepair(character.Id, "missing_vitals_created");
        }

        bool locationMissing = location is null;
        bool locationUnknown = location is not null
            && !indexes.LocationsById.ContainsKey(location.LocationId);
        if (locationMissing)
        {
            location = new CharacterLocation(
                character.Id,
                WorldLocationIds.StarterTown,
                1,
                now);
            dbContext.CharacterLocations.Add(location);
            repaired = true;
            LogRepair(character.Id, "missing_location_created");
        }
        else if (locationUnknown)
        {
            location!.Relocate(WorldLocationIds.StarterTown, now);
            repaired = true;
            LogRepair(character.Id, "unknown_location_relocated");
        }

        if (activeTravel is not null
            && (!indexes.LocationsById.ContainsKey(activeTravel.FromLocationId)
                || !indexes.LocationsById.ContainsKey(activeTravel.TargetLocationId)
                || !string.Equals(
                    location!.LocationId,
                    activeTravel.FromLocationId,
                    StringComparison.Ordinal)))
        {
            dbContext.CharacterTravelStates.Remove(activeTravel);
            activeTravel = null;
            repaired = true;
            LogRepair(character.Id, "invalid_travel_cancelled");
        }

        if (repaired)
            await dbContext.SaveChangesAsync(cancellationToken);

        await TravelPersistence.CompleteDueAsync(
            dbContext,
            character.Id,
            now,
            cancellationToken);

        activeTravel = await dbContext.CharacterTravelStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.CharacterId == character.Id,
                cancellationToken);
        LocationDefinition current = worldMap.GetRequired(location!.LocationId);

        TimeSpan elapsed = now - vitals.CheckpointedAtUtc;
        TimeSpan contextElapsed = now - vitals.ContextStartedAtUtc;
        decimal currentResource = CharacterResourceRules.ApplyElapsed(
            effectiveResourceProfile,
            vitals.CurrentResource,
            elapsed,
            isInCombat: false,
            contextElapsed);
        decimal currentHp = decimal.Clamp(vitals.CurrentHp, 0, stats.MaxHp);

        if (string.Equals(current.Id, WorldLocationIds.StarterTown, StringComparison.Ordinal)
            && currentHp < stats.MaxHp)
        {
            DateTimeOffset recoveryFrom = vitals.CheckpointedAtUtc > location.UpdatedAtUtc
                ? vitals.CheckpointedAtUtc
                : location.UpdatedAtUtc;
            TimeSpan recoveryElapsed = now - recoveryFrom;
            if (recoveryElapsed > TimeSpan.Zero)
            {
                decimal elapsedSeconds = Math.Max(0m, (decimal)recoveryElapsed.TotalSeconds);
                currentHp = Math.Min(
                    stats.MaxHp,
                    currentHp + (elapsedSeconds * StarterTownHpRegenPerSecond));
            }
        }

        if (checkpoint)
        {
            vitals.Checkpoint(currentHp, currentResource, now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        string[] completedContractIdValues = await dbContext.CharacterContractCompletions
            .AsNoTracking()
            .Where(state => state.CharacterId == character.Id)
            .Select(state => state.ContractId)
            .ToArrayAsync(cancellationToken);
        HashSet<string> completedContractIds =
            completedContractIdValues.ToHashSet(StringComparer.Ordinal);

        string[] acceptedContractIdValues = await dbContext.CharacterContractAcceptances
            .AsNoTracking()
            .Where(state => state.CharacterId == character.Id)
            .Select(state => state.ContractId)
            .ToArrayAsync(cancellationToken);
        HashSet<string> acceptedContractIds =
            acceptedContractIdValues.ToHashSet(StringComparer.Ordinal);

        string[] completedQuestStateIds = await dbContext.CharacterQuestStates
            .AsNoTracking()
            .Where(state => state.CharacterId == character.Id
                && state.Status == QuestStateStatuses.Completed)
            .Select(state => state.QuestId)
            .ToArrayAsync(cancellationToken);
        string[] rewardedQuestIds = await dbContext.QuestRewardGrants
            .AsNoTracking()
            .Where(grant => grant.CharacterId == character.Id)
            .Select(grant => grant.QuestId)
            .ToArrayAsync(cancellationToken);
        HashSet<string> completedQuestIds = completedContractIds
            .Concat(completedQuestStateIds)
            .Concat(rewardedQuestIds)
            .ToHashSet(StringComparer.Ordinal);

        BootstrapLocation[] transitions = activeTravel is not null
            ? []
            : current.Transitions
                .Select(worldMap.GetRequired)
                .Where(target => character.Level >= target.MinimumLevel)
                .Where(target => target.RequiredContractId is null
                    || completedContractIds.Contains(target.RequiredContractId))
                .Select(ToLocation)
                .ToArray();

        BootstrapWorldContract[] contracts = (contentPackage.WorldContracts ?? [])
            .Select(contract =>
            {
                QuestDefinition? quest = QuestCatalog.Find(
                    contentPackage,
                    contract.Id);
                bool prerequisitesMet = (quest?.PrerequisiteQuestIds ?? [])
                    .All(completedQuestIds.Contains);
                string status = completedContractIds.Contains(contract.Id)
                    ? "COMPLETED"
                    : acceptedContractIds.Contains(contract.Id)
                        ? "ACTIVE"
                        : character.Level >= contract.RequiredLevel
                            && prerequisitesMet
                            ? "AVAILABLE"
                            : "LOCKED";

                return new BootstrapWorldContract(
                    contract.Id,
                    contract.DisplayName,
                    contract.Description,
                    contract.RequiredLevel,
                    contract.TargetMonsterId,
                    contract.UnlockLocationId,
                    status,
                    contract.OfferLocationId,
                    contract.RewardXp,
                    contract.RewardGold);
            })
            .ToArray();

        return new BootstrapSnapshot(
            accountId,
            new BootstrapCharacter(
                character.Id,
                character.Name,
                character.PublicCode,
                character.RaceId,
                character.GenderId,
                character.ClassId,
                character.Level,
                character.Experience,
                (contentPackage.LevelProgression
                    ?? throw new InvalidOperationException("Level progression content is required."))
                    .XpToNext(character.Level),
                character.Gold,
                derived.EffectivePrimaryAttribute,
                contentPackage.BalanceVersion,
                derived.KnownAbilityIds,
                knownAbilities,
                stats,
                derived.StatCalculation.Breakdown,
                new BootstrapVitals(
                    currentHp,
                    stats.MaxHp,
                    effectiveResourceProfile.Id,
                    currentResource,
                    effectiveResourceProfile.MaxValue,
                    checkpoint ? now : vitals.CheckpointedAtUtc),
                derived.Inventory),
            new BootstrapWorld(
                ToLocation(current),
                location.Version,
                transitions,
                contracts,
                activeTravel is null
                    ? null
                    : new BootstrapTravel(
                        activeTravel.FromLocationId,
                        activeTravel.TargetLocationId,
                        activeTravel.StartedAtUtc,
                        activeTravel.EndsAtUtc)),
            contentPackage.ContentVersion,
            contentPackage.BalanceVersion,
            now);
    }

    private async Task<bool> EnsureStartingEquipmentAsync(
        Character character,
        GameContentSnapshot contentSnapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!contentSnapshot.Indexes.ClassesById.TryGetValue(
                character.ClassId,
                out ClassProfile? classProfile)
            || classProfile.StartingEquipmentItemIds is not { Count: > 0 })
        {
            return false;
        }

        if (await dbContext.CharacterMutations
            .AsNoTracking()
            .AnyAsync(
                mutation => mutation.CharacterId == character.Id
                    && mutation.MutationId == StartingEquipmentRepairMutationId,
                cancellationToken))
        {
            return false;
        }

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        _ = await dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"Id\" = {character.Id} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        if (await dbContext.CharacterMutations
            .AsNoTracking()
            .AnyAsync(
                mutation => mutation.CharacterId == character.Id
                    && mutation.MutationId == StartingEquipmentRepairMutationId,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        EquipmentSlot[] mainHandSlots = [EquipmentSlot.MainHand, EquipmentSlot.Weapon];
        CharacterEquipment[] mainHandRows = await dbContext.CharacterEquipment
            .Where(equipment => equipment.CharacterId == character.Id
                && mainHandSlots.Contains(equipment.Slot))
            .ToArrayAsync(cancellationToken);
        Guid[] equippedItemIds = mainHandRows
            .Select(equipment => equipment.CharacterItemId)
            .Distinct()
            .ToArray();
        CharacterItem[] equippedItems = await dbContext.CharacterItems
            .AsNoTracking()
            .Where(item => equippedItemIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);

        bool hasValidMainHand = equippedItems.Any(item =>
            contentSnapshot.Indexes.ItemsById.TryGetValue(
                item.ItemDefinitionId,
                out ItemDefinition? definition)
            && definition.Type == ItemType.Equipment
            && definition.WeaponCategory is not null
            && classProfile.AllowedWeaponCategories.Contains(
                definition.WeaponCategory,
                StringComparer.Ordinal)
            && (definition.AllowedClassIds is null
                || definition.AllowedClassIds.Contains(
                    character.ClassId,
                    StringComparer.Ordinal)));

        bool granted = false;
        if (!hasValidMainHand)
        {
            ItemDefinition? starterWeapon = classProfile.StartingEquipmentItemIds
                .Select(itemId =>
                    contentSnapshot.Indexes.ItemsById.GetValueOrDefault(itemId))
                .FirstOrDefault(definition =>
                    definition?.Type == ItemType.Equipment
                    && definition.Slot is EquipmentSlot.MainHand or EquipmentSlot.Weapon
                    && definition.WeaponCategory is not null);

            if (starterWeapon is not null)
            {
                dbContext.CharacterEquipment.RemoveRange(mainHandRows);

                CharacterItem starterItem =
                    ItemInstancePersistenceFactory.CreateCharacterItem(
                        character.Id,
                        starterWeapon,
                        character.Id,
                        "STARTING_EQUIPMENT_REPAIR",
                        $"STARTING_EQUIPMENT_REPAIR:{starterWeapon.Id}",
                        0,
                        now,
                        contentSnapshot.Package);
                dbContext.CharacterItems.Add(starterItem);
                dbContext.CharacterEquipment.Add(new CharacterEquipment(
                    character.Id,
                    EquipmentSlot.MainHand,
                    starterItem.Id));
                granted = true;
            }
        }

        dbContext.CharacterMutations.Add(new CharacterMutation(
            character.Id,
            StartingEquipmentRepairMutationId,
            StartingEquipmentRepairOperation,
            StartingEquipmentRepairFingerprint,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return granted;
    }

    private void LogRepair(Guid characterId, string repair)
    {
        if (logger is not null)
            RepairedCharacterState(logger, characterId, repair, null);
    }

    private static BootstrapAbility ToBootstrapAbility(
        string abilityId,
        TalentTreeDefinition? talentTree,
        IReadOnlyDictionary<string, int> activeTalentRanks,
        ResolvedTalentModifiers talentModifiers,
        GameContentIndexes indexes)
    {
        if (!indexes.AbilitiesById.TryGetValue(
                abilityId,
                out AbilityDefinition? baseDefinition))
        {
            throw new InvalidOperationException(
                $"Ability '{abilityId}' is missing from game content.");
        }
        AbilityDefinition definition = TalentAbilityResolver.Apply(baseDefinition, talentModifiers);
        TalentDefinition? sourceTalent = talentTree?.Nodes.FirstOrDefault(node =>
            activeTalentRanks.GetValueOrDefault(node.Id) > 0
            && (node.Modifiers ?? []).Any(modifier =>
                modifier.RuntimeStatus != TalentModifierRuntimeStatus.Deferred
                && modifier.Type == TalentModifierType.AbilityModifier
                && modifier.Key == TalentModifierKeys.UnlockAbility
                && string.Equals(modifier.TargetId, abilityId, StringComparison.Ordinal)));
        return new BootstrapAbility(
            definition.Id,
            definition.DisplayName ?? definition.Id,
            definition.Description ?? string.Empty,
            definition.IconId,
            definition.ResourceCost,
            (decimal)definition.Cooldown.TotalSeconds,
            definition.Type.ToString(),
            definition.TargetType.ToString(),
            sourceTalent?.Id,
            sourceTalent?.Name);
    }

    private static BootstrapLocation ToLocation(LocationDefinition location) =>
        new(
            location.Id,
            location.DisplayName,
            location.DangerLevel,
            location.RecommendedLevel,
            location.MinimumLevel,
            location.MaximumLevel,
            location.RequiredContractId,
            location.ArtId,
            location.Description,
            location.TravelDurationSeconds);
}
