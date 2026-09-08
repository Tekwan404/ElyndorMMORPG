namespace Elyndor.Core.Talents;

public static class TalentRuntimeAvailability
{
    private static readonly HashSet<string> StatKeys = new(StringComparer.Ordinal)
    {
        TalentModifierKeys.StrengthPercent,
        TalentModifierKeys.AgilityPercent,
        TalentModifierKeys.IntellectPercent,
        TalentModifierKeys.StaminaPercent,
        TalentModifierKeys.AttackPowerPercent,
        TalentModifierKeys.SpellPowerPercent,
        TalentModifierKeys.ArmorPercent,
        TalentModifierKeys.MagicResistancePercent,
        TalentModifierKeys.AccuracyPercent,
        TalentModifierKeys.DodgePercent,
        TalentModifierKeys.CriticalChancePercent,
        TalentModifierKeys.CriticalDamagePercent,
        TalentModifierKeys.ArmorPenetrationPercent,
        TalentModifierKeys.MagicPenetrationPercent,
        TalentModifierKeys.AttackSpeedPercent,
        TalentModifierKeys.MaxHpPercent,
        TalentModifierKeys.MaxResourceFlat,
        TalentModifierKeys.MaxResourcePercent,
        TalentModifierKeys.IncomingPhysicalDamageReductionPercent,
        TalentModifierKeys.IncomingMagicalDamageReductionPercent,
        TalentModifierKeys.DamageDealtPercent,
        TalentModifierKeys.HealingReceivedPercent,
        TalentModifierKeys.VampirismPercent
    };

    private static readonly HashSet<string> AbilityKeys = new(StringComparer.Ordinal)
    {
        TalentModifierKeys.UnlockAbility,
        TalentModifierKeys.AbilityCooldownSeconds,
        TalentModifierKeys.AbilityResourceCostFlat,
        TalentModifierKeys.AbilityResourceCostPercent,
        TalentModifierKeys.AbilityDamagePercent,
        TalentModifierKeys.AbilityArmorPenetrationPercent,
        TalentModifierKeys.EffectDurationSeconds,
        TalentModifierKeys.EffectMagnitudePercent
    };

    private static readonly HashSet<string> ProfileKeys = new(StringComparer.Ordinal)
    {
        TalentModifierKeys.ResourceProfileOverride,
        TalentModifierKeys.CompanionProfileOverride,
        TalentModifierKeys.PrimaryAttributeOverride
    };

    public static bool IsModifierSupported(
        TalentDefinition node,
        TalentModifierDefinition modifier)
    {
        if (modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred)
        {
            return BerserkerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
                || PyromancerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
                || MageTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
                || ArcherTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
                || GuardianTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
                || WarlordTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier);
        }

        if (modifier.RuntimeStatus != TalentModifierRuntimeStatus.Supported)
            return false;

        return modifier.Type switch
        {
            TalentModifierType.StatModifier or TalentModifierType.ResourceModifier =>
                StatKeys.Contains(modifier.Key),
            TalentModifierType.AbilityModifier =>
                AbilityKeys.Contains(modifier.Key)
                && !string.IsNullOrWhiteSpace(modifier.TargetId),
            TalentModifierType.ProfileModifier =>
                ProfileKeys.Contains(modifier.Key)
                && !string.IsNullOrWhiteSpace(modifier.TargetId),
            TalentModifierType.EquipmentConditional =>
                modifier.Key == TalentModifierKeys.EquipmentConditional
                && !string.IsNullOrWhiteSpace(modifier.TargetId),
            TalentModifierType.EventTriggered =>
                BerserkerTalentRuntimeCatalog.SupportsRuntime(node, modifier)
                || PyromancerTalentRuntimeCatalog.SupportsRuntime(node, modifier)
                || MageTalentRuntimeCatalog.SupportsRuntime(node, modifier)
                || ArcherTalentRuntimeCatalog.SupportsRuntime(node, modifier)
                || GuardianTalentRuntimeCatalog.SupportsRuntime(node, modifier)
                || WarlordTalentRuntimeCatalog.SupportsRuntime(node, modifier),
            _ => false
        };
    }

    public static bool IsNodeFullySupported(TalentDefinition node) =>
        (node.Modifiers ?? []).All(modifier =>
            IsModifierSupported(node, modifier));

    public static string RuntimeStatus(TalentDefinition node)
    {
        IReadOnlyList<TalentModifierDefinition> modifiers = node.Modifiers ?? [];
        bool supported = modifiers.Any(modifier =>
            IsModifierSupported(node, modifier));
        bool deferred = modifiers.Any(modifier =>
            !IsModifierSupported(node, modifier));

        return (supported, deferred) switch
        {
            (true, true) => "PARTIAL",
            (true, false) => "SUPPORTED",
            (false, true) => "DEFERRED",
            _ => "SUPPORTED"
        };
    }
}
