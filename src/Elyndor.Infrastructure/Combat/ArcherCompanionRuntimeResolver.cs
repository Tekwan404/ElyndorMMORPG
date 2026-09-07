using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.Infrastructure.Combat;

internal static class ArcherCompanionRuntimeResolver
{
    public static CombatParticipantDefinition Resolve(
        CompanionProfileDefinition profile,
        CharacterStats ownerStats,
        int ownerLevel,
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(ownerStats);
        ArgumentNullException.ThrowIfNull(talents);

        bool physical = string.Equals(profile.Tag, "PHYSICAL_PET", StringComparison.Ordinal);
        bool spirit = string.Equals(profile.Tag, "SPIRIT_PET", StringComparison.Ordinal);

        decimal maxHpPercent = 0;
        decimal damagePercent = 0;
        decimal armorPercent = 0;
        decimal magicResistancePercent = 0;
        decimal criticalChanceBonus = 0;
        decimal criticalDamageBonus = 0;
        decimal spellScalingPercent = 0;

        if (physical)
        {
            Add("M-2-3", secondaryForHp: true);
            Add("B-1-1", valueForHp: true);
            Add("B-1-2", valueForDamage: true);
            Add("B-2-2", valueForArmor: true, secondaryForMagicResistance: true);
            Add("B-8-1", valueForCritChance: true, secondaryForCritDamage: true);
            Add("B-9-1", valueForDamage: true, secondaryForHp: true);
        }
        else if (spirit)
        {
            Add("A-2-2", valueForDamage: true, secondaryForHp: true);
            Add("A-8-2", valueForSpellScaling: true, secondaryForCritChance: true);
        }

        decimal maxHp = (
            profile.MaxHpBase
            + ownerStats.Stamina * profile.MaxHpPerOwnerStamina)
            * (1 + maxHpPercent / 100m);
        decimal attackPower =
            ownerStats.AttackPower * profile.AttackPowerOwnerCoefficient;
        decimal spellPower =
            ownerStats.SpellPower
            * profile.SpellPowerOwnerCoefficient
            * (1 + spellScalingPercent / 100m);

        CombatStats stats = new(
            ownerLevel,
            profile.Accuracy,
            profile.Dodge,
            profile.CriticalChance + criticalChanceBonus,
            profile.CriticalDamage + criticalDamageBonus / 100m,
            profile.Armor * (1 + armorPercent / 100m),
            profile.MagicResistance * (1 + magicResistancePercent / 100m),
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: attackPower,
            SpellPower: spellPower);

        CombatActorState actor = new(
            Guid.CreateVersion7(),
            Math.Max(1, maxHp),
            Math.Max(1, maxHp),
            0,
            0,
            stats,
            new TalentCombatModifiers(DamageDealtPercent: damagePercent));

        AutoAttackProfile autoAttack = new(
            profile.AutoAttackInterval,
            BaseDamage: 0,
            AttackPowerCoefficient: physical ? 1 : 0,
            ResourceOnHit: 0,
            BaseDamageMin: profile.BaseDamageMin,
            BaseDamageMax: profile.BaseDamageMax,
            DamageType: spirit ? DamageType.Magical : DamageType.Physical,
            SpellPowerCoefficient: spirit ? 1 : 0);

        return new CombatParticipantDefinition(
            actor,
            CombatActorKind.Companion,
            profile.Id,
            profile.Name,
            "NONE",
            autoAttack,
            new HashSet<string>(StringComparer.Ordinal));

        void Add(
            string talentId,
            bool valueForHp = false,
            bool secondaryForHp = false,
            bool valueForDamage = false,
            bool valueForArmor = false,
            bool secondaryForMagicResistance = false,
            bool valueForCritChance = false,
            bool secondaryForCritChance = false,
            bool secondaryForCritDamage = false,
            bool valueForSpellScaling = false)
        {
            ResolvedTalentEventHook? hook = talents.EventHooks.FirstOrDefault(item =>
                string.Equals(item.TalentId, talentId, StringComparison.Ordinal));
            if (hook is null)
                return;

            if (valueForHp) maxHpPercent += hook.Value;
            if (secondaryForHp) maxHpPercent += hook.SecondaryValue;
            if (valueForDamage) damagePercent += hook.Value;
            if (valueForArmor) armorPercent += hook.Value;
            if (secondaryForMagicResistance) magicResistancePercent += hook.SecondaryValue;
            if (valueForCritChance) criticalChanceBonus += hook.Value;
            if (secondaryForCritChance) criticalChanceBonus += hook.SecondaryValue;
            if (secondaryForCritDamage) criticalDamageBonus += hook.SecondaryValue;
            if (valueForSpellScaling) spellScalingPercent += hook.Value;
        }
    }
}
