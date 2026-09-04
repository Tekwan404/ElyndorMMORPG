namespace Elyndor.Core.Talents;

public sealed record TalentStatModifiers(
    decimal StrengthPercent = 0,
    decimal StaminaPercent = 0,
    decimal AttackPowerPercent = 0,
    decimal ArmorPercent = 0,
    decimal MagicResistancePercent = 0,
    decimal AccuracyPercent = 0,
    decimal DodgePercent = 0,
    decimal CriticalChancePercent = 0,
    decimal CriticalDamagePercent = 0,
    decimal ArmorPenetrationPercent = 0,
    decimal AttackSpeedPercent = 0,
    decimal MaxHpPercent = 0,
    decimal MaxResourceFlat = 0);

public sealed record TalentAbilityModifiers(
    decimal ResourceCostFlatReduction = 0,
    decimal ResourceCostPercentReduction = 0,
    decimal CooldownSecondsReduction = 0,
    decimal DamagePercentBonus = 0,
    decimal ArmorPenetrationPercent = 0,
    decimal EffectDurationSecondsBonus = 0,
    decimal EffectMagnitudePercentBonus = 0);

public sealed record TalentCombatModifiers(
    decimal IncomingPhysicalDamageReductionPercent = 0,
    decimal IncomingMagicalDamageReductionPercent = 0,
    decimal DamageDealtPercent = 0,
    decimal HealingReceivedPercent = 0,
    decimal VampirismPercent = 0);

public sealed record ResolvedTalentEventHook(
    string TalentId,
    string Key,
    int Rank,
    decimal Value,
    string? TargetId,
    TimeSpan InternalCooldown,
    bool CanTriggerFromProc,
    decimal SecondaryValue = 0,
    decimal Threshold = 0,
    decimal ChancePercent = 100,
    TimeSpan Duration = default,
    TimeSpan TickInterval = default,
    int TriggerCount = 0,
    decimal CastTimeSeconds = 0,
    decimal ResourceCostReductionPercent = 0);

public sealed record ResolvedTalentModifiers(
    TalentStatModifiers Stats,
    TalentCombatModifiers Combat,
    IReadOnlySet<string> UnlockedAbilityIds,
    IReadOnlyDictionary<string, TalentAbilityModifiers> Abilities,
    IReadOnlyList<ResolvedTalentEventHook> EventHooks,
    IReadOnlyList<TalentModifierDefinition> DeferredHooks)
{
    public static ResolvedTalentModifiers Empty { get; } = new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        [],
        []);
}
