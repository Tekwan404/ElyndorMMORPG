using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Characters;

public sealed record CharacterDerivedState(
    ClassProfile ClassProfile,
    ResourceProfile BaseResourceProfile,
    ResourceProfile EffectiveResourceProfile,
    InventorySnapshot Inventory,
    EquipmentModifierSummary Equipment,
    TalentTreeDefinition? TalentTree,
    IReadOnlyDictionary<string, int> ActiveTalentRanks,
    ResolvedTalentModifiers TalentModifiers,
    CharacterStatCalculation StatCalculation,
    IReadOnlyList<string> KnownAbilityIds)
{
    public CharacterStats Stats => StatCalculation.Stats;
}

public sealed class CharacterDerivedStateService(
    GameDbContext dbContext,
    GameContentPackage content,
    InventoryEquipmentService inventoryService)
{
    public async Task<CharacterDerivedState> ResolveAsync(
        Guid characterId,
        string classId,
        int level,
        CancellationToken cancellationToken)
    {
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        ClassProfile classProfile = (content.ClassProfiles
            ?? throw new InvalidOperationException("Class profiles are required."))
            .Single(profile => string.Equals(profile.Id, classId, StringComparison.Ordinal));
        ResourceProfile baseResourceProfile = (content.ResourceProfiles
            ?? throw new InvalidOperationException("Resource profiles are required."))
            .Single(profile => string.Equals(
                profile.Id,
                classProfile.ResourceProfileId,
                StringComparison.Ordinal));

        InventorySnapshot inventory = await ResolveInventoryAsync(characterId, cancellationToken);
        EquipmentModifierSummary equipment = EquipmentStatModifierResolver.ResolveDetailed(
            inventory.Equipped.Values.Select(item => item.Definition),
            content.EquipmentSets ?? []);

        TalentTreeDefinition? talentTree = content.TalentTrees?.SingleOrDefault(tree =>
            string.Equals(tree.ClassId, classId, StringComparison.Ordinal));
        CharacterTalentState? talentState = talentTree is null
            ? null
            : await dbContext.CharacterTalentStates
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.CharacterId == characterId,
                    cancellationToken);

        IReadOnlyDictionary<string, int> activeTalentRanks =
            talentTree is not null
            && talentState is not null
            && string.Equals(talentState.TalentTreeId, talentTree.Id, StringComparison.Ordinal)
                ? talentState.GetRanks(talentState.ActiveLoadoutId)
                : new Dictionary<string, int>(StringComparer.Ordinal);

        ResolvedTalentModifiers talentModifiers = talentTree is not null
            && activeTalentRanks.Count > 0
                ? TalentModifierResolver.Resolve(talentTree, activeTalentRanks)
                : ResolvedTalentModifiers.Empty;

        TalentPrimaryStatPercentages talentPercentages = new(
            talentModifiers.Stats.StrengthPercent,
            0,
            0,
            talentModifiers.Stats.StaminaPercent);

        CharacterStatCalculation statCalculation = new CharacterStatCalculator(
            content.StatFormula
                ?? throw new InvalidOperationException("Stat formula content is required."),
            content.ClassProfiles).CalculateDetailed(
                classId,
                level,
                CharacterStatInputs.Empty with
                {
                    Equipment = equipment.PrimaryStats,
                    EquipmentDerived = new CharacterEquipmentDerivedModifiers(
                        equipment.AttackSpeedPercent,
                        equipment.DodgePercent),
                    TalentPercentages = talentPercentages,
                    TalentDerived = talentModifiers.Stats
                });

        ResourceProfile effectiveResourceProfile = CharacterResourceProfileResolver.Resolve(
            baseResourceProfile,
            content.ResourceScaling,
            statCalculation.Stats,
            talentModifiers.Stats.MaxResourceFlat);

        string[] knownAbilityIds = (classProfile.StartingAbilityIds ?? [])
            .Concat(talentModifiers.UnlockedAbilityIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(abilityId => abilityId, StringComparer.Ordinal)
            .ToArray();

        return new CharacterDerivedState(
            classProfile,
            baseResourceProfile,
            effectiveResourceProfile,
            inventory,
            equipment,
            talentTree,
            activeTalentRanks,
            talentModifiers,
            statCalculation,
            knownAbilityIds);
    }

    private async Task<InventorySnapshot> ResolveInventoryAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        if (content.Items is not null)
            return await inventoryService.GetForCharacterAsync(characterId, cancellationToken);

        bool hasPersistedInventory = await dbContext.CharacterItems
            .AsNoTracking()
            .AnyAsync(item => item.CharacterId == characterId, cancellationToken);
        bool hasPersistedEquipment = await dbContext.CharacterEquipment
            .AsNoTracking()
            .AnyAsync(item => item.CharacterId == characterId, cancellationToken);
        if (hasPersistedInventory || hasPersistedEquipment)
        {
            throw new InvalidOperationException(
                "Persisted inventory exists but item definitions are missing from game content.");
        }

        return new InventorySnapshot(
            [],
            new Dictionary<EquipmentSlot, InventoryItemSnapshot>());
    }
}
