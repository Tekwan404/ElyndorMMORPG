using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string ArcaneBurstId = "ARCANE_BURST";
    private const string ManaOverloadId = "MANA_OVERLOAD";
    private const string ArcaneCascadeId = "ARCANE_CASCADE";
    private const string ArcaneSealId = "ARCANE_SEAL";
    private const string IceLanceId = "ICE_LANCE";
    private const string IceFractureId = "ICE_FRACTURE";
    private const string HeartOfWinterId = "HEART_OF_WINTER";
    private const string FrostSealId = "FROST_SEAL";

    private const string ArcaneChargeEffectId = "MAGE_ARCANE_CHARGE";
    private const string ManaOverloadEffectId = "MAGE_MANA_OVERLOAD";
    private const string ArcaneResidualEffectId = "MAGE_ARCANE_RESIDUAL";
    private const string ArcaneSavedTimeEffectId = "MAGE_ARCANE_SAVED_TIME";
    private const string ArcaneFormulaFractureEffectId = "MAGE_ARCANE_FORMULA_FRACTURE";
    private const string ArcaneReadyEffectId = "MAGE_RESONANCE_ARCANE_READY";
    private const string ElementalReadyEffectId = "MAGE_RESONANCE_ELEMENTAL_READY";
    private const string ArcaneEchoEffectId = "MAGE_ARCANE_ECHO";
    private const string ArchmageEchoEffectId = "MAGE_ARCHMAGE_ECHO";
    private const string ArcaneOverflowShieldEffectId = "MAGE_ARCANE_OVERFLOW_SHIELD";

    private const string FrostbiteEffectId = "MAGE_FROSTBITE";
    private const string FrostbiteAttackSpeedEffectId = "MAGE_FROSTBITE_ATTACK_SPEED";
    private const string FrostbiteAccuracyEffectId = "MAGE_FROSTBITE_ACCURACY";
    private const string FrostbitePressureEffectId = "MAGE_FROSTBITE_PRESSURE";
    private const string BrittleEffectId = "MAGE_BRITTLE";
    private const string CrystalShieldEffectId = "MAGE_CRYSTAL_SHIELD";
    private const string FrostCleanSnowEffectId = "MAGE_CLEAN_SNOW";
    private const string FrostResponseEffectId = "MAGE_FROST_RESPONSE";
    private const string FrostCrackedIceEffectId = "MAGE_CRACKED_ICE";
    private const string FrostSequenceEffectId = "MAGE_COLD_SEQUENCE";
    private const string HeartOfWinterEffectId = "MAGE_HEART_OF_WINTER";
    private const string FrostSealAttackSpeedEffectId = "MAGE_FROST_SEAL_ATTACK_SPEED";
    private const string FrostControlDefenseEffectId = "MAGE_CONTROL_DEFENSE";
    private const string FrostEmergencyShieldEffectId = "MAGE_EMERGENCY_ICE";

    private int _frostSequence;
    private bool _frostEmergencyShieldUsed;
    private readonly Dictionary<Guid, DateTimeOffset> _deepFreezeReadyAt = [];

    private AbilityDefinition ResolveMageAbility(
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (!IsMage || !ability.IsSpell)
            return ability;

        ability = ResolveArcaneMageAbility(ability, now);
        ability = ResolveFrostMageAbility(ability, now);
        return ability;
    }

    private AbilityDefinition ResolveArcaneMageAbility(
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        decimal resourceCost = ability.ResourceCost;
        decimal damageMultiplier = ability.DamageMultiplier;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal magicPenetrationBonus = ability.MagicPenetrationBonus;
        TimeSpan castTime = ability.CastTime;
        TimeSpan cooldown = ability.Cooldown;
        IReadOnlyList<AbilityActionDefinition>? actions = ability.Actions;

        if (TryGetMageHook("A-1-4", out ResolvedTalentEventHook economy))
            resourceCost *= Math.Max(0, 1 - economy.Value / 100m);

        decimal manaPercent = ResourcePercent();
        if (TryGetMageHook("A-2-3", out ResolvedTalentEventHook conduit)
            && manaPercent > conduit.Threshold)
        {
            magicPenetrationBonus += conduit.Value / 100m;
        }
        if (TryGetMageHook("A-4-3", out ResolvedTalentEventHook overflow)
            && manaPercent > overflow.Threshold)
        {
            actions = ScaleSpellPower(actions, 1 + overflow.Value / 100m);
        }

        if (IsManaOverloadActive(now)
            && TryGetMageHook("A-5-1", out ResolvedTalentEventHook overload))
        {
            actions = ScaleSpellPower(actions, 1 + overload.Value / 100m);
            magicPenetrationBonus += overload.SecondaryValue / 100m;
            resourceCost *= 1.10m;
        }

        ActiveEffect? savedTime = FindOwnEffect(
            _player.Actor,
            ArcaneSavedTimeEffectId,
            now);
        if (savedTime is not null && ability.Type == AbilityType.Casted)
            castTime = ClampCastTime(
                castTime - TimeSpan.FromSeconds((double)savedTime.Definition.Magnitude));

        if (HasOwnEffect(_player.Actor, ArcaneReadyEffectId, now)
            && string.Equals(ability.School, "ARCANE", StringComparison.Ordinal)
            && TryGetMageHook("A-7-3", out ResolvedTalentEventHook resonanceArcane))
        {
            damageMultiplier *= 1 + resonanceArcane.Value / 100m;
        }
        if (HasOwnEffect(_player.Actor, ElementalReadyEffectId, now)
            && ability.School is "FIRE" or "FROST"
            && TryGetMageHook("A-7-3", out ResolvedTalentEventHook resonanceElemental))
        {
            damageMultiplier *= 1 + resonanceElemental.SecondaryValue / 100m;
        }

        int charges = ArcaneCharges(now);
        if (string.Equals(ability.Id, ArcaneSparkId, StringComparison.Ordinal))
        {
            if (TryGetMageHook("A-2-2", out ResolvedTalentEventHook storedPower)
                && charges > 0)
            {
                damageMultiplier *= 1 + charges * storedPower.Value / 100m;
            }

            ActiveEffect? residual = FindOwnEffect(
                _player.Actor,
                ArcaneResidualEffectId,
                now);
            if (residual is not null)
            {
                damageMultiplier *= 1 + residual.Definition.Magnitude / 100m;
                resourceCost *= Math.Max(
                    0,
                    1 - residual.RemainingMagnitude / 100m);
            }
        }
        else if (string.Equals(ability.Id, ArcaneBurstId, StringComparison.Ordinal))
        {
            actions = AddSpellPowerCoefficient(actions, charges * 0.35m);
            if (charges >= 4
                && TryGetMageHook("A-3-2", out ResolvedTalentEventHook empowered))
                criticalChanceBonus += empowered.Value;
            if (charges >= 4
                && TryGetMageHook("A-5-2", out ResolvedTalentEventHook controlled))
                damageMultiplier *= 1 + controlled.Value / 100m;
            if (TryGetMageHook("A-7-1", out ResolvedTalentEventHook perfect))
            {
                damageMultiplier *= 1 + perfect.Value / 100m;
                castTime = ClampCastTime(
                    castTime - TimeSpan.FromSeconds((double)perfect.SecondaryValue));
            }
            if (charges >= 4
                && TryGetMageHook("A-8-1", out ResolvedTalentEventHook absolute))
            {
                criticalChanceBonus += absolute.Value;
                actions = SetDamageCanMiss(actions, false);
            }
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            Cooldown = cooldown < TimeSpan.Zero ? TimeSpan.Zero : cooldown,
            CastTime = castTime,
            Actions = actions,
            DamageMultiplier = damageMultiplier,
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            MagicPenetrationBonus = magicPenetrationBonus
        };
    }

    private AbilityDefinition ResolveFrostMageAbility(
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (!string.Equals(ability.School, "FROST", StringComparison.Ordinal))
        {
            if (HasOwnEffect(_player.Actor, FrostCleanSnowEffectId, now))
            {
                ActiveEffect? cleanSnow =
                    FindOwnEffect(_player.Actor, FrostCleanSnowEffectId, now);
                if (cleanSnow is not null)
                    return ability with
                    {
                        AccuracyBonus = ability.AccuracyBonus
                            + cleanSnow.Definition.Magnitude
                    };
            }
            return ability;
        }

        decimal resourceCost = ability.ResourceCost;
        decimal damageMultiplier = ability.DamageMultiplier;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal magicPenetrationBonus = ability.MagicPenetrationBonus;
        TimeSpan castTime = ability.CastTime;
        TimeSpan cooldown = ability.Cooldown;

        if (TryGetMageHook("I-1-1", out ResolvedTalentEventHook precision))
            accuracyBonus += precision.Value;
        if (TryGetMageHook("I-1-2", out ResolvedTalentEventHook sharpIce))
            criticalChanceBonus += sharpIce.Value;
        if (TryGetMageHook("I-1-4", out ResolvedTalentEventHook coldEconomy))
            resourceCost *= Math.Max(0, 1 - coldEconomy.Value / 100m);
        if (TryGetMageHook("I-3-4", out ResolvedTalentEventHook frostPen))
            magicPenetrationBonus += frostPen.Value / 100m;
        if (TryGetMageHook("I-9-1", out ResolvedTalentEventHook lordOfFrost))
            damageMultiplier *= 1 + lordOfFrost.Value / 100m;

        ActiveEffect? cleanSnowEffect =
            FindOwnEffect(_player.Actor, FrostCleanSnowEffectId, now);
        if (cleanSnowEffect is not null)
            accuracyBonus += cleanSnowEffect.Definition.Magnitude;

        ActiveEffect? frostResponse =
            FindOwnEffect(_player.Actor, FrostResponseEffectId, now);
        if (frostResponse is not null)
            damageMultiplier *= 1 + frostResponse.Definition.Magnitude / 100m;

        ActiveEffect? sequence =
            FindOwnEffect(_player.Actor, FrostSequenceEffectId, now);
        if (sequence is not null)
        {
            damageMultiplier *= 1 + sequence.Definition.Magnitude / 100m;
            resourceCost *= Math.Max(0, 1 - sequence.RemainingMagnitude / 100m);
        }

        bool heart = IsHeartOfWinterActive(now);
        if (heart && TryGetMageHook("I-5-1", out ResolvedTalentEventHook winter))
        {
            damageMultiplier *= 1 + winter.Value / 100m;
            resourceCost *= Math.Max(0, 1 - winter.SecondaryValue / 100m);
            if (string.Equals(ability.Id, IceShardId, StringComparison.Ordinal))
                castTime = ClampCastTime(
                    castTime - TimeSpan.FromSeconds((double)winter.CastTimeSeconds));
        }

        if (heart
            && TryGetMageHook("I-8-2", out ResolvedTalentEventHook perfectHeart))
        {
            criticalChanceBonus += perfectHeart.Value;
            if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal))
            {
                cooldown = TimeSpan.FromSeconds(
                    cooldown.TotalSeconds
                    / (1 + (double)perfectHeart.SecondaryValue / 100d));
            }
        }

        if (string.Equals(ability.Id, IceShardId, StringComparison.Ordinal))
        {
            if (TryGetMageHook("I-4-4", out ResolvedTalentEventHook steadyCold))
                castTime = ClampCastTime(
                    castTime - TimeSpan.FromSeconds((double)steadyCold.Value));

            ActiveEffect? cracked =
                FindOwnEffect(_player.Actor, FrostCrackedIceEffectId, now);
            if (cracked is not null)
                criticalChanceBonus += cracked.Definition.Magnitude;

            if (heart
                && TryGetMageHook("I-9-1", out _)
                && SelectedTargetFrostbiteStacks(now) >= 3)
            {
                castTime = ClampCastTime(castTime - TimeSpan.FromSeconds(0.15));
            }
        }
        else if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal)
            && TryGetMageHook("I-7-1", out ResolvedTalentEventHook perfectLance))
        {
            cooldown -= TimeSpan.FromSeconds((double)perfectLance.SecondaryValue);
            if (cooldown < TimeSpan.Zero) cooldown = TimeSpan.Zero;
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            Cooldown = cooldown,
            CastTime = castTime,
            DamageMultiplier = damageMultiplier,
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            MagicPenetrationBonus = magicPenetrationBonus
        };
    }

    private AbilityTargetModifier ResolveMageTargetAbilityModifier(
        AbilityDefinition ability,
        CombatActorState target,
        AbilityTargetModifier modifier,
        DateTimeOffset now)
    {
        if (!IsMage || target.IsDead)
            return modifier;

        decimal damageMultiplier = modifier.DamageMultiplier;
        decimal accuracyBonus = modifier.AccuracyBonus;
        decimal criticalChanceBonus = modifier.CriticalChanceBonus;
        decimal criticalDamageBonus = modifier.CriticalDamageBonus;
        decimal magicPenetrationBonus = modifier.MagicPenetrationBonus;

        if (HasOwnEffect(target, ArcaneFormulaFractureEffectId, now)
            && TryGetMageHook("A-5-4", out ResolvedTalentEventHook fracture))
        {
            magicPenetrationBonus += fracture.Value / 100m;
        }

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal))
        {
            int stacks = FrostbiteStacks(target, now);
            if (stacks >= 3
                && TryGetMageHook("I-2-2", out ResolvedTalentEventHook iceCrack))
                damageMultiplier *= 1 + iceCrack.Value / 100m;

            ActiveEffect? brittle = FindOwnEffect(target, BrittleEffectId, now);
            if (brittle is not null)
                criticalDamageBonus += brittle.Definition.Magnitude;

            if (TryGetMageHook("I-5-4", out ResolvedTalentEventHook coldAim)
                && HpPercent(target) < coldAim.Threshold)
            {
                accuracyBonus += coldAim.Value;
                criticalChanceBonus += coldAim.Value;
            }

            if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal))
            {
                if (stacks >= 4 && TryGetMageHook("I-9-1", out ResolvedTalentEventHook lord))
                {
                    damageMultiplier *= 1 + lord.SecondaryValue / 100m;
                }
                else if (stacks >= 3)
                {
                    decimal bonus = 25;
                    if (TryGetMageHook("I-7-1", out ResolvedTalentEventHook perfectLance))
                        bonus = perfectLance.Value;
                    damageMultiplier *= 1 + bonus / 100m;
                }

                if (brittle is not null
                    && TryGetMageHook("I-5-2", out ResolvedTalentEventHook shattered))
                    damageMultiplier *= 1 + shattered.Value / 100m;
            }
        }

        return modifier with
        {
            DamageMultiplier = damageMultiplier,
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            CriticalDamageBonus = criticalDamageBonus,
            MagicPenetrationBonus = magicPenetrationBonus
        };
    }

    private void OnMageAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsMage || !ability.IsSpell) return;

        if (ability.Type == AbilityType.Casted)
            RemoveMageEffect(_player.Actor, ArcaneSavedTimeEffectId, now);

        if (string.Equals(ability.Id, ArcaneSparkId, StringComparison.Ordinal))
            RemoveMageEffect(_player.Actor, ArcaneResidualEffectId, now);

        RemoveMageEffect(_player.Actor, FrostCleanSnowEffectId, now);

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal))
        {
            RemoveMageEffect(_player.Actor, FrostResponseEffectId, now);
            RemoveMageEffect(_player.Actor, FrostSequenceEffectId, now);
            if (string.Equals(ability.Id, IceShardId, StringComparison.Ordinal))
                RemoveMageEffect(_player.Actor, FrostCrackedIceEffectId, now);
        }

        if (string.Equals(ability.School, "ARCANE", StringComparison.Ordinal))
            RemoveMageEffect(_player.Actor, ArcaneReadyEffectId, now);
        else if (ability.School is "FIRE" or "FROST")
            RemoveMageEffect(_player.Actor, ElementalReadyEffectId, now);
    }

    private void OnMageAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (!IsMage || Status != CombatSessionStatus.Active || !ability.IsSpell)
            return;

        bool hit = DidHit(execution);
        bool critical = DidCrit(execution);

        if (string.Equals(ability.Id, ManaOverloadId, StringComparison.Ordinal))
        {
            ActivateManaOverload(now);
            return;
        }
        if (string.Equals(ability.Id, HeartOfWinterId, StringComparison.Ordinal))
        {
            ActivateHeartOfWinter(now);
            return;
        }

        ApplyArcaneResolvedHooks(ability, execution, hit, critical, now);
        ApplyFrostResolvedHooks(ability, execution, hit, critical, now);

        if (hit && TryGetMageHook("A-7-3", out ResolvedTalentEventHook resonance))
        {
            if (string.Equals(ability.School, "ARCANE", StringComparison.Ordinal))
            {
                ApplyMageEffect(
                    _player.Actor,
                    new EffectDefinition(
                        ElementalReadyEffectId,
                        EffectKind.Buff,
                        resonance.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        resonance.SecondaryValue),
                    now);
            }
            else if (ability.School is "FIRE" or "FROST")
            {
                ApplyMageEffect(
                    _player.Actor,
                    new EffectDefinition(
                        ArcaneReadyEffectId,
                        EffectKind.Buff,
                        resonance.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        resonance.Value),
                    now);
            }
        }

        if (ability.Type == AbilityType.Casted
            && TryGetMageHook("A-2-4", out ResolvedTalentEventHook weaving)
            && TalentCooldownReady(weaving.TalentId, now))
        {
            RestoreMageResource(weaving.Value, now, weaving.TalentId);
            StartTalentCooldown(weaving, now);
        }

        if (IsOffensiveMageAbility(ability)
            && !hit
            && ability.ResourceCost > 0
            && TryGetMageHook("A-4-4", out ResolvedTalentEventHook calculation))
        {
            RestoreMageResource(
                ability.ResourceCost * calculation.Value / 100m,
                now,
                calculation.TalentId);
        }

        TryApplySpellEcho(ability, execution, hit, now);
    }

    private void ApplyArcaneResolvedHooks(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        bool hit,
        bool critical,
        DateTimeOffset now)
    {
        if (string.Equals(ability.Id, ArcaneSparkId, StringComparison.Ordinal)
            && hit
            && TryGetMageHook("A-2-1", out ResolvedTalentEventHook chargeHook))
        {
            int count = IsManaOverloadActive(now) ? 2 : 1;
            AddArcaneCharges(count, chargeHook.Duration, now);
        }

        if (string.Equals(ability.Id, ArcaneCascadeId, StringComparison.Ordinal))
        {
            ConsumeArcaneCharges(2, now);
        }

        if (string.Equals(ability.Id, ArcaneSealId, StringComparison.Ordinal)
            && hit)
        {
            foreach (CombatActorState sealedTarget in HitTargets(execution))
            {
                ApplyMageEffect(
                    sealedTarget,
                    new EffectDefinition(
                        "MAGE_ARCANE_SEAL_SILENCE",
                        EffectKind.Silence,
                        TimeSpan.FromSeconds(2),
                        1,
                        EffectStackPolicy.Replace,
                        0,
                        SourceSpecific: true),
                    now);
            }
        }

        if (!string.Equals(ability.Id, ArcaneBurstId, StringComparison.Ordinal))
            return;

        int consumed = ArcaneCharges(now);
        if (consumed <= 0) return;

        decimal directDamage = execution.Events
            .Where(item => item.Type == CombatEventType.DamageDealt)
            .Sum(item => item.Amount);
        CombatActorState? target = ResolveExecutionEnemyTarget(execution);

        ConsumeArcaneCharges(consumed, now);

        if (consumed >= 3
            && TryGetMageHook("A-3-4", out ResolvedTalentEventHook residual))
        {
            ApplyMageEffect(
                _player.Actor,
                new EffectDefinition(
                    ArcaneResidualEffectId,
                    EffectKind.Buff,
                    residual.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    residual.Value),
                now);
            ActiveEffect? effect = FindOwnEffect(
                _player.Actor,
                ArcaneResidualEffectId,
                now);
            if (effect is not null)
                effect.RemainingMagnitude = residual.SecondaryValue;
        }

        if (TryGetMageHook("A-5-3", out ResolvedTalentEventHook energyReturn))
            RestoreMageResource(consumed * energyReturn.Value, now, energyReturn.TalentId);

        if (TryGetMageHook("A-6-2", out ResolvedTalentEventHook savedTime))
        {
            ApplyMageEffect(
                _player.Actor,
                new EffectDefinition(
                    ArcaneSavedTimeEffectId,
                    EffectKind.Buff,
                    savedTime.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    savedTime.Value),
                now);
        }

        if (critical && consumed >= 4
            && TryGetMageHook("A-8-2", out ResolvedTalentEventHook chain))
            AddArcaneCharges((int)chain.Value, TimeSpan.FromSeconds(12), now);

        if (consumed >= 5
            && directDamage > 0
            && target is not null
            && TryGetMageHook("A-9-1", out ResolvedTalentEventHook archmage))
        {
            ApplyDelayedMageDamage(
                target,
                ArchmageEchoEffectId,
                directDamage * archmage.Value / 100m,
                archmage.Duration,
                now);
        }
    }

    private void ApplyFrostResolvedHooks(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        bool hit,
        bool critical,
        DateTimeOffset now)
    {
        if (!string.Equals(ability.School, "FROST", StringComparison.Ordinal))
            return;

        CombatActorState? primaryTarget = ResolveExecutionEnemyTarget(execution);

        if (string.Equals(ability.Id, IceShardId, StringComparison.Ordinal)
            && hit
            && primaryTarget is not null)
        {
            AddFrostbite(primaryTarget, now);
            if (TryGetMageHook("I-2-4", out ResolvedTalentEventHook cleanSnow))
            {
                ApplyMageEffect(
                    _player.Actor,
                    new EffectDefinition(
                        FrostCleanSnowEffectId,
                        EffectKind.Buff,
                        cleanSnow.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        cleanSnow.Value),
                    now);
            }

            if (FrostbiteStacks(primaryTarget, now) >= 3
                && TryGetMageHook("I-6-3", out ResolvedTalentEventHook cracked))
            {
                ApplyMageEffect(
                    _player.Actor,
                    new EffectDefinition(
                        FrostCrackedIceEffectId,
                        EffectKind.Buff,
                        cracked.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        cracked.Value),
                    now);
            }
        }

        if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal)
            && hit
            && primaryTarget is not null)
        {
            int stacks = FrostbiteStacks(primaryTarget, now);
            if (stacks >= 4 && TryGetMageHook("I-9-1", out ResolvedTalentEventHook lord))
            {
                ConsumeFrostbite(primaryTarget, 4, now);
                if (!_deepFreezeReadyAt.TryGetValue(primaryTarget.ActorId, out DateTimeOffset readyAt)
                    || readyAt <= now)
                {
                    ApplyMageEffect(
                        primaryTarget,
                        new EffectDefinition(
                            "MAGE_DEEP_FREEZE_STUN",
                            EffectKind.Stun,
                            lord.Duration,
                            1,
                            EffectStackPolicy.Replace,
                            0,
                            SourceSpecific: true),
                        now);
                    _deepFreezeReadyAt[primaryTarget.ActorId] = now + lord.InternalCooldown;
                }
            }
            else if (stacks >= 3)
            {
                ConsumeFrostbite(primaryTarget, 1, now);
            }
        }

        if (string.Equals(ability.Id, IceFractureId, StringComparison.Ordinal)
            && hit)
        {
            foreach (CombatActorState target in HitTargets(execution))
            {
                if (FrostbiteStacks(target, now) < 3) continue;

                ApplyMageEffect(
                    target,
                    new EffectDefinition(
                        "MAGE_ICE_FRACTURE_STUN",
                        EffectKind.Stun,
                        TimeSpan.FromSeconds(1),
                        1,
                        EffectStackPolicy.Replace,
                        0,
                        SourceSpecific: true),
                    now);

                if (TryGetMageHook("I-8-1", out ResolvedTalentEventHook absolute))
                {
                    ApplyBrittle(
                        target,
                        TryGetMageHook("I-3-2", out ResolvedTalentEventHook brittle)
                            ? brittle.Value
                            : 0,
                        absolute.Duration,
                        now);
                }
                ConsumeFrostbite(target, 3, now);
            }
        }

        if (string.Equals(ability.Id, FrostSealId, StringComparison.Ordinal)
            && hit
            && primaryTarget is not null)
        {
            ApplyMageEffect(
                primaryTarget,
                new EffectDefinition(
                    "MAGE_FROST_SEAL_SILENCE",
                    EffectKind.Silence,
                    TimeSpan.FromSeconds(2),
                    1,
                    EffectStackPolicy.Replace,
                    0,
                    SourceSpecific: true),
                now);
            if (TryGetMageHook("I-7-4", out ResolvedTalentEventHook whiteSilence))
            {
                ApplyMageEffect(
                    primaryTarget,
                    new EffectDefinition(
                        FrostSealAttackSpeedEffectId,
                        EffectKind.StatModifier,
                        whiteSilence.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        1 - whiteSilence.Value / 100m,
                        ModifiedStat: EffectStat.AttackSpeed,
                        ModifierMode: EffectModifierMode.Multiplicative,
                        SourceSpecific: true),
                    now);
            }
        }

        if (hit)
        {
            _frostSequence++;
            if (TryGetMageHook("I-7-3", out ResolvedTalentEventHook sequence)
                && _frostSequence >= Math.Max(1, sequence.TriggerCount))
            {
                _frostSequence = 0;
                ApplyMageEffect(
                    _player.Actor,
                    new EffectDefinition(
                        FrostSequenceEffectId,
                        EffectKind.Buff,
                        sequence.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        sequence.Value),
                    now);
                ActiveEffect? effect = FindOwnEffect(
                    _player.Actor,
                    FrostSequenceEffectId,
                    now);
                if (effect is not null)
                    effect.RemainingMagnitude = sequence.SecondaryValue;
            }
        }
        else
        {
            _frostSequence = 0;
            RemoveMageEffect(_player.Actor, FrostSequenceEffectId, now);
        }
    }

    private void ApplyMageCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsMage
            || combatEvent.SourceActorId != _player.Actor.ActorId
            || combatEvent.DefinitionId is null
            || !_abilities.TryGetValue(
                combatEvent.DefinitionId,
                out AbilityDefinition? ability)
            || !ability.IsSpell)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        CombatActorState? target =
            combatEvent.TargetActorId is { } targetId
            && _enemiesById.TryGetValue(targetId, out CombatParticipantDefinition? enemy)
                ? enemy.Actor
                : null;

        if (target is not null
            && TryGetMageHook("A-5-4", out ResolvedTalentEventHook fracture))
        {
            ApplyMageEffect(
                target,
                new EffectDefinition(
                    ArcaneFormulaFractureEffectId,
                    EffectKind.Debuff,
                    fracture.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    fracture.Value,
                    SourceSpecific: true),
                now);
        }

        if (!string.Equals(ability.School, "FROST", StringComparison.Ordinal))
            return;

        if (target is not null
            && TryGetMageHook("I-3-2", out ResolvedTalentEventHook brittle))
            ApplyBrittle(target, brittle.Value, brittle.Duration, now);

        if (TryGetMageHook("I-5-3", out ResolvedTalentEventHook economy)
            && TalentCooldownReady(economy.TalentId, now))
        {
            RestoreMageResource(economy.Value, now, economy.TalentId);
            StartTalentCooldown(economy, now);
        }
    }

    private void ApplyMageIncomingCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsMage
            || _player.Actor.IsDead
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || !TryGetMageHook("I-2-3", out ResolvedTalentEventHook response)
            || !TalentCooldownReady(response.TalentId, combatEvent.OccurredAtUtc))
            return;

        decimal percent = response.Value;
        TimeSpan duration = response.Duration;
        if (TryGetMageHook("I-6-2", out ResolvedTalentEventHook upgraded))
        {
            percent = upgraded.Value;
            duration += upgraded.Duration;
        }

        ApplyMageShield(
            CrystalShieldEffectId,
            _player.Actor.MaxHp * percent / 100m,
            duration,
            combatEvent.OccurredAtUtc,
            frostShield: true);
        StartTalentCooldown(response, combatEvent.OccurredAtUtc);
    }

    private void ApplyMageDamageTakenHooks(CombatEvent combatEvent)
    {
        if (!IsMage
            || _player.Actor.IsDead
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.Amount <= 0)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        if (!_frostEmergencyShieldUsed
            && TryGetMageHook("I-8-3", out ResolvedTalentEventHook emergency)
            && HpPercent(_player.Actor) < emergency.Threshold)
        {
            _frostEmergencyShieldUsed = true;
            ApplyMageShield(
                FrostEmergencyShieldEffectId,
                _player.Actor.MaxHp * emergency.Value / 100m,
                emergency.Duration,
                now,
                frostShield: true);
        }
    }

    private void ApplyMageShieldAbsorbedHooks(CombatEvent combatEvent)
    {
        if (!IsMage
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.Amount <= 0
            || !TryGetMageHook("I-4-3", out ResolvedTalentEventHook response)
            || !TalentCooldownReady(response.TalentId, combatEvent.OccurredAtUtc))
            return;

        ApplyMageEffect(
            _player.Actor,
            new EffectDefinition(
                FrostResponseEffectId,
                EffectKind.Buff,
                response.Duration,
                1,
                EffectStackPolicy.Replace,
                response.Value),
            combatEvent.OccurredAtUtc);
        StartTalentCooldown(response, combatEvent.OccurredAtUtc);
    }

    private void ApplyMageResourceThresholdHooks(CombatEvent combatEvent)
    {
        if (!IsMage
            || combatEvent.ActorId != _player.Actor.ActorId
            || combatEvent.Amount >= 0
            || !TryGetMageHook("A-7-4", out ResolvedTalentEventHook reserve)
            || ResourcePercent() >= reserve.Threshold
            || !TalentCooldownReady(reserve.TalentId, combatEvent.OccurredAtUtc))
            return;

        RestoreMageResource(
            _player.Actor.MaxResource * reserve.Value / 100m,
            combatEvent.OccurredAtUtc,
            reserve.TalentId);
        StartTalentCooldown(reserve, combatEvent.OccurredAtUtc);
    }

    private void OnMageAbilityInterrupted(CombatEvent combatEvent)
    {
        if (!IsMage || combatEvent.ActorId != _player.Actor.ActorId) return;

        if (combatEvent.DefinitionId is not null
            && _abilities.TryGetValue(combatEvent.DefinitionId, out AbilityDefinition? ability)
            && string.Equals(ability.School, "FROST", StringComparison.Ordinal))
        {
            _frostSequence = 0;
            RemoveMageEffect(_player.Actor, FrostSequenceEffectId, combatEvent.OccurredAtUtc);
        }
    }

    private void ActivateManaOverload(DateTimeOffset now)
    {
        if (!TryGetMageHook("A-5-1", out ResolvedTalentEventHook overload)) return;

        ApplyMageEffect(
            _player.Actor,
            new EffectDefinition(
                ManaOverloadEffectId,
                EffectKind.Buff,
                overload.Duration,
                1,
                EffectStackPolicy.Replace,
                0),
            now);

        if (TryGetMageHook("A-8-3", out ResolvedTalentEventHook perfect))
        {
            RestoreMageResource(
                _player.Actor.MaxResource * perfect.Value / 100m,
                now,
                perfect.TalentId);
            _playerRuntime.Cooldowns.Remove(ArcaneBurstId);
        }
    }

    private void ActivateHeartOfWinter(DateTimeOffset now)
    {
        if (!TryGetMageHook("I-5-1", out ResolvedTalentEventHook winter)) return;

        ApplyMageEffect(
            _player.Actor,
            new EffectDefinition(
                HeartOfWinterEffectId,
                EffectKind.Buff,
                winter.Duration,
                1,
                EffectStackPolicy.Replace,
                0),
            now);
    }

    private void TryApplySpellEcho(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        bool hit,
        DateTimeOffset now)
    {
        if (!hit
            || ability.Type != AbilityType.Casted
            || !TryGetMageHook("A-4-1", out ResolvedTalentEventHook echo))
            return;

        decimal chance = echo.SecondaryValue;
        ResolvedTalentEventHook? overloadEchoHook = null;
        if (IsManaOverloadActive(now)
            && TryGetMageHook("A-7-2", out ResolvedTalentEventHook resolvedOverloadEcho))
        {
            overloadEchoHook = resolvedOverloadEcho;
            if (!TalentCooldownReady(overloadEchoHook.TalentId, now))
                return;
            chance *= 2;
        }

        if (_random.NextUnit() >= chance / 100m) return;

        foreach (IGrouping<Guid?, CombatEvent> group in execution.Events
                     .Where(item => item.Type == CombatEventType.DamageDealt
                         && item.TargetActorId.HasValue
                         && item.Amount > 0)
                     .GroupBy(item => item.TargetActorId))
        {
            if (group.Key is not { } targetId
                || !_enemiesById.TryGetValue(targetId, out CombatParticipantDefinition? enemy)
                || enemy.Actor.IsDead)
                continue;

            decimal amount = group.Sum(item => item.Amount) * echo.Value / 100m;
            ApplyDelayedMageDamage(
                enemy.Actor,
                ArcaneEchoEffectId,
                amount,
                echo.Duration,
                now);
        }

        if (overloadEchoHook is not null)
            StartTalentCooldown(overloadEchoHook, now);
    }

    private void AddArcaneCharges(int count, TimeSpan duration, DateTimeOffset now)
    {
        int maxStacks = HasMageTalent("A-9-1") ? 5 : 4;
        for (var i = 0; i < count; i++)
        {
            ApplyMageEffect(
                _player.Actor,
                new EffectDefinition(
                    ArcaneChargeEffectId,
                    EffectKind.Buff,
                    duration,
                    maxStacks,
                    EffectStackPolicy.Stack,
                    0),
                now);
        }
    }

    private void ConsumeArcaneCharges(int count, DateTimeOffset now)
    {
        ActiveEffect? charge = FindOwnEffect(_player.Actor, ArcaneChargeEffectId, now);
        if (charge is null) return;
        charge.Stacks = Math.Max(0, charge.Stacks - count);
        if (charge.Stacks == 0)
            RemoveMageEffect(_player.Actor, ArcaneChargeEffectId, now);
    }

    private int ArcaneCharges(DateTimeOffset now) =>
        FindOwnEffect(_player.Actor, ArcaneChargeEffectId, now)?.Stacks ?? 0;

    private bool IsArcaneCascadeAvailable(DateTimeOffset now) =>
        HasMageTalent("A-6-1") && ArcaneCharges(now) >= 4;

    private void AddFrostbite(CombatActorState target, DateTimeOffset now)
    {
        if (!TryGetMageHook("I-2-1", out ResolvedTalentEventHook frostbite))
            return;

        int maxStacks = HasMageTalent("I-9-1") ? 4 : 3;
        ApplyMageEffect(
            target,
            new EffectDefinition(
                FrostbiteEffectId,
                EffectKind.Debuff,
                frostbite.Duration,
                maxStacks,
                EffectStackPolicy.Stack,
                0,
                SourceSpecific: true),
            now);
        SyncFrostbiteDebuffs(target, now);
    }

    private void ConsumeFrostbite(
        CombatActorState target,
        int count,
        DateTimeOffset now)
    {
        ActiveEffect? frostbite = FindOwnEffect(target, FrostbiteEffectId, now);
        if (frostbite is null) return;
        frostbite.Stacks = Math.Max(0, frostbite.Stacks - count);
        if (frostbite.Stacks == 0)
            RemoveMageEffect(target, FrostbiteEffectId, now);
        SyncFrostbiteDebuffs(target, now);
    }

    private void SyncFrostbiteDebuffs(CombatActorState target, DateTimeOffset now)
    {
        RemoveMageEffect(target, FrostbiteAttackSpeedEffectId, now);
        RemoveMageEffect(target, FrostbiteAccuracyEffectId, now);
        RemoveMageEffect(target, FrostbitePressureEffectId, now);

        ActiveEffect? frostbite = FindOwnEffect(target, FrostbiteEffectId, now);
        if (frostbite is null) return;

        int effectiveStacks = Math.Min(3, frostbite.Stacks);
        TimeSpan remaining = frostbite.ExpiresAtUtc - now;
        if (remaining <= TimeSpan.Zero) return;

        if (TryGetMageHook("I-2-1", out ResolvedTalentEventHook slow))
        {
            ApplyMageEffect(
                target,
                new EffectDefinition(
                    FrostbiteAttackSpeedEffectId,
                    EffectKind.StatModifier,
                    remaining,
                    1,
                    EffectStackPolicy.Replace,
                    Math.Max(0.1m, 1 - slow.Value * effectiveStacks / 100m),
                    ModifiedStat: EffectStat.AttackSpeed,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true),
                now);
        }

        if (TryGetMageHook("I-3-3", out ResolvedTalentEventHook noise))
        {
            ApplyMageEffect(
                target,
                new EffectDefinition(
                    FrostbiteAccuracyEffectId,
                    EffectKind.StatModifier,
                    remaining,
                    1,
                    EffectStackPolicy.Replace,
                    Math.Max(0.1m, 1 - noise.Value * effectiveStacks / 100m),
                    ModifiedStat: EffectStat.Accuracy,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true),
                now);
        }

        if (frostbite.Stacks >= 3
            && TryGetMageHook("I-7-2", out ResolvedTalentEventHook pressure))
        {
            ApplyMageEffect(
                target,
                new EffectDefinition(
                    FrostbitePressureEffectId,
                    EffectKind.StatModifier,
                    remaining,
                    1,
                    EffectStackPolicy.Replace,
                    Math.Max(0.1m, 1 - pressure.Value / 100m),
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true),
                now);
        }
    }

    private int FrostbiteStacks(CombatActorState target, DateTimeOffset now) =>
        FindOwnEffect(target, FrostbiteEffectId, now)?.Stacks ?? 0;

    private int SelectedTargetFrostbiteStacks(DateTimeOffset now) =>
        _enemiesById.TryGetValue(
            _selectedTargetActorId,
            out CombatParticipantDefinition? target)
            ? FrostbiteStacks(target.Actor, now)
            : 0;

    private void ApplyBrittle(
        CombatActorState target,
        decimal magnitude,
        TimeSpan duration,
        DateTimeOffset now)
    {
        if (magnitude <= 0 || duration <= TimeSpan.Zero) return;
        ApplyMageEffect(
            target,
            new EffectDefinition(
                BrittleEffectId,
                EffectKind.Debuff,
                duration,
                1,
                EffectStackPolicy.Replace,
                magnitude,
                SourceSpecific: true),
            now);
    }

    private void ApplyMageShield(
        string effectId,
        decimal amount,
        TimeSpan duration,
        DateTimeOffset now,
        bool frostShield)
    {
        if (amount <= 0 || duration <= TimeSpan.Zero) return;
        if (frostShield
            && TryGetMageHook("I-4-2", out ResolvedTalentEventHook shell))
            amount *= 1 + shell.Value / 100m;

        ApplyMageEffect(
            _player.Actor,
            new EffectDefinition(
                effectId,
                EffectKind.Shield,
                duration,
                1,
                EffectStackPolicy.Replace,
                amount),
            now);
    }

    private void RestoreMageResource(
        decimal amount,
        DateTimeOffset now,
        string definitionId)
    {
        if (amount <= 0) return;
        decimal overflow = Math.Max(
            0,
            _player.Actor.CurrentResource + amount - _player.Actor.MaxResource);
        AddResource(_player.Actor, amount, now, definitionId);

        if (overflow <= 0
            || !TryGetMageHook("A-6-3", out ResolvedTalentEventHook shield)
            || !TalentCooldownReady(shield.TalentId, now))
            return;

        decimal absorb = Math.Min(
            _player.Actor.MaxHp * shield.Threshold / 100m,
            overflow * shield.Value / 100m);
        if (absorb <= 0) return;

        ApplyMageShield(
            ArcaneOverflowShieldEffectId,
            absorb,
            shield.Duration,
            now,
            frostShield: false);
        StartTalentCooldown(shield, now);
    }

    private void ApplyDelayedMageDamage(
        CombatActorState target,
        string effectId,
        decimal amount,
        TimeSpan delay,
        DateTimeOffset now)
    {
        if (target.IsDead || amount <= 0 || delay <= TimeSpan.Zero) return;
        ApplyMageEffect(
            target,
            new EffectDefinition(
                effectId,
                EffectKind.DamageOverTime,
                delay,
                1,
                EffectStackPolicy.Replace,
                amount,
                delay,
                SourceSpecific: true,
                PeriodicDamageType: DamageType.Magical),
            now);
    }

    private IEnumerable<CombatActorState> HitTargets(AbilityExecutionResult execution) =>
        execution.Events
            .Where(item => item.Type == CombatEventType.DamageDealt
                && item.TargetActorId.HasValue
                && item.Amount > 0)
            .Select(item => item.TargetActorId!.Value)
            .Distinct()
            .Where(_enemiesById.ContainsKey)
            .Select(id => _enemiesById[id].Actor);

    private void SyncMageConditionalEffects(DateTimeOffset now)
    {
        if (!IsMage) return;

        bool controlled = EffectEngine.HasControl(_player.Actor, EffectKind.Stun, now)
            || EffectEngine.HasControl(_player.Actor, EffectKind.Silence, now);
        if (controlled
            && TryGetMageHook("I-6-4", out ResolvedTalentEventHook calm))
        {
            if (!HasOwnEffect(_player.Actor, FrostControlDefenseEffectId, now))
            {
                ApplyMageEffect(
                    _player.Actor,
                    new EffectDefinition(
                        FrostControlDefenseEffectId,
                        EffectKind.StatModifier,
                        TimeSpan.FromHours(12),
                        1,
                        EffectStackPolicy.Replace,
                        Math.Max(0.1m, 1 - calm.Value / 100m),
                        ModifiedStat: EffectStat.IncomingMagicalDamageMultiplier,
                        ModifierMode: EffectModifierMode.Multiplicative),
                    now);
            }
        }
        else
        {
            RemoveMageEffect(_player.Actor, FrostControlDefenseEffectId, now);
        }
    }

    private decimal EffectivePlayerResourceRegenPerSecond(DateTimeOffset now)
    {
        decimal regen = _player.ResourceRegenPerSecond;
        if (!IsMage || regen <= 0) return regen;

        if (TryGetMageHook("A-4-2", out ResolvedTalentEventHook manaCycle)
            && ResourcePercent() < manaCycle.Threshold)
            regen *= 1 + manaCycle.Value / 100m;
        return regen;
    }

    private decimal ResourcePercent() =>
        _player.Actor.MaxResource <= 0
            ? 0
            : _player.Actor.CurrentResource / _player.Actor.MaxResource * 100m;

    private bool IsManaOverloadActive(DateTimeOffset now) =>
        HasOwnEffect(_player.Actor, ManaOverloadEffectId, now);

    private bool IsHeartOfWinterActive(DateTimeOffset now) =>
        HasOwnEffect(_player.Actor, HeartOfWinterEffectId, now);

    private bool HasMageTalent(string talentId) =>
        _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal))
        || talentId switch
        {
            "A-6-1" => _playerTalents.UnlockedAbilityIds.Contains(ArcaneCascadeId),
            _ => false
        };

    private bool TryGetMageHook(
        string talentId,
        out ResolvedTalentEventHook hook)
    {
        hook = _playerTalents.EventHooks.FirstOrDefault(item =>
            string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!;
        return hook is not null;
    }

    private void ApplyMageEffect(
        CombatActorState target,
        EffectDefinition effect,
        DateTimeOffset now) =>
        ApplyTalentEffect(target, _player.Actor.ActorId, effect, now);

    private void RemoveMageEffect(
        CombatActorState target,
        string effectId,
        DateTimeOffset now) =>
        RemoveTalentEffects(EffectEngine.Remove(target, effectId, now));

    private static AbilityActionDefinition[]? AddSpellPowerCoefficient(
        IReadOnlyList<AbilityActionDefinition>? actions,
        decimal coefficient) =>
        actions?.Select(action =>
            action.Type == AbilityActionType.Damage
                ? action with
                {
                    SpellPowerCoefficient =
                        action.SpellPowerCoefficient + coefficient
                }
                : action).ToArray();

    private static AbilityActionDefinition[]? SetDamageCanMiss(
        IReadOnlyList<AbilityActionDefinition>? actions,
        bool canMiss) =>
        actions?.Select(action =>
            action.Type == AbilityActionType.Damage
                ? action with { CanMiss = canMiss }
                : action).ToArray();

    private static bool IsOffensiveMageAbility(AbilityDefinition ability) =>
        ability.IsSpell
        && ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            && action.DamageType == DamageType.Magical) == true;
}
