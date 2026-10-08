using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Mage;

internal sealed class FireMageRuntime(MageCombatContext context, MageCombatRuntime owner) : MageRuntimeBase(context, owner)
{
    private int _fireDirectCritStreak;
    private DateTimeOffset _lastFireDirectCritAt;
    private int _combustionCritCount;
    internal AbilityDefinition ResolvePyromancerAbility(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsMage || !string.Equals(ability.School, "FIRE", StringComparison.Ordinal))
            return ability;

        decimal damageMultiplier = ability.DamageMultiplier;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal criticalDamageBonus = ability.CriticalDamageBonus;
        decimal resourceCost = ability.ResourceCost;
        TimeSpan castTime = ability.CastTime;
        TimeSpan cooldown = ability.Cooldown;

        if (TryGetMageHook("F-1-4", out ResolvedTalentEventHook efficient))
            resourceCost *= Math.Max(0, 1 - efficient.Value / 100m);

        if (TryGetMageHook("F-5-2", out ResolvedTalentEventHook firePower))
            damageMultiplier *= 1 + firePower.Value / 100m;
        if (TryGetMageHook("F-3-4", out ResolvedTalentEventHook criticalMass))
            criticalChanceBonus += criticalMass.Value;
        if (TryGetMageHook("F-9-1", out ResolvedTalentEventHook embodiment))
            damageMultiplier *= 1 + embodiment.Value / 100m;

        if ((string.Equals(ability.Id, FlamestrikeId, StringComparison.Ordinal)
             || string.Equals(ability.Id, BlastWaveId, StringComparison.Ordinal))
            && TryGetMageHook("F-7-3", out ResolvedTalentEventHook worldInFlames))
        {
            criticalChanceBonus += worldInFlames.Value;
        }

        if (string.Equals(ability.Id, FireballId, StringComparison.Ordinal))
        {
            if (TryGetMageHook("F-1-1", out ResolvedTalentEventHook improvedFireball))
                castTime = ClampCastTime(castTime - TimeSpan.FromSeconds((double)improvedFireball.Value));
            if (TryGetMageHook("F-1-3", out ResolvedTalentEventHook burningSoul))
                resourceCost *= Math.Max(0, 1 - burningSoul.SecondaryValue / 100m);
        }
        else if (string.Equals(ability.Id, ScorchId, StringComparison.Ordinal))
        {
            if (TryGetMageHook("F-1-2", out ResolvedTalentEventHook incineration))
                criticalChanceBonus += incineration.Value;
            if (TryGetMageHook("F-1-3", out ResolvedTalentEventHook burningSoul))
                resourceCost *= Math.Max(0, 1 - burningSoul.SecondaryValue / 100m);
        }
        else if (string.Equals(ability.Id, FireBlastId, StringComparison.Ordinal))
        {
            if (TryGetMageHook("F-1-2", out ResolvedTalentEventHook incineration))
                criticalChanceBonus += incineration.Value;
            if (TryGetMageHook("F-2-4", out ResolvedTalentEventHook improvedBlast))
            {
                cooldown = TimeSpan.FromSeconds(Math.Max(0, cooldown.TotalSeconds - (double)improvedBlast.Value));
                criticalChanceBonus += improvedBlast.SecondaryValue;
            }
        }
        else if (string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal))
        {
            if (TryGetMageHook("F-4-2", out ResolvedTalentEventHook empoweredPyro))
                castTime = ClampCastTime(castTime - TimeSpan.FromSeconds((double)empoweredPyro.Value));

            ActiveEffect? pyromaniac = FindOwnEffect(_player.Actor, PyromaniacEffectId, now);
            if (pyromaniac is not null)
                castTime = ClampCastTime(castTime - TimeSpan.FromSeconds((double)(pyromaniac.Stacks * pyromaniac.Definition.Magnitude)));

            ActiveEffect? hotStreak = FindOwnEffect(_player.Actor, HotStreakEffectId, now);
            if (hotStreak is not null)
            {
                castTime = TimeSpan.Zero;
                resourceCost *= 0.5m;
            }

            if (HasOwnEffect(_player.Actor, CombustionFirstPyroEffectId, now))
            {
                if (TryGetMageHook("F-8-3", out ResolvedTalentEventHook perfectCombustion))
                    criticalChanceBonus += perfectCombustion.Value;
                if (HasMageTalent("F-9-1"))
                    castTime = TimeSpan.Zero;
            }
        }
        else if (string.Equals(ability.Id, FlamestrikeId, StringComparison.Ordinal)
            && TryGetMageHook("F-4-4", out ResolvedTalentEventHook improvedFlamestrike))
        {
            damageMultiplier *= 1 + improvedFlamestrike.Value / 100m;
        }

        ActiveEffect? kindling = FindOwnEffect(_player.Actor, KindlingEffectId, now);
        if (kindling is not null
            && (string.Equals(ability.Id, FireballId, StringComparison.Ordinal)
                || string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal)))
            damageMultiplier *= 1 + kindling.Definition.Magnitude / 100m;

        ActiveEffect? heatDiscount = FindOwnEffect(_player.Actor, HeatDiscountEffectId, now);
        if (heatDiscount is not null && IsOffensiveFireAbility(ability))
            resourceCost *= Math.Max(0, 1 - heatDiscount.Definition.Magnitude / 100m);

        if (IsCombustionActive(now) && IsDirectFireAbility(ability))
        {
            ActiveEffect? stacks = FindOwnEffect(_player.Actor, CombustionCritStackEffectId, now);
            if (stacks is not null)
                criticalChanceBonus += stacks.Stacks * stacks.Definition.Magnitude;
            if (TryGetMageHook("F-6-2", out ResolvedTalentEventHook powerfulCombustion))
                criticalDamageBonus += powerfulCombustion.Value;
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            CastTime = castTime,
            Cooldown = cooldown,
            DamageMultiplier = Math.Max(0, damageMultiplier),
            CriticalChanceBonus = criticalChanceBonus,
            CriticalDamageBonus = criticalDamageBonus
        };
    }

    internal AbilityTargetModifier ResolvePyromancerTargetAbilityModifier(
        AbilityDefinition ability,
        CombatActorState target,
        AbilityTargetModifier modifier,
        DateTimeOffset now)
    {
        if (!IsMage || target.IsDead || !string.Equals(ability.School, "FIRE", StringComparison.Ordinal))
            return modifier;

        decimal damageMultiplier = modifier.DamageMultiplier;
        ActiveEffect? vulnerability = FindOwnEffect(target, FireVulnerabilityEffectId, now);
        if (vulnerability is not null)
            damageMultiplier *= 1 + vulnerability.Stacks * vulnerability.Definition.Magnitude / 100m;

        ActiveEffect? pyroclasm = FindOwnEffect(target, PyroclasmEffectId, now);
        if (pyroclasm is not null)
            damageMultiplier *= 1 + pyroclasm.Definition.Magnitude / 100m;

        if ((string.Equals(ability.Id, FireballId, StringComparison.Ordinal)
             || string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal))
            && TryGetMageHook("F-7-4", out ResolvedTalentEventHook execute)
            && HpPercent(target) < execute.Threshold)
            damageMultiplier *= 1 + execute.Value / 100m;

        return modifier with { DamageMultiplier = damageMultiplier };
    }

    internal void OnPyromancerAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsMage || !string.Equals(ability.School, "FIRE", StringComparison.Ordinal)) return;

        if (string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal))
        {
            RemovePyroEffect(_player.Actor, HotStreakEffectId, now);
            RemovePyroEffect(_player.Actor, PyromaniacEffectId, now);
            RemovePyroEffect(_player.Actor, CombustionFirstPyroEffectId, now);
        }

        if ((string.Equals(ability.Id, FireballId, StringComparison.Ordinal)
             || string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal))
            && HasOwnEffect(_player.Actor, KindlingEffectId, now))
            RemovePyroEffect(_player.Actor, KindlingEffectId, now);

        if (IsOffensiveFireAbility(ability) && HasOwnEffect(_player.Actor, HeatDiscountEffectId, now))
            RemovePyroEffect(_player.Actor, HeatDiscountEffectId, now);
    }

    internal void OnPyromancerAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
        => RunResolvedProcHooks(execution, "pyromancer-resolved", () => OnSafePyromancerAbilityResolved(ability, execution, now));

    internal void OnSafePyromancerAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (!IsMage || !Context.IsActive()) return;

        if (string.Equals(ability.Id, CombustionId, StringComparison.Ordinal))
        {
            ActivateCombustion(now);
            return;
        }

        if (!IsOffensiveFireAbility(ability)) return;

        bool hit = DidHit(execution);
        bool critical = DidCrit(execution);
        if (!hit)
        {
            _fireDirectCritStreak = 0;
            return;
        }

        IReadOnlyList<CombatActorState> hitTargets = HitFireTargets(execution).ToArray();
        HashSet<Guid> criticalTargetIds = execution.Events
            .Where(item => item.Type == CombatEventType.CriticalHit && item.TargetActorId.HasValue)
            .Select(item => item.TargetActorId!.Value)
            .ToHashSet();

        if (string.Equals(ability.Id, ScorchId, StringComparison.Ordinal)
            && TryGetMageHook("F-3-2", out ResolvedTalentEventHook scorch))
        {
            foreach (CombatActorState target in hitTargets)
                ApplyPyroEffect(target, new EffectDefinition(
                    FireVulnerabilityEffectId, EffectKind.Debuff, scorch.Duration, 5,
                    EffectStackPolicy.Stack, scorch.Value, SourceSpecific: true), now);
        }

        if (string.Equals(ability.Id, BlastWaveId, StringComparison.Ordinal)
            && TryGetMageHook("F-5-1", out ResolvedTalentEventHook blastWave))
        {
            foreach (CombatActorState target in hitTargets)
                ApplyPyroEffect(target, new EffectDefinition(
                    BlastWaveSlowEffectId, EffectKind.StatModifier, blastWave.Duration, 1,
                    EffectStackPolicy.Replace, 0.85m,
                    ModifiedStat: EffectStat.AttackSpeed,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true), now);
        }

        if ((string.Equals(ability.Id, FireBlastId, StringComparison.Ordinal)
             || string.Equals(ability.Id, BlastWaveId, StringComparison.Ordinal))
            && TryGetMageHook("F-5-3", out ResolvedTalentEventHook kindling))
            ApplyPyroEffect(_player.Actor, new EffectDefinition(
                KindlingEffectId, EffectKind.Buff, kindling.Duration, 1,
                EffectStackPolicy.Replace, kindling.Value), now);

        if (critical && TryGetMageHook("F-3-3", out ResolvedTalentEventHook elements))
            AddResource(_player.Actor, ability.ResourceCost * elements.Value / 100m, now, elements.TalentId);

        if (critical && TryGetMageHook("F-5-4", out ResolvedTalentEventHook heat))
            ApplyPyroEffect(_player.Actor, new EffectDefinition(
                HeatDiscountEffectId, EffectKind.Buff, heat.Duration, 1,
                EffectStackPolicy.Replace, heat.Value), now);

        if (string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal))
        {
            decimal burnMultiplier = 1m;
            if (TryGetMageHook("F-4-2", out ResolvedTalentEventHook empoweredPyro))
                burnMultiplier += empoweredPyro.SecondaryValue / 100m;

            foreach (CombatActorState target in hitTargets)
                ApplySimpleFireDot(
                    target,
                    PyroblastBurnEffectId,
                    EffectiveFireSpellPower(now) * 0.06m * burnMultiplier,
                    4,
                    now);
        }

        if (string.Equals(ability.Id, FlamestrikeId, StringComparison.Ordinal))
        {
            // The design requires a short baseline burn but does not specify an exact
            // coefficient. Keep the isolated follow-up deliberately small until that
            // tuning value moves into content.
            foreach (CombatActorState target in hitTargets)
                ApplySimpleFireDot(
                    target,
                    FlamestrikeBurnEffectId,
                    EffectiveFireSpellPower(now) * 0.04m,
                    3,
                    now);
        }

        if (critical && string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal)
            && TryGetMageHook("F-7-2", out ResolvedTalentEventHook pyroclasmHook))
        {
            foreach (CombatActorState target in hitTargets)
                ApplyPyroEffect(target, new EffectDefinition(
                    PyroclasmEffectId, EffectKind.Debuff, pyroclasmHook.Duration, 1,
                    EffectStackPolicy.Replace, pyroclasmHook.Value, SourceSpecific: true), now);
        }

        bool aoe = string.Equals(ability.Id, FlamestrikeId, StringComparison.Ordinal)
            || string.Equals(ability.Id, BlastWaveId, StringComparison.Ordinal);
        if (critical && TryGetMageHook("F-2-1", out ResolvedTalentEventHook ignite))
        {
            decimal igniteScale = aoe ? 0m : 1m;
            if (string.Equals(ability.Id, FlamestrikeId, StringComparison.Ordinal)
                && HasMageTalent("F-4-4"))
            {
                igniteScale = 1m;
            }

            if (aoe && TryGetMageHook("F-7-3", out ResolvedTalentEventHook worldInFlames))
                igniteScale = Math.Max(igniteScale, worldInFlames.SecondaryValue / 100m);

            if (igniteScale > 0)
            {
                foreach (CombatActorState target in hitTargets.Where(target =>
                             criticalTargetIds.Contains(target.ActorId)))
                {
                    decimal direct = execution.Events
                        .Where(item => item.Type == CombatEventType.DamageDealt
                            && item.TargetActorId == target.ActorId)
                        .Sum(item => item.Amount);
                    ApplyRollingIgnite(target, direct * ignite.Value / 100m * igniteScale, now);
                }
            }
        }

        if (TryGetMageHook("F-2-2", out ResolvedTalentEventHook impact))
        {
            foreach (CombatActorState target in hitTargets)
            {
                if (!IsBossEnemy(target) && _random.NextUnit() < impact.Value / 100m)
                    ApplyPyroEffect(target, new EffectDefinition(
                        "MAGE_FIRE_IMPACT_STUN", EffectKind.Stun, impact.Duration, 1,
                        EffectStackPolicy.Replace, 0, SourceSpecific: true), now);
            }
        }

        UpdateFireCritChains(ability, critical, now);
        UpdateCombustionState(ability, execution, critical, now);
    }

    internal void ApplyPyromancerEnemyKilledHooks(CombatEvent death)
    {
        if (!IsMage || !HasMageTalent("F-6-3") || death.TargetActorId is not { } deadId) return;
        if (!_enemiesById.TryGetValue(deadId, out CombatParticipantDefinition? dead)) return;

        ActiveEffect? ignite = FindOwnEffect(dead.Actor, IgniteEffectId, death.OccurredAtUtc);
        if (ignite is null) return;

        CombatActorState? recipient = _enemiesById.Values
            .Select(enemy => enemy.Actor)
            .FirstOrDefault(actor => !actor.IsDead && actor.ActorId != deadId);
        if (recipient is null) return;

        decimal seconds = Math.Max(0, (decimal)(ignite.ExpiresAtUtc - death.OccurredAtUtc).TotalSeconds);
        decimal remaining = ignite.Definition.Magnitude * Math.Ceiling(seconds);
        if (remaining > 0) ApplyRollingIgnite(recipient, remaining, death.OccurredAtUtc);
    }

    internal void OnPyromancerAbilityInterrupted(CombatEvent combatEvent)
    {
        if (combatEvent.ActorId == _player.Actor.ActorId)
            _fireDirectCritStreak = 0;
    }

    internal void ActivateCombustion(DateTimeOffset now)
    {
        if (!TryGetMageHook("F-6-1", out ResolvedTalentEventHook combustion)) return;
        TimeSpan duration = HasMageTalent("F-9-1") ? TimeSpan.FromSeconds(15) : combustion.Duration;
        _combustionCritCount = 0;
        RemovePyroEffect(_player.Actor, CombustionCritStackEffectId, now);
        ApplyPyroEffect(_player.Actor, new EffectDefinition(
            CombustionEffectId, EffectKind.Buff, duration, 1, EffectStackPolicy.Replace, 0), now);

        if (TryGetMageHook("F-8-3", out ResolvedTalentEventHook perfect))
        {
            _playerRuntime.Cooldowns.Remove(FireBlastId);
            _playerRuntime.Cooldowns.Remove(BlastWaveId);
            ApplyPyroEffect(_player.Actor, new EffectDefinition(
                CombustionFirstPyroEffectId, EffectKind.Buff, duration, 1,
                EffectStackPolicy.Replace, perfect.Value), now);
        }
        else if (HasMageTalent("F-9-1"))
        {
            ApplyPyroEffect(_player.Actor, new EffectDefinition(
                CombustionFirstPyroEffectId, EffectKind.Buff, duration, 1,
                EffectStackPolicy.Replace, 0), now);
        }
    }

    internal void UpdateCombustionState(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        bool critical,
        DateTimeOffset now)
    {
        if (!IsCombustionActive(now) || !IsDirectFireAbility(ability)) return;
        if (critical)
        {
            _combustionCritCount++;
            RemovePyroEffect(_player.Actor, CombustionCritStackEffectId, now);
            if (TryGetMageHook("F-6-4", out ResolvedTalentEventHook mana))
                AddResource(_player.Actor, mana.Value, now, mana.TalentId);

            if (_combustionCritCount == 3
                && TryGetMageHook("F-6-2", out ResolvedTalentEventHook powerfulCombustion))
            {
                CombatActorState? target = ResolveExecutionEnemyTarget(execution);
                if (target is not null)
                {
                    decimal directDamage = execution.Events
                        .Where(item => item.Type == CombatEventType.DamageDealt
                            && item.TargetActorId == target.ActorId)
                        .Sum(item => item.Amount);
                    if (directDamage > 0)
                    {
                        // The design specifies a small third-crit pulse without a separate
                        // magnitude. Reuse the talent's 10/20 value as the pulse percentage
                        // instead of inventing another hidden tuning number.
                        ApplyDelayedMageDamage(
                            target,
                            CombustionPulseEffectId,
                            directDamage * powerfulCombustion.Value / 100m,
                            TimeSpan.FromMilliseconds(1),
                            now);
                    }
                }
            }

            int limit = HasMageTalent("F-9-1") ? 4 : 3;
            if (_combustionCritCount >= limit)
                RemovePyroEffect(_player.Actor, CombustionEffectId, now);
        }
        else if (TryGetMageHook("F-6-1", out ResolvedTalentEventHook combustion))
        {
            ActiveEffect? active = FindOwnEffect(_player.Actor, CombustionEffectId, now);
            TimeSpan remaining = active is null ? TimeSpan.FromSeconds(1) : active.ExpiresAtUtc - now;
            if (remaining > TimeSpan.Zero)
                ApplyPyroEffect(_player.Actor, new EffectDefinition(
                    CombustionCritStackEffectId, EffectKind.Buff, remaining, 20,
                    EffectStackPolicy.Stack, combustion.Value), now);
        }
    }

    internal void UpdateFireCritChains(AbilityDefinition ability, bool critical, DateTimeOffset now)
    {
        if (!IsDirectFireAbility(ability)) return;

        if (critical)
        {
            _fireDirectCritStreak = now - _lastFireDirectCritAt <= TimeSpan.FromSeconds(6)
                ? _fireDirectCritStreak + 1
                : 1;
            _lastFireDirectCritAt = now;

            if (string.Equals(ability.Id, FireballId, StringComparison.Ordinal)
                && TryGetMageHook("F-7-1", out ResolvedTalentEventHook pyromaniac))
                ApplyPyroEffect(_player.Actor, new EffectDefinition(
                    PyromaniacEffectId, EffectKind.Buff, pyromaniac.Duration, 2,
                    EffectStackPolicy.Stack, pyromaniac.Value), now);

            if (_fireDirectCritStreak >= 2
                && TryGetMageHook("F-8-1", out ResolvedTalentEventHook hotStreak))
            {
                _fireDirectCritStreak = 0;
                ApplyPyroEffect(_player.Actor, new EffectDefinition(
                    HotStreakEffectId, EffectKind.Buff, hotStreak.Duration, 1,
                    EffectStackPolicy.Replace, hotStreak.Value), now);
            }
        }
        else
        {
            _fireDirectCritStreak = 0;
        }
    }

    internal void ApplyRollingIgnite(CombatActorState target, decimal addedTotalDamage, DateTimeOffset now)
    {
        if (target.IsDead || addedTotalDamage <= 0) return;
        decimal multiplier = 1m;
        if (TryGetMageHook("F-6-3", out ResolvedTalentEventHook living))
            multiplier *= 1 + living.Value / 100m;
        if (TryGetMageHook("F-8-2", out ResolvedTalentEventHook eternal))
            multiplier *= 1 + eternal.Value / 100m;

        ActiveEffect? current = FindOwnEffect(target, IgniteEffectId, now);
        decimal remaining = 0;
        if (current is not null)
        {
            decimal seconds = Math.Max(0, (decimal)(current.ExpiresAtUtc - now).TotalSeconds);
            remaining = current.Definition.Magnitude * Math.Ceiling(seconds);
            RemovePyroEffect(target, IgniteEffectId, now);
        }

        decimal total = remaining + addedTotalDamage * multiplier;
        ApplyPyroEffect(target, new EffectDefinition(
            IgniteEffectId, EffectKind.DamageOverTime, TimeSpan.FromSeconds(4), 1,
            EffectStackPolicy.Replace, total / 4m, TimeSpan.FromSeconds(1),
            SourceSpecific: true, PeriodicDamageType: DamageType.Magical), now);
    }

    internal void ApplySimpleFireDot(CombatActorState target, string id, decimal tick, int seconds, DateTimeOffset now)
    {
        if (target.IsDead || tick <= 0) return;
        if (TryGetMageHook("F-5-2", out ResolvedTalentEventHook firePower))
            tick *= 1 + firePower.Value / 100m;
        if (TryGetMageHook("F-9-1", out ResolvedTalentEventHook embodiment))
            tick *= 1 + embodiment.Value / 100m;
        ApplyPyroEffect(target, new EffectDefinition(
            id, EffectKind.DamageOverTime, TimeSpan.FromSeconds(seconds), 1,
            EffectStackPolicy.Replace, tick, TimeSpan.FromSeconds(1),
            SourceSpecific: true, PeriodicDamageType: DamageType.Magical), now);
    }

    internal decimal EffectiveFireSpellPower(DateTimeOffset now) =>
        TryGetMageHook("A-5-3", out ResolvedTalentEventHook overflow)
        && ResourcePercent() > overflow.Threshold
            ? _player.Actor.Stats.SpellPower * (1 + overflow.Value / 100m)
            : _player.Actor.Stats.SpellPower;

    internal bool IsCombustionActive(DateTimeOffset now) => HasOwnEffect(_player.Actor, CombustionEffectId, now);

    internal void ApplyPyroEffect(CombatActorState target, EffectDefinition effect, DateTimeOffset now) =>
        ApplyKernelEvents(EffectEngine.Apply(target, _player.Actor.ActorId, effect, now),
            _player.Actor.ActorId, target.ActorId, effect.Id);

    internal void RemovePyroEffect(CombatActorState target, string effectId, DateTimeOffset now) =>
        ApplyKernelEvents(EffectEngine.Remove(target, effectId, now),
            _player.Actor.ActorId, target.ActorId, effectId);

    internal CombatActorState? ResolveExecutionEnemyTarget(AbilityExecutionResult execution)
    {
        Guid? targetActorId = execution.Events.FirstOrDefault(item => item.Type == CombatEventType.DamageDealt)?.TargetActorId;
        return targetActorId is { } id && _enemiesById.TryGetValue(id, out CombatParticipantDefinition? target)
            ? target.Actor
            : null;
    }

    internal IEnumerable<CombatActorState> HitFireTargets(AbilityExecutionResult execution) =>
        execution.Events
            .Where(item => item.Type == CombatEventType.DamageDealt && item.TargetActorId.HasValue && item.Amount > 0)
            .Select(item => item.TargetActorId!.Value)
            .Distinct()
            .Where(_enemiesById.ContainsKey)
            .Select(id => _enemiesById[id].Actor);

    internal static bool IsDirectFireAbility(AbilityDefinition ability) =>
        string.Equals(ability.Id, FireballId, StringComparison.Ordinal)
        || string.Equals(ability.Id, FireBlastId, StringComparison.Ordinal)
        || string.Equals(ability.Id, ScorchId, StringComparison.Ordinal)
        || string.Equals(ability.Id, PyroblastId, StringComparison.Ordinal)
        || string.Equals(ability.Id, FlamestrikeId, StringComparison.Ordinal)
        || string.Equals(ability.Id, BlastWaveId, StringComparison.Ordinal);

    internal static bool IsOffensiveFireAbility(AbilityDefinition ability) =>
        string.Equals(ability.School, "FIRE", StringComparison.Ordinal)
        && ability.Actions?.Any(action => action.Type == AbilityActionType.Damage) == true;
}
