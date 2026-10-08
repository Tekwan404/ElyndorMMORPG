using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Mage;

internal sealed class FrostMageRuntime(MageCombatContext context, MageCombatRuntime owner) : MageRuntimeBase(context, owner)
{
    private DateTimeOffset? _coldBloodReadyAtUtc;
    internal DateTimeOffset? NextDueAt => _coldBloodReadyAtUtc;
    internal AbilityDefinition ResolveFrostMageAbility(AbilityDefinition ability, DateTimeOffset now)
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

    internal AbilityTargetModifier ResolveMageTargetAbilityModifier(
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

    internal void ApplyFrostResolvedHooks(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        bool hit,
        bool critical,
        DateTimeOffset now)
    {
        if (!hit) return;

        CombatActorState[] targets = HitTargets(execution).ToArray();
        HashSet<Guid> criticalTargetIds = execution.Events
            .Where(item => item.Type == CombatEventType.CriticalHit && item.TargetActorId.HasValue)
            .Select(item => item.TargetActorId!.Value)
            .ToHashSet();
        bool isBlizzard = string.Equals(ability.Id, BlizzardId, StringComparison.Ordinal);
        bool appliesChill = string.Equals(ability.Id, IceShardId, StringComparison.Ordinal)
            || isBlizzard
            || string.Equals(ability.Id, ConeOfColdId, StringComparison.Ordinal);

        if (appliesChill)
            foreach (CombatActorState target in targets)
                ApplyChill(
                    target,
                    now,
                    enhanced: string.Equals(ability.Id, ConeOfColdId, StringComparison.Ordinal),
                    improvedBlizzard: isBlizzard);

        bool directFrost = !isBlizzard;
        if (directFrost && TryGetMageHook("I-2-2", out ResolvedTalentEventHook frostbite))
        {
            foreach (CombatActorState target in targets)
                if (_random.NextUnit() < frostbite.Value / 100m)
                    ApplyFreezeOrDeepChill(target, frostbite.Duration, TimeSpan.FromSeconds((double)frostbite.SecondaryValue), now);
        }

        if (criticalTargetIds.Count > 0
            && TryGetMageHook("I-5-1", out ResolvedTalentEventHook winterChill))
        {
            foreach (CombatActorState target in targets.Where(target => criticalTargetIds.Contains(target.ActorId)))
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

        if (criticalTargetIds.Count > 0
            && TryGetMageHook("I-6-3", out ResolvedTalentEventHook boneChill))
        {
            foreach (CombatActorState target in targets.Where(target => criticalTargetIds.Contains(target.ActorId)))
                ExtendFrozenStateOnce(target, boneChill.Value, now);
        }

        if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal)
            && TryGetMageHook("I-7-1", out ResolvedTalentEventHook deepFreeze))
        {
            foreach (CombatActorState target in targets)
                TryApplyDeepFreeze(target, execution, deepFreeze, now);
        }
    }

    internal void ApplyFrostNova(DateTimeOffset now)
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

    internal void ActivateColdSnap(DateTimeOffset now)
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

    internal void ActivateIceBlock(DateTimeOffset now)
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

    internal void ActivateIceBarrier(DateTimeOffset now)
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

    internal void OnIceBarrierBroken(CombatEvent combatEvent, DateTimeOffset now)
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

    internal void ApplyChill(
        CombatActorState target,
        DateTimeOffset now,
        bool enhanced,
        bool improvedBlizzard = false)
    {
        decimal slow = enhanced ? 10m : 5m;
        TimeSpan duration = TimeSpan.FromSeconds(4);
        if (TryGetMageHook("I-2-1", out ResolvedTalentEventHook permafrost))
        {
            slow += permafrost.Value;
            duration += TimeSpan.FromSeconds(2);
        }
        if (improvedBlizzard && TryGetMageHook("I-3-3", out _))
            slow += 5m;

        ApplyMageEffect(target, new EffectDefinition(
            ChillEffectId, EffectKind.StatModifier, duration, 1,
            EffectStackPolicy.Replace, Math.Max(0.1m, 1 - slow / 100m),
            ModifiedStat: EffectStat.AttackSpeed,
            ModifierMode: EffectModifierMode.Multiplicative,
            SourceSpecific: true), now);
    }

    internal void ApplyFreezeOrDeepChill(
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

    internal void ApplyWinterChill(
        CombatActorState target,
        int maxStacks,
        TimeSpan duration,
        DateTimeOffset now)
    {
        ApplyMageEffect(target, new EffectDefinition(
            WinterChillEffectId, EffectKind.Debuff, duration, Math.Max(1, maxStacks),
            EffectStackPolicy.Stack, 1, SourceSpecific: false), now);
    }

    internal void ExtendFrozenStateOnce(CombatActorState target, decimal seconds, DateTimeOffset now)
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

    internal void TryApplyDeepFreeze(
        CombatActorState target,
        AbilityExecutionResult execution,
        ResolvedTalentEventHook deepFreeze,
        DateTimeOffset now)
    {
        bool frozen = HasOwnEffect(target, FreezeEffectId, now);
        bool deep = HasOwnEffect(target, DeepChillEffectId, now);
        if (!frozen && !deep) return;
        string cooldownKey = $"{deepFreeze.TalentId}:{target.ActorId}";
        if (!_procGuard.IsReady(_player.Actor.ActorId, cooldownKey, now))
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

        _procGuard.StartCooldown(_player.Actor.ActorId, cooldownKey, now, deepFreeze.InternalCooldown);
    }

    // The existing PvE sync boundary belongs to the fight, not whichever
    // participant happened to be activated last by regen/effect processing.

    internal void SyncConditionalEffects(DateTimeOffset now)
    {
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

        decimal frostArmorReduction = 0m;
        if (HasOwnEffect(_player.Actor, IceBarrierEffectId, now)
            && TryGetMageHook("I-5-4", out ResolvedTalentEventHook armor))
            frostArmorReduction = armor.Value;
        SyncIncomingDamageReductionEffect(
            FrostArmorEffectId,
            frostArmorReduction,
            now);
    }
}
