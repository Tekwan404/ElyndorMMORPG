using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string BerserkAttackPowerEffectId = "BERSERK_ATTACK_POWER";
    private const string BloodRageAttackPowerEffectId = "BERSERKER_BLOOD_RAGE_ATTACK_POWER";
    private const string BloodRageAttackSpeedEffectId = "BERSERKER_BLOOD_RAGE_ATTACK_SPEED";
    private const string MomentumAttackSpeedEffectId = "BERSERKER_MOMENTUM_ATTACK_SPEED";
    private const string RecklessnessOutgoingEffectId = "BERSERKER_RECKLESSNESS_OUTGOING";
    private const string RecklessnessIncomingEffectId = "BERSERKER_RECKLESSNESS_INCOMING";
    private const string DevastatingVulnerabilityEffectId = "BERSERKER_DEVASTATING_VULNERABILITY";
    private const string DeathStrengthCriticalEffectId = "BERSERKER_DEATH_STRENGTH_CRITICAL";
    private const string DeathsEmbraceReadyEffectId = "BERSERKER_DEATHS_EMBRACE_READY";
    private const string BloodTrailEffectId = "BERSERKER_BLOOD_TRAIL";
    private const string RendingRampageEffectId = "BERSERKER_RENDING_RAMPAGE";
    private static readonly TimeSpan ConditionalEffectDuration = TimeSpan.FromHours(12);

    private bool _deathsEmbraceArmed;
    private bool _deathsEmbraceConsumed;

    private AbilityDefinition ResolvePlayerAbility(
        AbilityDefinition baseAbility,
        DateTimeOffset now)
    {
        AbilityDefinition ability = TalentAbilityResolver.Apply(baseAbility, _playerTalents);

        if (IsBerserkActive(now)
            && TryGetBerserkerHook("B-5-4", out ResolvedTalentEventHook frenzy)
            && IsAttackingAbility(ability))
        {
            decimal reduction = frenzy.Value / 100m;
            ability = ability with
            {
                ResourceCost = Math.Max(0, ability.ResourceCost * (1 - reduction))
            };
        }

        if (string.Equals(ability.Id, "BERSERK", StringComparison.Ordinal)
            && HasBerserkerTalent("B-9-1"))
        {
            ability = ability with
            {
                CanUseWhileSilenced = true,
                CanUseWhileStunned = true
            };
        }

        return ability;
    }

    private AbilityDefinition ResolvePlayerAbilityForSnapshot(
        AbilityDefinition baseAbility,
        DateTimeOffset now) =>
        ResolvePlayerAbility(baseAbility, now);

    private AbilityTargetModifier ResolveBerserkerTargetAbilityModifier(
        AbilityDefinition ability,
        CombatActorState target,
        AbilityTargetModifier modifier)
    {
        bool dealsPhysicalDamage = ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            && action.DamageType == DamageType.Physical) == true;
        if (dealsPhysicalDamage
            && !target.IsDead
            && TryGetBerserkerHook("B-7-4", out ResolvedTalentEventHook executioner)
            && HpPercent(target) < executioner.Threshold)
        {
            modifier = modifier with
            {
                DamageMultiplier = modifier.DamageMultiplier
                    * (1 + executioner.Value / 100m)
            };
        }

        return modifier;
    }

    private void OnPlayerAbilitySucceeded(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (Status != CombatSessionStatus.Active) return;

        if (ability.ResourceCost >= 20
            && TryGetBerserkerHook("B-2-4", out ResolvedTalentEventHook momentum))
        {
            ApplyTalentEffect(
                _player.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    MomentumAttackSpeedEffectId,
                    EffectKind.StatModifier,
                    momentum.Duration,
                    1,
                    EffectStackPolicy.Refresh,
                    momentum.Value / 100m,
                    ModifiedStat: EffectStat.AttackSpeed,
                    ModifierMode: EffectModifierMode.Percent),
                now);
        }

        if (string.Equals(ability.Id, "BERSERK", StringComparison.Ordinal)
            && HasBerserkerTalent("B-9-1"))
        {
            RemoveTalentEffects(EffectEngine.RemoveByKind(_player.Actor, EffectKind.Stun, now));
            RemoveTalentEffects(EffectEngine.RemoveByKind(_player.Actor, EffectKind.Silence, now));
        }

        if (string.Equals(ability.Id, "WHIRLWIND", StringComparison.Ordinal))
        {
            ApplyDeathWhirlwind(execution, now);
            ApplyRendingRampage(execution, now);
        }

        SyncBerserkerConditionalEffects(now);
    }

    private void ApplyBerserkerCriticalHooks(CombatEvent combatEvent)
    {
        if (combatEvent.SourceActorId != _player.Actor.ActorId) return;
        DateTimeOffset now = combatEvent.OccurredAtUtc;

        CombatParticipantDefinition? eventTarget =
            combatEvent.TargetActorId is { } targetActorId
            && _enemiesById.TryGetValue(targetActorId, out CombatParticipantDefinition? resolvedTarget)
                ? resolvedTarget
                : null;

        if (string.Equals(combatEvent.DefinitionId, "WILD_STRIKE", StringComparison.Ordinal)
            && eventTarget is not null
            && !eventTarget.Actor.IsDead
            && TryGetBerserkerHook("B-3-4", out ResolvedTalentEventHook bloodTrail))
        {
            decimal attackPower = EffectEngine.CalculateStat(
                _player.Actor,
                EffectStat.AttackPower,
                _player.Actor.Stats.AttackPower,
                now);
            decimal tickDamage =
                attackPower * bloodTrail.Value / 100m;
            ApplyTalentEffect(
                eventTarget.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    BloodTrailEffectId,
                    EffectKind.DamageOverTime,
                    bloodTrail.Duration,
                    1,
                    EffectStackPolicy.Refresh,
                    tickDamage,
                    bloodTrail.TickInterval),
                now);
        }

        if (string.Equals(combatEvent.DefinitionId, "AUTO_ATTACK", StringComparison.Ordinal)
            && eventTarget is not null
            && !eventTarget.Actor.IsDead
            && TryGetBerserkerHook("B-6-2", out ResolvedTalentEventHook devastating))
        {
            ApplyTalentEffect(
                eventTarget.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    DevastatingVulnerabilityEffectId,
                    EffectKind.StatModifier,
                    devastating.Duration,
                    1,
                    EffectStackPolicy.Refresh,
                    1 + devastating.Value / 100m,
                    ModifiedStat: EffectStat.IncomingPhysicalDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true),
                now);
        }

        if (TryGetBerserkerHook("B-6-4", out ResolvedTalentEventHook bloodMomentum)
            && !IsBerserkActive(now)
            && TalentCooldownReady(bloodMomentum.TalentId, now)
            && ReduceCooldown(
                _playerRuntime,
                "BERSERK",
                TimeSpan.FromSeconds((double)bloodMomentum.Value),
                now))
        {
            StartTalentCooldown(bloodMomentum, now);
        }

        if (TryGetBerserkerHook("B-9-1", out ResolvedTalentEventHook avatar)
            && _random.NextUnit() < avatar.ChancePercent / 100m)
        {
            AddResource(
                _player.Actor,
                avatar.Value,
                now,
                avatar.TalentId);
        }
    }

    private void ApplyBerserkerDamageTakenHooks(CombatEvent combatEvent)
    {
        if (combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.Amount <= 0
            || combatEvent.IsPeriodic)
        {
            return;
        }

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        if (IsBerserkActive(now)
            && TryGetBerserkerHook("B-6-3", out ResolvedTalentEventHook battleTrance))
        {
            AddResource(
                _player.Actor,
                battleTrance.Value,
                now,
                battleTrance.TalentId);
        }

        SyncBerserkerConditionalEffects(now);
    }

    private void ApplyBerserkerEnemyKilledHooks(DateTimeOffset now)
    {
        if (IsBerserkActive(now)
            && HasBerserkerTalent("B-8-2"))
        {
            _playerRuntime.Cooldowns.Remove("WILD_STRIKE");
        }
    }

    private void ResolvePlayerAutoAttack(
        CombatParticipantDefinition target,
        AutoAttackProfile profile,
        DateTimeOffset now)
    {
        SyncBerserkerConditionalEffects(now);

        decimal attackPower = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.AttackPower,
            _player.Actor.Stats.AttackPower,
            now);
        decimal baseDamage = AutoAttackDamageRoller.RollPlayerDamage(
            profile,
            attackPower,
            _random);

        ArcherAutoAttackModifier archerModifier =
            ResolveArcherAutoAttackModifier(target, baseDamage, now);

        bool consumeDeathsEmbrace = _deathsEmbraceArmed && !_deathsEmbraceConsumed;
        decimal deathsEmbraceMultiplier = 1;
        if (consumeDeathsEmbrace)
        {
            _deathsEmbraceArmed = false;
            _deathsEmbraceConsumed = true;
            if (TryGetBerserkerHook("B-8-3", out ResolvedTalentEventHook embrace))
                deathsEmbraceMultiplier = embrace.Value / 100m;

            RemoveTalentEffects(EffectEngine.Remove(
                _player.Actor,
                DeathsEmbraceReadyEffectId,
                now));
        }

        DamageResult damage = DamagePipeline.Resolve(
            new DamageRequest(
                _player.Actor,
                target.Actor,
                baseDamage,
                DamageType.Physical,
                DamageMultiplier: deathsEmbraceMultiplier
                    * ResolveWarlordVengeanceMultiplier()
                    * BerserkerTargetPhysicalDamageMultiplier(target.Actor)
                    * archerModifier.DamageMultiplier,
                ArmorPenetrationBonus: archerModifier.ArmorPenetrationBonus,
                ForceCritical: consumeDeathsEmbrace,
                AccuracyBonus: archerModifier.AccuracyBonus,
                CriticalChanceBonus: archerModifier.CriticalChanceBonus,
                CriticalDamageBonus: archerModifier.CriticalDamageBonus),
            _random,
            now);
        ApplyKernelEvents(
            damage.Events,
            _player.Actor.ActorId,
            target.Actor.ActorId,
            "AUTO_ATTACK",
            profile.WeaponHand,
            profile.WeaponDefinitionId);
        ApplyArcherAutoAttackResolved(
            target,
            profile,
            baseDamage,
            damage,
            archerModifier,
            now);

        if (damage.Avoidance == DamageAvoidance.None
            && damage.HpDamage > 0
            && profile.ResourceOnHit > 0)
        {
            AddResource(
                _player.Actor,
                ResolveWarlordAutoAttackResource(profile.ResourceOnHit),
                now,
                "AUTO_ATTACK");
        }

        if (Status != CombatSessionStatus.Active
            || damage.Avoidance != DamageAvoidance.None
            || damage.HpDamage <= 0
            || target.Actor.IsDead)
        {
            return;
        }

        if (IsBerserkActive(now)
            && TryGetBerserkerHook("B-7-1", out ResolvedTalentEventHook unstoppable))
        {
            ResolveSecondaryAutoAttack(
                target,
                profile,
                baseDamage,
                unstoppable.Value / 100m,
                unstoppable.TalentId,
                now);
            return;
        }

        if (TryGetBerserkerHook("B-4-1", out ResolvedTalentEventHook doubleStrike)
            && TalentCooldownReady(doubleStrike.TalentId, now)
            && _random.NextUnit() < doubleStrike.ChancePercent / 100m)
        {
            ResolveSecondaryAutoAttack(
                target,
                profile,
                baseDamage,
                doubleStrike.Value / 100m,
                doubleStrike.TalentId,
                now);
            StartTalentCooldown(doubleStrike, now);
        }

        if (_player.Actor.ActiveEffects.Any(effect =>
                effect.Definition.Id == WarlordBattleStandardEffectId)
            && _random.NextUnit() < 0.10m)
        {
            ResolveSecondaryAutoAttack(
                target,
                profile,
                baseDamage,
                0.25m,
                "W-7-1",
                now);
        }
    }

    private void ResolveSecondaryAutoAttack(
        CombatParticipantDefinition target,
        AutoAttackProfile sourceProfile,
        decimal ordinaryBaseDamage,
        decimal multiplier,
        string definitionId,
        DateTimeOffset now)
    {
        if (target.Actor.IsDead || Status != CombatSessionStatus.Active) return;

        DamageResult secondary = DamagePipeline.Resolve(
            new DamageRequest(
                _player.Actor,
                target.Actor,
                ordinaryBaseDamage * multiplier,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                DamageMultiplier: BerserkerTargetPhysicalDamageMultiplier(target.Actor)),
            _random,
            now);
        ApplyKernelEvents(
            secondary.Events,
            _player.Actor.ActorId,
            target.Actor.ActorId,
            definitionId,
            sourceProfile.WeaponHand,
            sourceProfile.WeaponDefinitionId);
    }

    private void ApplyDeathWhirlwind(
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (Status != CombatSessionStatus.Active
            || !TryGetBerserkerHook("B-8-1", out ResolvedTalentEventHook deathWhirlwind))
        {
            return;
        }

        foreach (CombatEvent physical in execution.Events.Where(item =>
                     item.Type == CombatEventType.DamageDealt
                     && item.AmountBeforeShields > 0
                     && item.TargetActorId.HasValue))
        {
            if (!_enemiesById.TryGetValue(
                    physical.TargetActorId!.Value,
                    out CombatParticipantDefinition? target)
                || target.Actor.IsDead)
            {
                continue;
            }

            DamageResult extra = DamagePipeline.Resolve(
                new DamageRequest(
                    _player.Actor,
                    target.Actor,
                    physical.AmountBeforeShields
                        * deathWhirlwind.Value
                        / 100m,
                    DamageType.True,
                    CanMiss: false,
                    CanDodge: false,
                    CanCrit: false,
                    MinimumDamage: 0),
                _random,
                now);
            ApplyKernelEvents(
                extra.Events,
                _player.Actor.ActorId,
                target.Actor.ActorId,
                "B-8-1");
            if (Status != CombatSessionStatus.Active)
                return;
        }
    }

    private void ApplyRendingRampage(
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (Status != CombatSessionStatus.Active
            || !TryGetBerserkerHook("B-7-3", out ResolvedTalentEventHook rending))
        {
            return;
        }

        decimal attackPower = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.AttackPower,
            _player.Actor.Stats.AttackPower,
            now);
        decimal tickDamage = attackPower * rending.Value / 100m;
        Guid[] hitTargetIds = execution.Events
            .Where(item => item.Type == CombatEventType.DamageDealt
                && item.Amount > 0
                && item.TargetActorId.HasValue)
            .Select(item => item.TargetActorId!.Value)
            .Distinct()
            .ToArray();
        foreach (Guid targetActorId in hitTargetIds)
        {
            if (!_enemiesById.TryGetValue(
                    targetActorId,
                    out CombatParticipantDefinition? target)
                || target.Actor.IsDead)
            {
                continue;
            }

            ApplyTalentEffect(
                target.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    RendingRampageEffectId,
                    EffectKind.DamageOverTime,
                    rending.Duration,
                    1,
                    EffectStackPolicy.Refresh,
                    tickDamage,
                    rending.TickInterval),
                now);
            if (Status != CombatSessionStatus.Active)
                return;
        }
    }

    private decimal BerserkerTargetPhysicalDamageMultiplier(
        CombatActorState target)
    {
        if (target.IsDead
            || !TryGetBerserkerHook("B-7-4", out ResolvedTalentEventHook executioner)
            || HpPercent(target) >= executioner.Threshold)
        {
            return 1;
        }

        return 1 + executioner.Value / 100m;
    }

    private void SyncBerserkerConditionalEffects(DateTimeOffset now)
    {
        if (Status != CombatSessionStatus.Active || _player.Actor.IsDead) return;

        decimal playerHpPercent = HpPercent(_player.Actor);

        if (TryGetBerserkerHook("B-2-1", out ResolvedTalentEventHook bloodRage))
        {
            bool active = playerHpPercent < bloodRage.Threshold;
            SyncStatEffect(
                _player.Actor,
                BloodRageAttackPowerEffectId,
                active,
                EffectStat.AttackPower,
                bloodRage.Value / 100m,
                EffectModifierMode.Percent,
                now);
            SyncStatEffect(
                _player.Actor,
                BloodRageAttackSpeedEffectId,
                active,
                EffectStat.AttackSpeed,
                bloodRage.SecondaryValue / 100m,
                EffectModifierMode.Percent,
                now);
        }

        if (TryGetBerserkerHook("B-4-4", out ResolvedTalentEventHook recklessness))
        {
            bool active = playerHpPercent < recklessness.Threshold;
            SyncStatEffect(
                _player.Actor,
                RecklessnessOutgoingEffectId,
                active,
                EffectStat.OutgoingPhysicalDamageMultiplier,
                1 + recklessness.Value / 100m,
                EffectModifierMode.Multiplicative,
                now);
            SyncStatEffect(
                _player.Actor,
                RecklessnessIncomingEffectId,
                active,
                EffectStat.IncomingDamageMultiplier,
                1 + recklessness.SecondaryValue / 100m,
                EffectModifierMode.Multiplicative,
                now);
        }

        if (TryGetBerserkerHook("B-7-2", out ResolvedTalentEventHook deathStrength))
        {
            SyncStatEffect(
                _player.Actor,
                DeathStrengthCriticalEffectId,
                playerHpPercent < deathStrength.Threshold,
                EffectStat.CriticalChance,
                deathStrength.Value,
                EffectModifierMode.Flat,
                now);
        }

        if (!_deathsEmbraceArmed
            && !_deathsEmbraceConsumed
            && TryGetBerserkerHook("B-8-3", out ResolvedTalentEventHook embrace)
            && playerHpPercent < embrace.Threshold)
        {
            _deathsEmbraceArmed = true;
            ApplyTalentEffect(
                _player.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    DeathsEmbraceReadyEffectId,
                    EffectKind.Buff,
                    ConditionalEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    0),
                now);
        }
    }

    private void SyncStatEffect(
        CombatActorState target,
        string effectId,
        bool active,
        EffectStat stat,
        decimal magnitude,
        EffectModifierMode mode,
        DateTimeOffset now)
    {
        ActiveEffect? existing = target.ActiveEffects.FirstOrDefault(effect =>
            string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
            && effect.ExpiresAtUtc > now);

        if (active)
        {
            if (existing is not null) return;
            ApplyTalentEffect(
                target,
                _player.Actor.ActorId,
                new EffectDefinition(
                    effectId,
                    EffectKind.StatModifier,
                    ConditionalEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    magnitude,
                    ModifiedStat: stat,
                    ModifierMode: mode),
                now);
            return;
        }

        if (existing is not null)
        {
            RemoveTalentEffects(EffectEngine.Remove(target, effectId, now));
        }
    }

    private void ApplyTalentEffect(
        CombatActorState target,
        Guid sourceId,
        EffectDefinition effect,
        DateTimeOffset now)
    {
        ApplyKernelEvents(
            EffectEngine.Apply(target, sourceId, effect, now),
            sourceId,
            target.ActorId,
            effect.Id);
    }

    private void RemoveTalentEffects(IEnumerable<CombatEvent> events)
    {
        ApplyKernelEvents(
            events,
            _player.Actor.ActorId,
            _player.Actor.ActorId,
            null);
    }

    private TimeSpan EffectivePlayerAutoAttackInterval(
        AutoAttackProfile profile,
        DateTimeOffset now)
    {
        decimal multiplier = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.AttackSpeed,
            1,
            now);
        double seconds = profile.Interval.TotalSeconds
            / Math.Max(0.1, (double)multiplier);
        return TimeSpan.FromSeconds(Math.Max(0.05, seconds));
    }

    private bool IsBerserkActive(DateTimeOffset now) =>
        _player.Actor.ActiveEffects.Any(effect =>
            string.Equals(
                effect.Definition.Id,
                BerserkAttackPowerEffectId,
                StringComparison.Ordinal)
            && effect.ExpiresAtUtc > now);

    private bool IsWarrior =>
        string.Equals(_player.DefinitionId, "WARRIOR", StringComparison.Ordinal);

    private bool HasBerserkerTalent(string talentId) =>
        IsWarrior
        && _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    private bool TryGetBerserkerHook(
        string talentId,
        out ResolvedTalentEventHook hook)
    {
        hook = IsWarrior
            ? _playerTalents.EventHooks.FirstOrDefault(item =>
                string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!
            : null!;
        return hook is not null;
    }

    private bool TalentCooldownReady(string talentId, DateTimeOffset now) =>
        !_talentInternalCooldowns.TryGetValue(talentId, out DateTimeOffset readyAt)
        || readyAt <= now;

    private void StartTalentCooldown(
        ResolvedTalentEventHook hook,
        DateTimeOffset now)
    {
        if (hook.InternalCooldown > TimeSpan.Zero)
        {
            _talentInternalCooldowns[hook.TalentId] = now + hook.InternalCooldown;
        }
    }

    private static bool ReduceCooldown(
        CombatRuntimeState runtime,
        string abilityId,
        TimeSpan reduction,
        DateTimeOffset now)
    {
        if (!runtime.Cooldowns.TryGetValue(abilityId, out DateTimeOffset readyAt)
            || readyAt <= now
            || reduction <= TimeSpan.Zero)
        {
            return false;
        }

        DateTimeOffset reduced = readyAt - reduction;
        if (reduced <= now)
        {
            runtime.Cooldowns.Remove(abilityId);
        }
        else
        {
            runtime.Cooldowns[abilityId] = reduced;
        }

        return true;
    }

    private static bool IsAttackingAbility(AbilityDefinition ability) =>
        ability.Actions?.Any(action => action.Type == AbilityActionType.Damage) == true;

    private static decimal HpPercent(CombatActorState actor) =>
        actor.MaxHp <= 0 ? 0 : actor.CurrentHp / actor.MaxHp * 100m;
}
