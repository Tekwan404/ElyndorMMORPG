namespace Elyndor.Core.Talents;

public static class TalentModifierKeys
{
    public const string StrengthPercent = "STRENGTH_PERCENT";
    public const string StaminaPercent = "STAMINA_PERCENT";
    public const string AttackPowerPercent = "ATTACK_POWER_PERCENT";
    public const string ArmorPercent = "ARMOR_PERCENT";
    public const string MagicResistancePercent = "MAGIC_RESISTANCE_PERCENT";
    public const string AccuracyPercent = "ACCURACY_PERCENT";
    public const string DodgePercent = "DODGE_PERCENT";
    public const string CriticalChancePercent = "CRITICAL_CHANCE_PERCENT";
    public const string CriticalDamagePercent = "CRITICAL_DAMAGE_PERCENT";
    public const string ArmorPenetrationPercent = "ARMOR_PENETRATION_PERCENT";
    public const string AttackSpeedPercent = "ATTACK_SPEED_PERCENT";
    public const string MaxHpPercent = "MAX_HP_PERCENT";
    public const string MaxResourceFlat = "MAX_RESOURCE_FLAT";
    public const string UnlockAbility = "UNLOCK_ABILITY";
    public const string AbilityCooldownSeconds = "ABILITY_COOLDOWN_SECONDS";
    public const string AbilityResourceCostFlat = "ABILITY_RESOURCE_COST_FLAT";
    public const string AbilityResourceCostPercent = "ABILITY_RESOURCE_COST_PERCENT";
    public const string AbilityDamagePercent = "ABILITY_DAMAGE_PERCENT";
    public const string AbilityArmorPenetrationPercent = "ABILITY_ARMOR_PENETRATION_PERCENT";
    public const string EffectDurationSeconds = "EFFECT_DURATION_SECONDS";
    public const string EffectMagnitudePercent = "EFFECT_MAGNITUDE_PERCENT";
    public const string IncomingPhysicalDamageReductionPercent = "INCOMING_PHYSICAL_DAMAGE_REDUCTION_PERCENT";
    public const string IncomingMagicalDamageReductionPercent = "INCOMING_MAGICAL_DAMAGE_REDUCTION_PERCENT";
    public const string DamageDealtPercent = "DAMAGE_DEALT_PERCENT";
    public const string HealingReceivedPercent = "HEALING_RECEIVED_PERCENT";
    public const string VampirismPercent = "VAMPIRISM_PERCENT";
    public const string OnDamageTaken = "ON_DAMAGE_TAKEN";
    public const string OnDodge = "ON_DODGE";
    public const string OnCriticalHit = "ON_CRITICAL_HIT";
    public const string OnEnemyKilled = "ON_ENEMY_KILLED";
    public const string OnAutoAttack = "ON_AUTO_ATTACK";
    public const string OnHpThreshold = "ON_HP_THRESHOLD";
    public const string OnAbilityUsed = "ON_ABILITY_USED";
    public const string OnPartyEvent = "ON_PARTY_EVENT";
    public const string EquipmentConditional = "EQUIPMENT_CONDITIONAL";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        StrengthPercent, StaminaPercent, AttackPowerPercent, ArmorPercent,
        MagicResistancePercent, AccuracyPercent, DodgePercent, CriticalChancePercent,
        CriticalDamagePercent, ArmorPenetrationPercent, AttackSpeedPercent, MaxHpPercent,
        MaxResourceFlat, UnlockAbility, AbilityCooldownSeconds, AbilityResourceCostFlat,
        AbilityResourceCostPercent, AbilityDamagePercent, AbilityArmorPenetrationPercent,
        EffectDurationSeconds, EffectMagnitudePercent, IncomingPhysicalDamageReductionPercent,
        IncomingMagicalDamageReductionPercent, DamageDealtPercent, HealingReceivedPercent,
        VampirismPercent, OnDamageTaken, OnDodge, OnCriticalHit, OnEnemyKilled,
        OnAutoAttack, OnHpThreshold, OnAbilityUsed, OnPartyEvent, EquipmentConditional
    };
}

public static class TalentRuntimeOwners
{
    public const string CombatSession = "COMBAT_SESSION";
    public const string Party = "PARTY";
    public const string Monster = "MONSTER";
    public const string BossElite = "BOSS_ELITE";
    public const string Equipment = "EQUIPMENT";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CombatSession, Party, Monster, BossElite, Equipment
    };
}
