using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string ArcaneMissilesId = "MAGE_ARCANE_MISSILES";
    private const string ArcaneExplosionId = "MAGE_ARCANE_EXPLOSION";
    private const string ManaShieldId = "MAGE_MANA_SHIELD";
    private const string CounterspellId = "MAGE_COUNTERSPELL";
    private const string PresenceOfMindId = "MAGE_PRESENCE_OF_MIND";
    private const string ArcanePowerId = "MAGE_ARCANE_POWER";
    private const string EvocationId = "MAGE_EVOCATION";

    private const string FrostNovaId = "MAGE_FROST_NOVA";
    private const string BlizzardId = "MAGE_BLIZZARD";
    private const string ColdSnapId = "MAGE_COLD_SNAP";
    private const string IceBlockId = "MAGE_ICE_BLOCK";
    private const string ConeOfColdId = "MAGE_CONE_OF_COLD";
    private const string IceBarrierId = "MAGE_ICE_BARRIER";
    private const string IceLanceId = "MAGE_ICE_LANCE";

    private const string ClearcastingEffectId = "MAGE_CLEARCASTING";
    private const string ClearcastingRegenEffectId = "MAGE_CLEARCASTING_REGEN";
    private const string PresenceOfMindEffectId = "MAGE_PRESENCE_OF_MIND_ACTIVE";
    private const string ArcanePowerEffectId = "MAGE_ARCANE_POWER_ACTIVE";
    private const string ArcanePowerFreeCostEffectId = "MAGE_ARCANE_POWER_FREE_COST";
    private const string ArcaneFortitudeEffectId = "MAGE_ARCANE_FORTITUDE";
    private const string ManaShieldEffectId = "MAGE_MANA_SHIELD_EFFECT";
    private const string ManaShieldEfficiencyEffectId = "MAGE_MANA_SHIELD_EFFICIENCY";
    private const string ArcaneEchoEffectId = "MAGE_ARCANE_ECHO";

    private const string ChillEffectId = "MAGE_CHILL";
    private const string FreezeEffectId = "MAGE_FREEZE";
    private const string DeepChillEffectId = "MAGE_DEEP_CHILL";
    private const string WinterChillEffectId = "MAGE_WINTERS_CHILL";
    private const string FrostExtendedEffectId = "MAGE_FROST_EXTENDED";
    private const string IceBlockEffectId = "MAGE_ICE_BLOCK_ACTIVE";
    private const string IceBlockImmunityEffectId = "MAGE_ICE_BLOCK_IMMUNITY";
    private const string ColdBloodEffectId = "MAGE_COLD_BLOOD";
    private const string IceBarrierEffectId = "MAGE_ICE_BARRIER_EFFECT";
    private const string FrostArmorEffectId = "MAGE_FROST_ARMOR";
    private const string EmergencyIceEffectId = "MAGE_EMERGENCY_ICE";
    private const string ColdSnapLanceEffectId = "MAGE_COLD_SNAP_LANCE";

    private DateTimeOffset? _lastMageManaSpendAtUtc;
    private DateTimeOffset? _coldBloodReadyAtUtc;
    private int _arcanePowerManaSpendCount;
    private readonly Dictionary<Guid, DateTimeOffset> _deepFreezeReadyAt = [];
    private readonly List<PendingMageResourceRefund> _pendingMageResourceRefunds = [];

    private sealed record PendingMageResourceRefund(
        DateTimeOffset DueAtUtc,
        decimal Amount,
        string TalentId);

    private AbilityDefinition ResolveMageAbility(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsMage || !ability.IsSpell)
            return ability;

        ability = ResolveArcaneMageAbility(ability, now);
        ability = ResolveFrostMageAbility(ability, now);
        return ability;
    }

    private AbilityDefinition ResolveArcaneMageAbility(AbilityDefinition ability, DateTimeOffset now)
    {
        decimal resourceCost = ability.ResourceCost;
        decimal damageMultiplier = ability.DamageMultiplier;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal magicPenetrationBonus = ability.MagicPenetrationBonus;
        TimeSpan castTime = ability.CastTime;
        TimeSpan cooldown = ability.Cooldown;
        IReadOnlyList<AbilityActionDefinition>? actions = ability.Actions;

        if (string.Equals(ability.School, "ARCANE", StringComparison.Ordinal)
            && TryGetMageHook("A-1-1", out ResolvedTalentEventHook focus))
            accuracyBonus += focus.Value;

        if (TryGetMageHook("A-4-3", out ResolvedTalentEventHook instability))
        {
            damageMultiplier *= 1 + instability.Value / 100m;
            criticalChanceBonus += instability.SecondaryValue;
        }

        if (TryGetMageHook("A-5-3", out ResolvedTalentEventHook overflow)
            && ResourcePercent() > overflow.Threshold)
            actions = ScaleSpellPower(actions, 1 + overflow.Value / 100m);

        bool clearcasting = ability.ResourceCost > 0
            && HasOwnEffect(_player.Actor, ClearcastingEffectId, now);
        if (clearcasting)
        {
            resourceCost = 0;
            if (TryGetMageHook("A-5-2", out ResolvedTalentEventHook potency))
                criticalChanceBonus += potency.Value;
            if (TryGetMageHook("A-7-3", out ResolvedTalentEventHook perfectClarity))
            {
                criticalChanceBonus += perfectClarity.Value;
                damageMultiplier *= 1 + perfectClarity.SecondaryValue / 100m;
            }
        }

        bool presenceApplies = HasOwnEffect(_player.Actor, PresenceOfMindEffectId, now)
            && ability.Type == AbilityType.Casted
            && ability.CastTime > TimeSpan.Zero
            && ability.CastTime <= TimeSpan.FromSeconds(3);
        if (presenceApplies)
        {
            castTime = TimeSpan.Zero;
            if (TryGetMageHook("A-6-1", out ResolvedTalentEventHook mindPower))
                damageMultiplier *= 1 + mindPower.Value / 100m;
            if (TryGetMageHook("A-8-2", out ResolvedTalentEventHook absolutePresence))
                resourceCost *= Math.Max(0, 1 - absolutePresence.Value / 100m);
            if (IsArcanePowerActive(now) && HasMageTalent("A-9-1"))
                damageMultiplier *= 1.10m;
        }

        if (string.Equals(ability.Id, ArcaneMissilesId, StringComparison.Ordinal)
            && TryGetMageHook("A-2-3", out ResolvedTalentEventHook improvedMissiles))
        {
            damageMultiplier *= 1 + improvedMissiles.Value / 100m;
            resourceCost *= Math.Max(0, 1 - improvedMissiles.SecondaryValue / 100m);
        }
        else if (string.Equals(ability.Id, ArcaneExplosionId, StringComparison.Ordinal)
            && TryGetMageHook("A-3-1", out ResolvedTalentEventHook improvedExplosion))
        {
            resourceCost *= Math.Max(0, 1 - improvedExplosion.Value / 100m);
            damageMultiplier *= 1 + improvedExplosion.SecondaryValue / 100m;
        }
        else if (string.Equals(ability.Id, PresenceOfMindId, StringComparison.Ordinal)
            && TryGetMageHook("A-7-2", out ResolvedTalentEventHook quickThinking))
        {
            cooldown = TimeSpan.FromSeconds(Math.Max(0, cooldown.TotalSeconds - (double)quickThinking.Value));
        }

        if (IsArcanePowerActive(now) && IsOffensiveMageAbility(ability))
        {
            damageMultiplier *= 1.20m;
            ActiveEffect? freeCost = FindOwnEffect(_player.Actor, ArcanePowerFreeCostEffectId, now);
            if (freeCost is null)
            {
                decimal extraCostPercent = 20m;
                if (TryGetMageHook("A-7-1", out ResolvedTalentEventHook controlPower))
                    extraCostPercent = controlPower.Value;
                resourceCost *= 1 + extraCostPercent / 100m;
            }
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            DamageMultiplier = damageMultiplier,
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            MagicPenetrationBonus = magicPenetrationBonus,
            CastTime = castTime,
            Cooldown = cooldown,
            Actions = actions
        };
    }

    private AbilityDefinition ResolveFrostMageAbility(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!string.Equals(ability.School, "FROST", StringComparison.Ordinal))
            return ability;

        decimal resourceCost = ability.ResourceCost;
        decimal damageMultiplier = ability.DamageMultiplier;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal criticalDamageBonus = ability.CriticalDamageBonus;
        TimeSpan castTime = ability.CastTime;
        TimeSpan cooldown = ability.Cooldown;

        if (TryGetMageHook("I-1-2", out ResolvedTalentEventHook precision))
        {
            accuracyBonus += precision.Value;
            resourceCost *= Math.Max(0, 1 - precision.SecondaryValue / 100m);
        }
        if (TryGetMageHook("I-1-3", out ResolvedTalentEventHook piercing))
            damageMultiplier *= 1 + piercing.Value / 100m;
        if (TryGetMageHook("I-1-4", out ResolvedTalentEventHook iceShards))
            criticalDamageBonus += iceShards.Value;
        if (TryGetMageHook("I-3-4", out ResolvedTalentEventHook focusedIce))
            resourceCost *= Math.Max(0, 1 - focusedIce.Value / 100m);
        if (TryGetMageHook("I-9-1", out ResolvedTalentEventHook lordOfCold))
            damageMultiplier *= 1 + lordOfCold.Value / 100m;

        if (string.Equals(ability.Id, IceShardId, StringComparison.Ordinal)
            && TryGetMageHook("I-1-1", out ResolvedTalentEventHook improvedShard))
            castTime = ClampCastTime(castTime - TimeSpan.FromSeconds((double)improvedShard.Value));

        if (string.Equals(ability.Id, BlizzardId, StringComparison.Ordinal)
            && TryGetMageHook("I-3-3", out ResolvedTalentEventHook improvedBlizzard))
            damageMultiplier *= 1 + improvedBlizzard.Value / 100m;

        if (string.Equals(ability.Id, ConeOfColdId, StringComparison.Ordinal)
            && TryGetMageHook("I-4-4", out ResolvedTalentEventHook improvedCone))
            damageMultiplier *= 1 + improvedCone.Value / 100m;

        if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal)
            && TryGetMageHook("I-6-2", out ResolvedTalentEventHook perfectLance))
            cooldown = TimeSpan.FromSeconds(Math.Max(0, cooldown.TotalSeconds - (double)perfectLance.Value));

        ActiveEffect? coldBlood = FindOwnEffect(_player.Actor, ColdBloodEffectId, now);
        if (coldBlood is not null)
        {
            criticalChanceBonus += coldBlood.Definition.Magnitude;
            resourceCost *= Math.Max(0, 1 - coldBlood.RemainingMagnitude / 100m);
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            DamageMultiplier = damageMultiplier,
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            CriticalDamageBonus = criticalDamageBonus,
            CastTime = castTime,
            Cooldown = cooldown
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
        decimal criticalChanceBonus = modifier.CriticalChanceBonus;

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal))
        {
            bool frozen = HasOwnEffect(target, FreezeEffectId, now);
            bool deepChill = HasOwnEffect(target, DeepChillEffectId, now);

            if ((frozen || deepChill) && TryGetMageHook("I-3-1", out ResolvedTalentEventHook shatter))
                criticalChanceBonus += frozen ? shatter.Value : shatter.SecondaryValue;

            if (deepChill && TryGetMageHook("I-8-2", out ResolvedTalentEventHook absoluteZero))
                criticalChanceBonus += absoluteZero.Value;

            if ((frozen || deepChill) && TryGetMageHook("I-7-2", out ResolvedTalentEventHook glassIce))
                damageMultiplier *= 1 + glassIce.Value / 100m;

            ActiveEffect? winter = FindAnyActiveEffect(target, WinterChillEffectId, now);
            if (winter is not null)
            {
                criticalChanceBonus += winter.Stacks;
                if (winter.Stacks >= 5 && HasMageTalent("I-8-1"))
                    damageMultiplier *= 1.05m;
            }

            if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal))
            {
                if (frozen)
                    damageMultiplier *= 3m;
                else if (deepChill)
                    damageMultiplier *= 1.75m;

                if ((frozen || deepChill)
                    && TryGetMageHook("I-6-2", out ResolvedTalentEventHook perfectLance))
                    criticalChanceBonus += perfectLance.SecondaryValue;

                if ((frozen || deepChill)
                    && HasOwnEffect(_player.Actor, ColdSnapLanceEffectId, now))
                    criticalChanceBonus += 100m;
            }
        }

        return modifier with
        {
            DamageMultiplier = damageMultiplier,
            CriticalChanceBonus = criticalChanceBonus
        };
    }

    private void OnMageAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsMage || !ability.IsSpell) return;

        bool baseManaSpell = _abilities.TryGetValue(ability.Id, out AbilityDefinition? baseAbility)
            ? baseAbility.ResourceCost > 0
            : ability.ResourceCost > 0;
        ActiveEffect? clearcasting = FindOwnEffect(_player.Actor, ClearcastingEffectId, now);
        if (baseManaSpell && clearcasting is not null)
        {
            ConsumeEffectStack(_player.Actor, clearcasting, ClearcastingEffectId, now);
            if (TryGetMageHook("A-6-2", out ResolvedTalentEventHook afterglow))
                ApplyMageEffect(_player.Actor, new EffectDefinition(
                    ClearcastingRegenEffectId, EffectKind.Buff, afterglow.Duration, 1,
                    EffectStackPolicy.Replace, afterglow.Value), now);
        }

        bool presenceApplies = HasOwnEffect(_player.Actor, PresenceOfMindEffectId, now)
            && ability.Type == AbilityType.Casted
            && ability.CastTime <= TimeSpan.FromSeconds(3);
        if (presenceApplies)
            RemoveMageEffect(_player.Actor, PresenceOfMindEffectId, now);

        ActiveEffect? freeCost = FindOwnEffect(_player.Actor, ArcanePowerFreeCostEffectId, now);
        if (baseManaSpell && freeCost is not null && IsArcanePowerActive(now))
            ConsumeEffectStack(_player.Actor, freeCost, ArcanePowerFreeCostEffectId, now);

        if (ability.ResourceCost > 0)
            _lastMageManaSpendAtUtc = now;

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal)
            && HasOwnEffect(_player.Actor, ColdBloodEffectId, now))
            RemoveMageEffect(_player.Actor, ColdBloodEffectId, now);

        if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal)
            && HasOwnEffect(_player.Actor, ColdSnapLanceEffectId, now))
            RemoveMageEffect(_player.Actor, ColdSnapLanceEffectId, now);
    }

    private void OnMageAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (!IsMage || Status != CombatSessionStatus.Active || !ability.IsSpell)
            return;

        if (string.Equals(ability.Id, PresenceOfMindId, StringComparison.Ordinal))
        {
            ApplyMageEffect(_player.Actor, new EffectDefinition(
                PresenceOfMindEffectId, EffectKind.Buff, TimeSpan.FromSeconds(20), 1,
                EffectStackPolicy.Replace, 0), now);
            return;
        }
        if (string.Equals(ability.Id, ArcanePowerId, StringComparison.Ordinal))
        {
            ActivateArcanePower(now);
            return;
        }
        if (string.Equals(ability.Id, ManaShieldId, StringComparison.Ordinal))
        {
            ActivateManaShield(now);
            return;
        }
        if (string.Equals(ability.Id, EvocationId, StringComparison.Ordinal))
        {
            decimal percent = TryGetMageHook("A-6-4", out ResolvedTalentEventHook evocation)
                ? evocation.Value
                : 40m;
            AddResource(_player.Actor, _player.Actor.MaxResource * percent / 100m, now, "A-6-4");
            return;
        }
        if (string.Equals(ability.Id, CounterspellId, StringComparison.Ordinal))
        {
            ApplyCounterspell(now);
            return;
        }
        if (string.Equals(ability.Id, FrostNovaId, StringComparison.Ordinal))
        {
            ApplyFrostNova(now);
            return;
        }
        if (string.Equals(ability.Id, ColdSnapId, StringComparison.Ordinal))
        {
            ActivateColdSnap(now);
            return;
        }
        if (string.Equals(ability.Id, IceBlockId, StringComparison.Ordinal))
        {
            ActivateIceBlock(now);
            return;
        }
        if (string.Equals(ability.Id, IceBarrierId, StringComparison.Ordinal))
        {
            ActivateIceBarrier(now);
            return;
        }

        bool hit = DidHit(execution);
        bool critical = DidCrit(execution);

        if (hit)
        {
            TryProcClearcasting(now);
            if (IsArcanePowerActive(now))
            {
                TryApplyArcanePowerEcho(ability, execution, now);
                if (ability.ResourceCost > 0 && HasMageTalent("A-9-1"))
                {
                    _arcanePowerManaSpendCount++;
                    if (_arcanePowerManaSpendCount >= 3)
                    {
                        _arcanePowerManaSpendCount = 0;
                        GrantClearcasting(now);
                    }
                }
            }
        }

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal))
            ApplyFrostResolvedHooks(ability, execution, hit, critical, now);
    }

    private void ApplyFrostResolvedHooks(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        bool hit,
        bool critical,
        DateTimeOffset now)
    {
        if (!hit) return;

        CombatActorState[] targets = HitTargets(execution).ToArray();
        bool appliesChill = string.Equals(ability.Id, IceShardId, StringComparison.Ordinal)
            || string.Equals(ability.Id, BlizzardId, StringComparison.Ordinal)
            || string.Equals(ability.Id, ConeOfColdId, StringComparison.Ordinal);

        if (appliesChill)
            foreach (CombatActorState target in targets)
                ApplyChill(target, now, enhanced: string.Equals(ability.Id, ConeOfColdId, StringComparison.Ordinal));

        bool directFrost = !string.Equals(ability.Id, BlizzardId, StringComparison.Ordinal);
        if (directFrost && TryGetMageHook("I-2-2", out ResolvedTalentEventHook frostbite))
        {
            foreach (CombatActorState target in targets)
                if (_random.NextUnit() < frostbite.Value / 100m)
                    ApplyFreezeOrDeepChill(target, frostbite.Duration, TimeSpan.FromSeconds((double)frostbite.SecondaryValue), now);
        }

        if (critical && TryGetMageHook("I-5-1", out ResolvedTalentEventHook winterChill))
        {
            foreach (CombatActorState target in targets)
                ApplyWinterChill(target, (int)Math.Max(1, winterChill.Value), winterChill.Duration, now);
        }

        if (HasMageTalent("I-9-1"))
        {
            foreach (CombatActorState target in targets)
            {
                ActiveEffect? winter = FindAnyActiveEffect(target, WinterChillEffectId, now);
                if (winter is not null)
                    winter.ExpiresAtUtc = now + winter.Definition.Duration;
            }
        }

        if (critical && TryGetMageHook("I-7-4", out ResolvedTalentEventHook economy)
            && TalentCooldownReady(economy.TalentId, now))
        {
            AddResource(_player.Actor, economy.Value, now, economy.TalentId);
            StartTalentCooldown(economy, now);
        }

        if (critical && TryGetMageHook("I-6-3", out ResolvedTalentEventHook boneChill))
        {
            foreach (CombatActorState target in targets)
                ExtendFrozenStateOnce(target, boneChill.Value, now);
        }

        if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal)
            && TryGetMageHook("I-7-1", out ResolvedTalentEventHook deepFreeze))
        {
            foreach (CombatActorState target in targets)
                TryApplyDeepFreeze(target, execution, deepFreeze, now);
        }
    }

    private static void ApplyMageCriticalHooks(CombatEvent combatEvent) { }
    private static void ApplyMageIncomingCriticalHooks(CombatEvent combatEvent) { }
    private static void ApplyMageDamageTakenHooks(CombatEvent combatEvent) { }
    private static void ApplyMageResourceThresholdHooks(CombatEvent combatEvent) { }

    private void ApplyMageShieldAbsorbedHooks(CombatEvent combatEvent)
    {
        if (!IsMage || combatEvent.TargetActorId != _player.Actor.ActorId || combatEvent.Amount <= 0)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        if (string.Equals(combatEvent.DefinitionId, ManaShieldEffectId, StringComparison.Ordinal)
            || HasOwnEffect(_player.Actor, ManaShieldEffectId, now))
        {
            decimal efficiency = TryGetMageHook("A-3-3", out ResolvedTalentEventHook improvedShield)
                ? improvedShield.Value
                : 0m;
            decimal manaSpent = combatEvent.Amount * Math.Max(0, 1 - efficiency / 100m);
            AddResource(_player.Actor, -manaSpent, now, ManaShieldEffectId);

            if (TryGetMageHook("A-5-4", out ResolvedTalentEventHook absorption) && manaSpent > 0)
                _pendingMageResourceRefunds.Add(new(
                    now + absorption.Duration,
                    manaSpent * absorption.Value / 100m,
                    absorption.TalentId));

            if (_player.Actor.CurrentResource <= 0)
                RemoveMageEffect(_player.Actor, ManaShieldEffectId, now);
        }

        if (string.Equals(combatEvent.DefinitionId, IceBarrierEffectId, StringComparison.Ordinal)
            && !HasOwnEffect(_player.Actor, IceBarrierEffectId, now))
            OnIceBarrierBroken(combatEvent, now);
    }

    private static void OnMageAbilityInterrupted(CombatEvent combatEvent) { }

    private void ActivateArcanePower(DateTimeOffset now)
    {
        if (!TryGetMageHook("A-5-1", out ResolvedTalentEventHook arcanePower)) return;
        TimeSpan duration = arcanePower.Duration;
        if (TryGetMageHook("A-8-3", out ResolvedTalentEventHook perfectPower))
            duration += TimeSpan.FromSeconds((double)perfectPower.Value);

        _arcanePowerManaSpendCount = 0;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            ArcanePowerEffectId, EffectKind.Buff, duration, 1,
            EffectStackPolicy.Replace, arcanePower.Value), now);

        if (HasMageTalent("A-8-3"))
            ApplyMageEffect(_player.Actor, new EffectDefinition(
                ArcanePowerFreeCostEffectId, EffectKind.Buff, duration, 2,
                EffectStackPolicy.Stack, 0), now);
    }

    private void ActivateManaShield(DateTimeOffset now)
    {
        decimal absorb = Math.Min(_player.Actor.MaxHp * 0.30m, Math.Max(1, _player.Actor.CurrentResource));
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            ManaShieldEffectId, EffectKind.Shield, TimeSpan.FromSeconds(12), 1,
            EffectStackPolicy.Replace, absorb), now);
    }

    private void ApplyCounterspell(DateTimeOffset now)
    {
        if (!_enemiesById.TryGetValue(_selectedTargetActorId, out CombatParticipantDefinition? target)
            || target.Actor.IsDead)
            return;

        if (_enemyRuntimes.TryGetValue(target.Actor.ActorId, out CombatRuntimeState? runtime))
        {
            AbilityExecutionResult interrupted = AbilityEngine.Interrupt(runtime, now, TimeSpan.Zero);
            if (interrupted.Succeeded)
            {
                ApplyKernelEvents(
                    interrupted.Events,
                    target.Actor.ActorId,
                    _player.Actor.ActorId,
                    CounterspellId);
            }
        }

        if (TryGetMageHook("A-4-1", out ResolvedTalentEventHook improved))
            ApplyMageEffect(target.Actor, new EffectDefinition(
                "MAGE_COUNTERSPELL_SILENCE", EffectKind.Silence,
                TimeSpan.FromSeconds((double)improved.Value), 1,
                EffectStackPolicy.Replace, 0, SourceSpecific: true), now);
    }

    private void TryProcClearcasting(DateTimeOffset now)
    {
        if (!TryGetMageHook("A-1-2", out ResolvedTalentEventHook concentration)) return;
        if (_random.NextUnit() >= concentration.Value / 100m) return;
        GrantClearcasting(now);
    }

    private void GrantClearcasting(DateTimeOffset now)
    {
        int maxStacks = HasMageTalent("A-8-1") ? 2 : 1;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            ClearcastingEffectId, EffectKind.Buff, TimeSpan.FromSeconds(30), maxStacks,
            maxStacks > 1 ? EffectStackPolicy.Stack : EffectStackPolicy.Replace, 0), now);
    }

    private void TryApplyArcanePowerEcho(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (!IsOffensiveMageAbility(ability)
            || !TryGetMageHook("A-7-4", out ResolvedTalentEventHook echo)
            || _random.NextUnit() >= echo.Value / 100m)
            return;

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

            decimal amount = group.Sum(item => item.Amount) * echo.SecondaryValue / 100m;
            ApplyDelayedMageDamage(enemy.Actor, ArcaneEchoEffectId, amount, echo.Duration, now);
        }
    }

    private void ApplyFrostNova(DateTimeOffset now)
    {
        TimeSpan normalDuration = TimeSpan.FromSeconds(2);
        TimeSpan bossDuration = TimeSpan.FromSeconds(4);
        if (TryGetMageHook("I-2-4", out ResolvedTalentEventHook improved))
        {
            bossDuration += TimeSpan.FromSeconds((double)improved.SecondaryValue);
            ReduceCooldown(_playerRuntime, FrostNovaId, TimeSpan.FromSeconds((double)improved.Value), now);
        }

        foreach (CombatParticipantDefinition enemy in _enemiesById.Values.Where(item => !item.Actor.IsDead))
            ApplyFreezeOrDeepChill(enemy.Actor, normalDuration, bossDuration, now);
    }

    private void ActivateColdSnap(DateTimeOffset now)
    {
        foreach (string id in new[] { FrostNovaId, BlizzardId, ConeOfColdId, IceBlockId })
            _playerRuntime.Cooldowns.Remove(id);

        if (HasMageTalent("I-9-1"))
        {
            _playerRuntime.Cooldowns.Remove(IceLanceId);
            _playerRuntime.Cooldowns.Remove(IceBarrierId);
            ApplyMageEffect(_player.Actor, new EffectDefinition(
                ColdSnapLanceEffectId, EffectKind.Buff, TimeSpan.FromSeconds(20), 1,
                EffectStackPolicy.Replace, 0), now);
        }
    }

    private void ActivateIceBlock(DateTimeOffset now)
    {
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            IceBlockEffectId, EffectKind.Stun, TimeSpan.FromSeconds(3), 1,
            EffectStackPolicy.Replace, 0), now);
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            IceBlockImmunityEffectId, EffectKind.StatModifier, TimeSpan.FromSeconds(3), 1,
            EffectStackPolicy.Replace, 0,
            ModifiedStat: EffectStat.IncomingDamageMultiplier,
            ModifierMode: EffectModifierMode.Multiplicative), now);

        foreach (ActiveEffect effect in _player.Actor.ActiveEffects
                     .Where(effect =>
                         !string.Equals(effect.Definition.Id, IceBlockEffectId, StringComparison.Ordinal)
                         && effect.Definition.Kind is EffectKind.Debuff or EffectKind.Stun or EffectKind.Silence)
                     .ToArray())
        {
            ApplyKernelEvents(
                EffectEngine.Remove(_player.Actor, effect.Definition.Id, now),
                _player.Actor.ActorId,
                _player.Actor.ActorId,
                effect.Definition.Id);
        }

        _coldBloodReadyAtUtc = TryGetMageHook("I-7-3", out _)
            ? now + TimeSpan.FromSeconds(3)
            : null;
    }

    private void ActivateIceBarrier(DateTimeOffset now)
    {
        decimal absorbPercent = 15m;
        TimeSpan duration = TimeSpan.FromSeconds(8);
        if (TryGetMageHook("I-5-3", out ResolvedTalentEventHook improved))
        {
            absorbPercent *= 1 + improved.Value / 100m;
            duration += TimeSpan.FromSeconds((double)improved.SecondaryValue);
        }

        ApplyMageEffect(_player.Actor, new EffectDefinition(
            IceBarrierEffectId, EffectKind.Shield, duration, 1,
            EffectStackPolicy.Replace, _player.Actor.MaxHp * absorbPercent / 100m), now);
    }

    private void OnIceBarrierBroken(CombatEvent combatEvent, DateTimeOffset now)
    {
        if (TryGetMageHook("I-6-4", out ResolvedTalentEventHook reaction))
        {
            ReduceCooldown(_playerRuntime, FrostNovaId, TimeSpan.FromSeconds((double)reaction.Value), now);
            AddResource(_player.Actor, reaction.SecondaryValue, now, reaction.TalentId);
        }

        if (TryGetMageHook("I-8-3", out ResolvedTalentEventHook emergency)
            && TalentCooldownReady(emergency.TalentId, now))
        {
            ApplyMageShield(EmergencyIceEffectId,
                _player.Actor.MaxHp * emergency.Value / 100m,
                emergency.Duration,
                now);
            StartTalentCooldown(emergency, now);
        }

        if (TryGetMageHook("I-5-4", out _)
            && combatEvent.SourceActorId is { } sourceId
            && _enemiesById.TryGetValue(sourceId, out CombatParticipantDefinition? attacker))
            ApplyChill(attacker.Actor, now, enhanced: false);
    }

    private void ApplyChill(CombatActorState target, DateTimeOffset now, bool enhanced)
    {
        decimal slow = enhanced ? 10m : 5m;
        TimeSpan duration = TimeSpan.FromSeconds(4);
        if (TryGetMageHook("I-2-1", out ResolvedTalentEventHook permafrost))
        {
            slow += permafrost.Value;
            duration += TimeSpan.FromSeconds(2);
        }
        if (string.Equals(BlizzardId, _playerRuntime.ActiveCast?.Ability.Id, StringComparison.Ordinal)
            && TryGetMageHook("I-3-3", out _))
            slow += 5m;

        ApplyMageEffect(target, new EffectDefinition(
            ChillEffectId, EffectKind.StatModifier, duration, 1,
            EffectStackPolicy.Replace, Math.Max(0.1m, 1 - slow / 100m),
            ModifiedStat: EffectStat.AttackSpeed,
            ModifierMode: EffectModifierMode.Multiplicative,
            SourceSpecific: true), now);
    }

    private void ApplyFreezeOrDeepChill(
        CombatActorState target,
        TimeSpan normalDuration,
        TimeSpan bossDuration,
        DateTimeOffset now)
    {
        if (IsBossEnemy(target))
        {
            ApplyMageEffect(target, new EffectDefinition(
                DeepChillEffectId, EffectKind.Debuff, bossDuration, 1,
                EffectStackPolicy.Replace, 0, SourceSpecific: true), now);
            return;
        }

        ApplyMageEffect(target, new EffectDefinition(
            FreezeEffectId, EffectKind.Stun, normalDuration, 1,
            EffectStackPolicy.Replace, 0, SourceSpecific: true), now);
    }

    private void ApplyWinterChill(
        CombatActorState target,
        int maxStacks,
        TimeSpan duration,
        DateTimeOffset now)
    {
        ApplyMageEffect(target, new EffectDefinition(
            WinterChillEffectId, EffectKind.Debuff, duration, Math.Max(1, maxStacks),
            EffectStackPolicy.Stack, 1, SourceSpecific: false), now);
    }

    private void ExtendFrozenStateOnce(CombatActorState target, decimal seconds, DateTimeOffset now)
    {
        if (HasOwnEffect(target, FrostExtendedEffectId, now)) return;
        ActiveEffect? state = FindOwnEffect(target, FreezeEffectId, now)
            ?? FindOwnEffect(target, DeepChillEffectId, now);
        if (state is null) return;

        state.ExpiresAtUtc += TimeSpan.FromSeconds((double)seconds);
        ApplyMageEffect(target, new EffectDefinition(
            FrostExtendedEffectId, EffectKind.Debuff,
            state.ExpiresAtUtc - now, 1,
            EffectStackPolicy.Replace, 0, SourceSpecific: true), now);
    }

    private void TryApplyDeepFreeze(
        CombatActorState target,
        AbilityExecutionResult execution,
        ResolvedTalentEventHook deepFreeze,
        DateTimeOffset now)
    {
        bool frozen = HasOwnEffect(target, FreezeEffectId, now);
        bool deep = HasOwnEffect(target, DeepChillEffectId, now);
        if (!frozen && !deep) return;
        if (_deepFreezeReadyAt.TryGetValue(target.ActorId, out DateTimeOffset readyAt) && readyAt > now)
            return;

        decimal directDamage = execution.Events
            .Where(item => item.Type == CombatEventType.DamageDealt && item.TargetActorId == target.ActorId)
            .Sum(item => item.Amount);
        if (directDamage <= 0) return;

        decimal bonus = IsBossEnemy(target) ? deepFreeze.Value : 25m;
        ApplyDelayedMageDamage(target, "MAGE_DEEP_FREEZE_HIT",
            directDamage * bonus / 100m,
            TimeSpan.FromMilliseconds(1), now);

        if (!IsBossEnemy(target))
            ApplyMageEffect(target, new EffectDefinition(
                "MAGE_DEEP_FREEZE_STUN", EffectKind.Stun, deepFreeze.Duration, 1,
                EffectStackPolicy.Replace, 0, SourceSpecific: true), now);

        _deepFreezeReadyAt[target.ActorId] = now + deepFreeze.InternalCooldown;
    }

    private void SyncMageConditionalEffects(DateTimeOffset now)
    {
        if (!IsMage) return;

        foreach (PendingMageResourceRefund pending in _pendingMageResourceRefunds
                     .Where(item => item.DueAtUtc <= now)
                     .ToArray())
        {
            AddResource(_player.Actor, pending.Amount, now, pending.TalentId);
            _pendingMageResourceRefunds.Remove(pending);
        }

        if (_coldBloodReadyAtUtc is { } coldBloodReadyAt && now >= coldBloodReadyAt)
        {
            _coldBloodReadyAtUtc = null;
            if (TryGetMageHook("I-7-3", out ResolvedTalentEventHook coldBlood)
                && !HasOwnEffect(_player.Actor, ColdBloodEffectId, now))
            {
                ApplyMageEffect(_player.Actor, new EffectDefinition(
                    ColdBloodEffectId,
                    EffectKind.Buff,
                    coldBlood.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    coldBlood.Value), now);
                ActiveEffect? activeColdBlood = FindOwnEffect(_player.Actor, ColdBloodEffectId, now);
                if (activeColdBlood is not null)
                    activeColdBlood.RemainingMagnitude = coldBlood.SecondaryValue;
            }
        }

        bool arcaneFortitude = TryGetMageHook("A-4-4", out ResolvedTalentEventHook fortitude)
            && ResourcePercent() > fortitude.Threshold;
        SyncIncomingDamageReductionEffect(
            ArcaneFortitudeEffectId,
            arcaneFortitude ? fortitude.Value : 0,
            now);

        decimal frostArmorReduction = 0m;
        if (HasOwnEffect(_player.Actor, IceBarrierEffectId, now)
            && TryGetMageHook("I-5-4", out ResolvedTalentEventHook armor))
            frostArmorReduction = armor.Value;
        SyncIncomingDamageReductionEffect(
            FrostArmorEffectId,
            frostArmorReduction,
            now);
    }

    private void SyncIncomingDamageReductionEffect(
        string effectId,
        decimal reductionPercent,
        DateTimeOffset now)
    {
        if (reductionPercent <= 0)
        {
            RemoveMageEffect(_player.Actor, effectId, now);
            return;
        }

        if (HasOwnEffect(_player.Actor, effectId, now)) return;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            effectId, EffectKind.StatModifier, TimeSpan.FromHours(12), 1,
            EffectStackPolicy.Replace, Math.Max(0, 1 - reductionPercent / 100m),
            ModifiedStat: EffectStat.IncomingDamageMultiplier,
            ModifierMode: EffectModifierMode.Multiplicative), now);
    }

    private decimal EffectivePlayerResourceRegenPerSecond(DateTimeOffset now)
    {
        decimal regen = _player.ResourceRegenPerSecond;
        if (!IsMage || regen <= 0) return regen;

        if (TryGetMageHook("A-2-1", out ResolvedTalentEventHook meditation))
            regen *= 1 + meditation.Value / 100m;

        ActiveEffect? afterglow = FindOwnEffect(_player.Actor, ClearcastingRegenEffectId, now);
        if (afterglow is not null)
            regen *= 1 + afterglow.Definition.Magnitude / 100m;

        if (TryGetMageHook("A-6-3", out ResolvedTalentEventHook deepMeditation)
            && _lastMageManaSpendAtUtc is { } spentAt
            && now - spentAt >= deepMeditation.Duration)
            regen *= 1 + deepMeditation.Value / 100m;

        return regen;
    }

    private void ConsumeEffectStack(
        CombatActorState actor,
        ActiveEffect effect,
        string effectId,
        DateTimeOffset now)
    {
        effect.Stacks = Math.Max(0, effect.Stacks - 1);
        if (effect.Stacks == 0)
            RemoveMageEffect(actor, effectId, now);
    }

    private void ApplyMageShield(
        string effectId,
        decimal amount,
        TimeSpan duration,
        DateTimeOffset now)
    {
        if (amount <= 0 || duration <= TimeSpan.Zero) return;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            effectId, EffectKind.Shield, duration, 1,
            EffectStackPolicy.Replace, amount), now);
    }

    private void ApplyDelayedMageDamage(
        CombatActorState target,
        string effectId,
        decimal amount,
        TimeSpan delay,
        DateTimeOffset now)
    {
        if (target.IsDead || amount <= 0 || delay <= TimeSpan.Zero) return;
        ApplyMageEffect(target, new EffectDefinition(
            effectId, EffectKind.DamageOverTime, delay, 1,
            EffectStackPolicy.Replace, amount, delay,
            SourceSpecific: true, PeriodicDamageType: DamageType.Magical), now);
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

    private static ActiveEffect? FindAnyActiveEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        actor.ActiveEffects.FirstOrDefault(effect =>
            string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
            && effect.ExpiresAtUtc > now);

    private decimal ResourcePercent() =>
        _player.Actor.MaxResource <= 0
            ? 0
            : _player.Actor.CurrentResource / _player.Actor.MaxResource * 100m;

    private bool IsArcanePowerActive(DateTimeOffset now) =>
        HasOwnEffect(_player.Actor, ArcanePowerEffectId, now);

    private bool HasMageTalent(string talentId) =>
        _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    private bool TryGetMageHook(string talentId, out ResolvedTalentEventHook hook)
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
        RemoveTalentEffects(EffectEngine.RemoveOwned(
            target,
            effectId,
            _player.Actor.ActorId,
            now));

    private static bool IsOffensiveMageAbility(AbilityDefinition ability) =>
        ability.IsSpell
        && ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            && action.DamageType == DamageType.Magical) == true;
}
