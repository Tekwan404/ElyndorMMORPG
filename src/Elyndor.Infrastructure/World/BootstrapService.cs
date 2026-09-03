using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Core.Talents;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed record BootstrapAbility(
    string Id,
    decimal ResourceCost,
    decimal CooldownSeconds,
    string Type,
    string TargetType,
    string? SourceTalentId,
    string? SourceTalentName);

public sealed record BootstrapCharacter(
    Guid Id,
    string Name,
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
    int RecommendedLevel);

public sealed record BootstrapWorld(
    BootstrapLocation CurrentLocation,
    long Version,
    IReadOnlyList<BootstrapLocation> OutgoingTransitions);

public sealed record BootstrapSnapshot(
    Guid AccountId,
    BootstrapCharacter? Character,
    BootstrapWorld? World,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset ServerTimeUtc);

public sealed class BootstrapService(
    GameDbContext dbContext,
    GameContentPackage contentPackage,
    WorldMap worldMap,
    InventoryEquipmentService inventoryService,
    TimeProvider timeProvider)
{
    private const string StarterTownId = "STARTER_TOWN";
    private const decimal StarterTownHpRegenPerSecond = 5m;

    public async Task<BootstrapSnapshot> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken,
        bool checkpoint = false)
    {
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
        ClassProfile classProfile = (contentPackage.ClassProfiles
            ?? throw new InvalidOperationException("Class profiles are required."))
            .Single(profile => string.Equals(profile.Id, character.ClassId, StringComparison.Ordinal));
        ResourceProfile resourceProfile = (contentPackage.ResourceProfiles
            ?? throw new InvalidOperationException("Resource profiles are required."))
            .Single(profile => string.Equals(
                profile.Id,
                classProfile.ResourceProfileId,
                StringComparison.Ordinal));
        InventorySnapshot inventory = await inventoryService.GetForCharacterAsync(
            character.Id,
            cancellationToken);
        EquipmentModifierSummary equipment = EquipmentStatModifierResolver.ResolveDetailed(
            inventory.Equipped.Values.Select(item => item.Definition),
            contentPackage.EquipmentSets ?? []);
        TalentPrimaryStatPercentages talentPercentages = TalentPrimaryStatPercentages.Empty;
        ResolvedTalentModifiers talentModifiers = ResolvedTalentModifiers.Empty;
        TalentTreeDefinition? talentTree = contentPackage.TalentTrees?
            .SingleOrDefault(tree => tree.ClassId == character.ClassId);
        IReadOnlyDictionary<string, int> activeTalentRanks = new Dictionary<string, int>(StringComparer.Ordinal);
        if (talentTree is not null)
        {
            CharacterTalentState? talentState = await dbContext.CharacterTalentStates
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
            if (talentState is not null)
            {
                activeTalentRanks = talentState.GetRanks(talentState.ActiveLoadoutId);
                talentModifiers = TalentModifierResolver.Resolve(talentTree, activeTalentRanks);
                talentPercentages = new TalentPrimaryStatPercentages(
                    talentModifiers.Stats.StrengthPercent,
                    0,
                    0,
                    talentModifiers.Stats.StaminaPercent);
            }
        }
        CharacterStatCalculation statCalculation = new CharacterStatCalculator(
            contentPackage.StatFormula
                ?? throw new InvalidOperationException("Stat formula content is required."),
            contentPackage.ClassProfiles).CalculateDetailed(
                character.ClassId,
                character.Level,
                CharacterStatInputs.Empty with
                {
                    Equipment = equipment.PrimaryStats,
                    EquipmentDerived = new CharacterEquipmentDerivedModifiers(
                        equipment.AttackSpeedPercent,
                        equipment.DodgePercent),
                    TalentPercentages = talentPercentages,
                    TalentDerived = talentModifiers.Stats
                });
        CharacterStats stats = statCalculation.Stats;

        string[] knownAbilityIds = (classProfile.StartingAbilityIds ?? [])
            .Concat(talentModifiers.UnlockedAbilityIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(abilityId => abilityId, StringComparer.Ordinal)
            .ToArray();
        BootstrapAbility[] knownAbilities = knownAbilityIds
            .Select(abilityId => ToBootstrapAbility(
                abilityId,
                talentTree,
                activeTalentRanks,
                talentModifiers,
                contentPackage.Abilities ?? []))
            .ToArray();

        ResourceProfile effectiveResourceProfile = CharacterResourceProfileResolver.Resolve(
            resourceProfile,
            contentPackage.ResourceScaling,
            stats,
            talentModifiers.Stats.MaxResourceFlat);

        CharacterVitals vitals = await dbContext.CharacterVitals
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        CharacterLocation location = await dbContext.CharacterLocations
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        LocationDefinition current = worldMap.GetRequired(location.LocationId);

        TimeSpan elapsed = now - vitals.CheckpointedAtUtc;
        TimeSpan contextElapsed = now - vitals.ContextStartedAtUtc;
        decimal currentResource = CharacterResourceRules.ApplyElapsed(
            effectiveResourceProfile,
            vitals.CurrentResource,
            elapsed,
            isInCombat: false,
            contextElapsed);
        decimal currentHp = decimal.Clamp(vitals.CurrentHp, 0, stats.MaxHp);

        if (string.Equals(current.Id, StarterTownId, StringComparison.Ordinal)
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

        BootstrapLocation[] transitions = current.Transitions
            .Select(worldMap.GetRequired)
            .Select(ToLocation)
            .ToArray();

        return new BootstrapSnapshot(
            accountId,
            new BootstrapCharacter(
                character.Id,
                character.Name,
                character.RaceId,
                character.GenderId,
                character.ClassId,
                character.Level,
                character.Experience,
                (contentPackage.LevelProgression
                    ?? throw new InvalidOperationException("Level progression content is required."))
                    .XpToNext(character.Level),
                character.Gold,
                classProfile.PrimaryAttribute,
                contentPackage.BalanceVersion,
                knownAbilityIds,
                knownAbilities,
                stats,
                statCalculation.Breakdown,
                new BootstrapVitals(
                    currentHp,
                    stats.MaxHp,
                    resourceProfile.Id,
                    currentResource,
                    effectiveResourceProfile.MaxValue,
                    checkpoint ? now : vitals.CheckpointedAtUtc),
                inventory),
            new BootstrapWorld(ToLocation(current), location.Version, transitions),
            contentPackage.ContentVersion,
            contentPackage.BalanceVersion,
            now);
    }

    private static BootstrapAbility ToBootstrapAbility(
        string abilityId,
        TalentTreeDefinition? talentTree,
        IReadOnlyDictionary<string, int> activeTalentRanks,
        ResolvedTalentModifiers talentModifiers,
        IReadOnlyList<AbilityDefinition> abilities)
    {
        AbilityDefinition baseDefinition = abilities.Single(ability =>
            string.Equals(ability.Id, abilityId, StringComparison.Ordinal));
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
            location.RecommendedLevel);
}
