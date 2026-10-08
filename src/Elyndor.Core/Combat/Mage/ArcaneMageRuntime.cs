using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Mage;

internal sealed class ArcaneMageRuntime(MageCombatContext context, MageCombatRuntime owner) : MageRuntimeBase(context, owner)
{
    private DateTimeOffset? _lastMageManaSpendAtUtc;
    private int _arcanePowerManaSpendCount;
    private readonly List<PendingMageResourceRefund> _pendingMageResourceRefunds = [];
    private readonly List<CombatEvent> _channelProcEvents = [];
    private sealed record PendingMageResourceRefund(DateTimeOffset DueAtUtc, decimal Amount, string TalentId);
    internal DateTimeOffset? LastManaSpendAtUtc => _lastMageManaSpendAtUtc;
    internal DateTimeOffset? NextDueAt => _pendingMageResourceRefunds.Count == 0 ? null : _pendingMageResourceRefunds.Min(x => x.DueAtUtc);
    internal AbilityDefinition ResolveArcaneMageAbility(AbilityDefinition ability, DateTimeOffset now)
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
            if (HasMageTalent("A-9-1"))
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

        if (string.Equals(ability.Id, CounterspellId, StringComparison.Ordinal)
            && TryGetMageHook("A-3-4", out ResolvedTalentEventHook counterspellTraining))
            cooldown = TimeSpan.FromSeconds(Math.Max(0, cooldown.TotalSeconds - (double)counterspellTraining.Value));

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

    internal void OnMageAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsMage || !ability.IsSpell) return;
        _channelProcEvents.Clear();

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

        if (ability.ResourceCost > 0 && IsArcanePowerActive(now) && HasMageTalent("A-9-1"))
        {
            _arcanePowerManaSpendCount++;
            if (_arcanePowerManaSpendCount >= 3)
            {
                _arcanePowerManaSpendCount = 0;
                GrantClearcasting(now);
            }
        }

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal)
            && HasOwnEffect(_player.Actor, ColdBloodEffectId, now))
            RemoveMageEffect(_player.Actor, ColdBloodEffectId, now);

        if (string.Equals(ability.Id, IceLanceId, StringComparison.Ordinal)
            && HasOwnEffect(_player.Actor, ColdSnapLanceEffectId, now))
            RemoveMageEffect(_player.Actor, ColdSnapLanceEffectId, now);
    }

    internal void OnMageAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
        => RunResolvedProcHooks(execution, "mage-resolved", () => OnSafeMageAbilityResolved(ability, execution, now));

    internal void OnSafeMageAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (!IsMage || !Context.IsActive() || !ability.IsSpell)
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
            return;
        }
        if (string.Equals(ability.Id, CounterspellId, StringComparison.Ordinal))
        {
            ApplyCounterspell(now);
            return;
        }
        if (string.Equals(ability.Id, FrostNovaId, StringComparison.Ordinal))
        {
            Owner.Frost.ApplyFrostNova(now);
            return;
        }
        if (string.Equals(ability.Id, ColdSnapId, StringComparison.Ordinal))
        {
            Owner.Frost.ActivateColdSnap(now);
            return;
        }
        if (string.Equals(ability.Id, IceBlockId, StringComparison.Ordinal))
        {
            Owner.Frost.ActivateIceBlock(now);
            return;
        }
        if (string.Equals(ability.Id, IceBarrierId, StringComparison.Ordinal))
        {
            Owner.Frost.ActivateIceBarrier(now);
            return;
        }

        bool hit = DidHit(execution);
        bool critical = DidCrit(execution);

        AbilityExecutionResult procExecution = execution;
        bool completed = execution.Events.Any(item => item.Type == CombatEventType.AbilityCompleted);
        if (ability.Type == AbilityType.Channelled)
        {
            _channelProcEvents.AddRange(execution.Events.Where(item =>
                item.Type is CombatEventType.DamageDealt or CombatEventType.CriticalHit));
            if (completed)
            {
                procExecution = execution with { Events = _channelProcEvents.ToArray() };
                _channelProcEvents.Clear();
            }
        }
        if ((ability.Type != AbilityType.Channelled || completed) && DidHit(procExecution))
        {
            TryProcClearcasting(now);
            if (IsArcanePowerActive(now))
            {
                TryApplyArcanePowerEcho(ability, procExecution, now);
            }
        }

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal))
            Owner.Frost.ApplyFrostResolvedHooks(ability, execution, hit, critical, now);
    }

    internal void ApplyMageResourceThresholdHooks(CombatEvent combatEvent)
    {
        if (!IsMage || combatEvent.Amount >= 0
            || !string.Equals(combatEvent.DefinitionId, ManaShieldEffectId, StringComparison.Ordinal))
            return;
        if (TryGetMageHook("A-5-4", out ResolvedTalentEventHook absorption))
            _pendingMageResourceRefunds.Add(new(
                combatEvent.OccurredAtUtc + absorption.Duration,
                -combatEvent.Amount * absorption.Value / 100m,
                absorption.TalentId));
    }

    internal void ApplyMageShieldAbsorbedHooks(CombatEvent combatEvent)
    {
        if (!IsMage || combatEvent.TargetActorId != _player.Actor.ActorId || combatEvent.Amount <= 0)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        if (string.Equals(combatEvent.DefinitionId, ManaShieldEffectId, StringComparison.Ordinal))
        {
            if (_player.Actor.CurrentResource <= 0)
                RemoveMageEffect(_player.Actor, ManaShieldEffectId, now);
        }

        if (string.Equals(combatEvent.DefinitionId, IceBarrierEffectId, StringComparison.Ordinal)
            && !HasOwnEffect(_player.Actor, IceBarrierEffectId, now))
            Owner.Frost.OnIceBarrierBroken(combatEvent, now);
    }

    internal void ActivateArcanePower(DateTimeOffset now)
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
        {
            var freeSpells = new EffectDefinition(
                ArcanePowerFreeCostEffectId, EffectKind.Buff, duration, 2,
                EffectStackPolicy.Stack, 0);
            for (int charge = 0; charge < freeSpells.MaxStacks; charge++)
                ApplyMageEffect(_player.Actor, freeSpells, now);
        }
    }

    internal void ActivateManaShield(DateTimeOffset now)
    {
        decimal absorb = Math.Min(_player.Actor.MaxHp * 0.30m, Math.Max(1, _player.Actor.CurrentResource));
        decimal costPerDamage = TryGetMageHook("A-3-3", out ResolvedTalentEventHook improvedShield)
            ? Math.Max(0, 1 - improvedShield.Value / 100m)
            : 1m;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            ManaShieldEffectId, EffectKind.Shield, TimeSpan.FromSeconds(12), 1,
            EffectStackPolicy.Replace, absorb,
            ResourceCostPerAbsorbedDamage: costPerDamage), now);
    }

    internal void ApplyCounterspell(DateTimeOffset now)
    {
        if (!_enemiesById.TryGetValue(_selectedTargetActorId, out CombatParticipantDefinition? target)
            || target.Actor.IsDead)
            return;

        bool interruptedCast = false;
        if (_enemyRuntimes.TryGetValue(target.Actor.ActorId, out CombatRuntimeState? runtime))
        {
            AbilityExecutionResult interrupted = AbilityEngine.Interrupt(runtime, now, TimeSpan.Zero);
            if (interrupted.Succeeded)
            {
                interruptedCast = true;
                ApplyKernelEvents(
                    interrupted.Events,
                    _player.Actor.ActorId,
                    target.Actor.ActorId,
                    CounterspellId);
            }
        }

        if (interruptedCast && TryGetMageHook("A-4-1", out ResolvedTalentEventHook improved))
            ApplyMageEffect(target.Actor, new EffectDefinition(
                "MAGE_COUNTERSPELL_SILENCE", EffectKind.Silence,
                TimeSpan.FromSeconds((double)improved.Value), 1,
                EffectStackPolicy.Replace, 0, SourceSpecific: true), now);
    }

    internal void TryProcClearcasting(DateTimeOffset now)
    {
        if (!TryGetMageHook("A-1-2", out ResolvedTalentEventHook concentration)) return;
        if (_random.NextUnit() >= concentration.Value / 100m) return;
        GrantClearcasting(now);
    }

    internal void GrantClearcasting(DateTimeOffset now)
    {
        int maxStacks = HasMageTalent("A-8-1") ? 2 : 1;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            ClearcastingEffectId, EffectKind.Buff, TimeSpan.FromSeconds(30), maxStacks,
            maxStacks > 1 ? EffectStackPolicy.Stack : EffectStackPolicy.Replace, 0), now);
    }

    internal void TryApplyArcanePowerEcho(
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

    internal void SyncMageConditionalEffects(DateTimeOffset now)
    {
        if (!IsMage) return;

        foreach (PendingMageResourceRefund pending in _pendingMageResourceRefunds
                     .Where(item => item.DueAtUtc <= now)
                     .ToArray())
        {
            AddResource(_player.Actor, pending.Amount, now, pending.TalentId);
            _pendingMageResourceRefunds.Remove(pending);
        }

        bool arcaneFortitude = TryGetMageHook("A-4-4", out ResolvedTalentEventHook fortitude)
            && ResourcePercent() > fortitude.Threshold;
        if (arcaneFortitude)
        {
            if (!HasOwnEffect(_player.Actor, ArcaneFortitudeEffectId, now))
                ApplyMageEffect(_player.Actor, new EffectDefinition(
                    ArcaneFortitudeEffectId, EffectKind.Buff, TimeSpan.FromHours(12), 1,
                    EffectStackPolicy.Replace, fortitude.Value), now);
        }
        else
            RemoveMageEffect(_player.Actor, ArcaneFortitudeEffectId, now);

    }

    internal void ConfigureMageIncomingDamage()
    {
        if (!IsMage || !TryGetMageHook("A-4-4", out ResolvedTalentEventHook fortitude))
            return;
        CombatActorState actor = _player.Actor;
        var previous = actor.IncomingDamageModifier;
        actor.IncomingDamageModifier = (context, random) =>
        {
            decimal amount = previous?.Invoke(context, random) ?? context.CurrentAmount;
            return actor.MaxResource > 0
                && actor.CurrentResource / actor.MaxResource * 100m > fortitude.Threshold
                    ? amount * Math.Max(0, 1 - fortitude.Value / 100m)
                    : amount;
        };
    }

    internal decimal EffectivePlayerResourceRegenPerSecond(DateTimeOffset now)
    {
        decimal regen = _player.ResourceRegenPerSecond;
        if (!IsMage || regen <= 0) return regen;

        if (TryGetMageHook("A-2-1", out ResolvedTalentEventHook meditation))
            regen *= 1 + meditation.Value / 100m;

        ActiveEffect? afterglow = FindOwnEffect(_player.Actor, ClearcastingRegenEffectId, now);
        if (afterglow is not null && afterglow.AppliedAtUtc <= now)
            regen *= 1 + afterglow.Definition.Magnitude / 100m;

        if (TryGetMageHook("A-6-3", out ResolvedTalentEventHook deepMeditation)
            && now - (_lastMageManaSpendAtUtc ?? _combatStartedAtUtc) >= deepMeditation.Duration)
            regen *= 1 + deepMeditation.Value / 100m;

        return regen;
    }

    internal bool IsArcanePowerActive(DateTimeOffset now) =>
        HasOwnEffect(_player.Actor, ArcanePowerEffectId, now);
}
