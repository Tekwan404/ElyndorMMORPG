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
    private const string ExecutionerEffectId = "BERSERKER_EXECUTIONER";
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
            && IsAttackingAbility(ability)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                frenzy.TalentId,
                out BerserkerTalentRuntimeRule frenzyRule))
        {
            decimal reduction = frenzyRule.ValueForRank(frenzy.Rank) / 100m;
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

    private void OnPlayerAbilitySucceeded(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        Guid targetActorId,
        DateTimeOffset now)
    {
        if (Status != CombatSessionStatus.Active) return;

        if (ability.ResourceCost >= 20
            && TryGetBerserkerHook("B-2-4", out ResolvedTalentEventHook momentum)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                momentum.TalentId,
                out BerserkerTalentRuntimeRule momentumRule))
        {
            ApplyTalentEffect(
                _player.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    MomentumAttackSpeedEffectId,
                    EffectKind.StatModifier,
                    momentumRule.Duration ?? TimeSpan.FromSeconds(3),
                    1,
                    EffectStackPolicy.Refresh,
                    momentumRule.ValueForRank(momentum.Rank) / 100m,
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
            ApplyDeathWhirlwind(execution, targetActorId, now);
            ApplyRendingRampage(targetActorId, now);
        }

        SyncBerserkerConditionalEffects(now);
    }

    private void ApplyBerserkerCriticalHooks(CombatEvent combatEvent)
    {
        if (combatEvent.SourceActorId != _player.Actor.ActorId) return;
        DateTimeOffset now = combatEvent.OccurredAtUtc;

        if (string.Equals(combatEvent.DefinitionId, "WILD_STRIKE", StringComparison.Ordinal)
            && !_enemy.Actor.IsDead
            && TryGetBerserkerHook("B-3-4", out ResolvedTalentEventHook bloodTrail)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                bloodTrail.TalentId,
                out BerserkerTalentRuntimeRule bloodTrailRule))
        {
            decimal attackPower = EffectEngine.CalculateStat(
                _player.Actor,
                EffectStat.AttackPower,
                _player.Actor.Stats.AttackPower,
                now);
            decimal tickDamage =
                attackPower * bloodTrailRule.ValueForRank(bloodTrail.Rank) / 100m;
            ApplyTalentEffect(
                _enemy.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    BloodTrailEffectId,
                    EffectKind.DamageOverTime,
                    bloodTrailRule.Duration ?? TimeSpan.FromSeconds(4),
                    1,
                    EffectStackPolicy.Refresh,
                    tickDamage,
                    bloodTrailRule.TickInterval ?? TimeSpan.FromSeconds(1)),
                now);
        }

        if (string.Equals(combatEvent.DefinitionId, "AUTO_ATTACK", StringComparison.Ordinal)
            && !_enemy.Actor.IsDead
            && TryGetBerserkerHook("B-6-2", out ResolvedTalentEventHook devastating)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                devastating.TalentId,
                out BerserkerTalentRuntimeRule devastatingRule))
        {
            ApplyTalentEffect(
                _enemy.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    DevastatingVulnerabilityEffectId,
                    EffectKind.StatModifier,
                    devastatingRule.Duration ?? TimeSpan.FromSeconds(8),
                    1,
                    EffectStackPolicy.Refresh,
                    1 + devastatingRule.ValueForRank(devastating.Rank) / 100m,
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
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                avatar.TalentId,
                out BerserkerTalentRuntimeRule avatarRule)
            && _random.NextUnit() < avatarRule.ChancePercent / 100m)
        {
            AddResource(
                _player.Actor,
                avatarRule.ValueForRank(avatar.Rank),
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
        DateTimeOffset now)
    {
        SyncBerserkerConditionalEffects(now);

        decimal attackPower = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.AttackPower,
            _player.Actor.Stats.AttackPower,
            now);
        decimal baseDamage = _player.AutoAttack.BaseDamage
            + attackPower * _player.AutoAttack.AttackPowerCoefficient;

        bool consumeDeathsEmbrace = _deathsEmbraceArmed && !_deathsEmbraceConsumed;
        decimal deathsEmbraceMultiplier = 1;
        if (consumeDeathsEmbrace)
        {
            _deathsEmbraceArmed = false;
            _deathsEmbraceConsumed = true;
            if (TryGetBerserkerHook("B-8-3", out ResolvedTalentEventHook embrace)
                && BerserkerTalentRuntimeCatalog.TryGetRule(
                    embrace.TalentId,
                    out BerserkerTalentRuntimeRule embraceRule))
            {
                deathsEmbraceMultiplier =
                    embraceRule.ValueForRank(embrace.Rank) / 100m;
            }

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
                DamageMultiplier: deathsEmbraceMultiplier,
                ForceCritical: consumeDeathsEmbrace),
            _random,
            now);
        ApplyKernelEvents(
            damage.Events,
            _player.Actor.ActorId,
            target.Actor.ActorId,
            "AUTO_ATTACK");

        if (damage.Avoidance == DamageAvoidance.None
            && damage.HpDamage > 0
            && _player.AutoAttack.ResourceOnHit > 0)
        {
            AddResource(
                _player.Actor,
                _player.AutoAttack.ResourceOnHit,
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
            && TryGetBerserkerHook("B-7-1", out ResolvedTalentEventHook unstoppable)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                unstoppable.TalentId,
                out BerserkerTalentRuntimeRule unstoppableRule))
        {
            ResolveSecondaryAutoAttack(
                target,
                baseDamage,
                unstoppableRule.ValueForRank(unstoppable.Rank) / 100m,
                unstoppable.TalentId,
                now);
            return;
        }

        if (TryGetBerserkerHook("B-4-1", out ResolvedTalentEventHook doubleStrike)
            && TalentCooldownReady(doubleStrike.TalentId, now)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                doubleStrike.TalentId,
                out BerserkerTalentRuntimeRule doubleStrikeRule)
            && _random.NextUnit() < doubleStrikeRule.ChancePercent / 100m)
        {
            ResolveSecondaryAutoAttack(
                target,
                baseDamage,
                doubleStrikeRule.ValueForRank(doubleStrike.Rank) / 100m,
                doubleStrike.TalentId,
                now);
            StartTalentCooldown(doubleStrike, now);
        }
    }

    private void ResolveSecondaryAutoAttack(
        CombatParticipantDefinition target,
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
                CanCrit: false),
            _random,
            now);
        ApplyKernelEvents(
            secondary.Events,
            _player.Actor.ActorId,
            target.Actor.ActorId,
            definitionId);
    }

    private void ApplyDeathWhirlwind(
        AbilityExecutionResult execution,
        Guid targetActorId,
        DateTimeOffset now)
    {
        if (Status != CombatSessionStatus.Active
            || _enemy.Actor.IsDead
            || !TryGetBerserkerHook("B-8-1", out ResolvedTalentEventHook deathWhirlwind)
            || !BerserkerTalentRuntimeCatalog.TryGetRule(
                deathWhirlwind.TalentId,
                out BerserkerTalentRuntimeRule deathWhirlwindRule))
        {
            return;
        }

        CombatEvent? physical = execution.Events.FirstOrDefault(item =>
            item.Type == CombatEventType.DamageDealt
            && item.TargetActorId == targetActorId);
        if (physical is null || physical.AmountBeforeShields <= 0) return;

        DamageResult extra = DamagePipeline.Resolve(
            new DamageRequest(
                _player.Actor,
                _enemy.Actor,
                physical.AmountBeforeShields
                    * deathWhirlwindRule.ValueForRank(deathWhirlwind.Rank)
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
            _enemy.Actor.ActorId,
            "B-8-1");
    }

    private void ApplyRendingRampage(Guid targetActorId, DateTimeOffset now)
    {
        if (targetActorId != _enemy.Actor.ActorId
            || _enemy.Actor.IsDead
            || Status != CombatSessionStatus.Active
            || !TryGetBerserkerHook("B-7-3", out ResolvedTalentEventHook rending)
            || !BerserkerTalentRuntimeCatalog.TryGetRule(
                rending.TalentId,
                out BerserkerTalentRuntimeRule rendingRule))
        {
            return;
        }

        decimal attackPower = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.AttackPower,
            _player.Actor.Stats.AttackPower,
            now);
        decimal tickDamage =
            attackPower * rendingRule.ValueForRank(rending.Rank) / 100m;
        ApplyTalentEffect(
            _enemy.Actor,
            _player.Actor.ActorId,
            new EffectDefinition(
                RendingRampageEffectId,
                EffectKind.DamageOverTime,
                rendingRule.Duration ?? TimeSpan.FromSeconds(6),
                1,
                EffectStackPolicy.Refresh,
                tickDamage,
                rendingRule.TickInterval ?? TimeSpan.FromSeconds(1)),
            now);
    }

    private void SyncBerserkerConditionalEffects(DateTimeOffset now)
    {
        if (Status != CombatSessionStatus.Active || _player.Actor.IsDead) return;

        decimal playerHpPercent = HpPercent(_player.Actor);
        decimal enemyHpPercent = HpPercent(_enemy.Actor);

        if (TryGetBerserkerHook("B-2-1", out ResolvedTalentEventHook bloodRage)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                bloodRage.TalentId,
                out BerserkerTalentRuntimeRule bloodRageRule))
        {
            bool active = playerHpPercent < bloodRageRule.Threshold;
            SyncStatEffect(
                _player.Actor,
                BloodRageAttackPowerEffectId,
                active,
                EffectStat.AttackPower,
                bloodRageRule.ValueForRank(bloodRage.Rank) / 100m,
                EffectModifierMode.Percent,
                now);
            SyncStatEffect(
                _player.Actor,
                BloodRageAttackSpeedEffectId,
                active,
                EffectStat.AttackSpeed,
                bloodRageRule.SecondaryValueForRank(bloodRage.Rank) / 100m,
                EffectModifierMode.Percent,
                now);
        }

        if (TryGetBerserkerHook("B-4-4", out ResolvedTalentEventHook recklessness)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                recklessness.TalentId,
                out BerserkerTalentRuntimeRule recklessnessRule))
        {
            bool active = playerHpPercent < recklessnessRule.Threshold;
            SyncStatEffect(
                _player.Actor,
                RecklessnessOutgoingEffectId,
                active,
                EffectStat.OutgoingPhysicalDamageMultiplier,
                1 + recklessnessRule.ValueForRank(recklessness.Rank) / 100m,
                EffectModifierMode.Multiplicative,
                now);
            SyncStatEffect(
                _player.Actor,
                RecklessnessIncomingEffectId,
                active,
                EffectStat.IncomingDamageMultiplier,
                1 + recklessnessRule.SecondaryValueForRank(recklessness.Rank) / 100m,
                EffectModifierMode.Multiplicative,
                now);
        }

        if (TryGetBerserkerHook("B-7-2", out ResolvedTalentEventHook deathStrength)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                deathStrength.TalentId,
                out BerserkerTalentRuntimeRule deathStrengthRule))
        {
            SyncStatEffect(
                _player.Actor,
                DeathStrengthCriticalEffectId,
                playerHpPercent < deathStrengthRule.Threshold,
                EffectStat.CriticalChance,
                deathStrengthRule.ValueForRank(deathStrength.Rank),
                EffectModifierMode.Flat,
                now);
        }

        if (TryGetBerserkerHook("B-7-4", out ResolvedTalentEventHook executioner)
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                executioner.TalentId,
                out BerserkerTalentRuntimeRule executionerRule))
        {
            SyncStatEffect(
                _player.Actor,
                ExecutionerEffectId,
                enemyHpPercent < executionerRule.Threshold && !_enemy.Actor.IsDead,
                EffectStat.OutgoingPhysicalDamageMultiplier,
                1 + executionerRule.ValueForRank(executioner.Rank) / 100m,
                EffectModifierMode.Multiplicative,
                now);
        }

        if (!_deathsEmbraceArmed
            && !_deathsEmbraceConsumed
            && HasBerserkerTalent("B-8-3")
            && BerserkerTalentRuntimeCatalog.TryGetRule(
                "B-8-3",
                out BerserkerTalentRuntimeRule embraceRule)
            && playerHpPercent < embraceRule.Threshold)
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

    private TimeSpan EffectivePlayerAutoAttackInterval(DateTimeOffset now)
    {
        decimal multiplier = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.AttackSpeed,
            1,
            now);
        double seconds = _player.AutoAttack.Interval.TotalSeconds
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

    private bool HasBerserkerTalent(string talentId) =>
        _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    private bool TryGetBerserkerHook(
        string talentId,
        out ResolvedTalentEventHook hook)
    {
        hook = _playerTalents.EventHooks.FirstOrDefault(item =>
            string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!;
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
