using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string GuardianStanceDamageEffectId = "GUARDIAN_STANCE_DAMAGE";
    private const string GuardianOneHandDamageEffectId = "GUARDIAN_ONE_HAND_DAMAGE";
    private const string GuardianLastStandActiveEffectId = "GUARDIAN_LAST_STAND_ACTIVE";
    private const string GuardianLastStandMaxHpSourceId = "GUARDIAN_LAST_STAND_MAX_HP";
    private const string GuardianRevengeWindowEffectId = "GUARDIAN_REVENGE_WINDOW";
    private const string GuardianCapstoneShieldBlockEffectId = "GUARDIAN_CAPSTONE_SHIELD_BLOCK";
    private const string GuardianLowHpBlockMinEffectId = "GUARDIAN_LAST_FORTRESS_MIN";
    private const string GuardianLowHpBlockMaxEffectId = "GUARDIAN_LAST_FORTRESS_MAX";
    private const string GuardianHeavyShieldMinEffectId = "GUARDIAN_HEAVY_SHIELD_MIN";
    private const string GuardianHeavyShieldMaxEffectId = "GUARDIAN_HEAVY_SHIELD_MAX";
    private const string GuardianHoldLineEffectId = "GUARDIAN_HOLD_THE_LINE";
    private const decimal GuardianRevengeBaseThreat = 120m;
    private const decimal GuardianSunderBaseThreat = 150m;
    private const decimal GuardianShieldSlamBaseThreat = 250m;
    private readonly Dictionary<(Guid PlayerId, Guid EnemyId), DateTimeOffset> _guardianProvokeThreatWindows = [];

    private bool IsGuardian =>
        string.Equals(_player.DefinitionId, "WARRIOR", StringComparison.Ordinal)
        && (_playerTalents.EventHooks.Any(hook => hook.TalentId.StartsWith("G-", StringComparison.Ordinal))
            || _playerTalents.UnlockedAbilityIds.Any(abilityId =>
                abilityId is "LAST_STAND"
                    or "REVENGE"
                    or "SHIELD_BLOCK"
                    or "PROVOKE"
                    or "SUNDER_ARMOR"
                    or "CONCUSSION_BLOW"
                    or "BASTION"
                    or "CHALLENGING_SHOUT"
                    or "SHIELD_SLAM"));

    private bool GuardianHasShieldProfile =>
        _player.Actor.Stats.BlockValueMax > 0
        || _player.Actor.Stats.BlockChance > 0;

    private void ApplyGuardianStartingEffects(DateTimeOffset now)
    {
        if (!IsGuardian)
            return;

        if (GetGuardianHook("G-1-5") is { } criticalReduction)
        {
            _player.Actor.IncomingCriticalDamageReductionPercent = criticalReduction.Value;
        }

        if (GuardianHasShieldProfile && HasGuardianTalent("G-1-1"))
        {
            ApplyGuardianEffect(
                _player.Actor,
                GuardianStanceDamageEffectId,
                EffectStat.OutgoingDamageMultiplier,
                0.95m,
                now,
                EffectModifierMode.Multiplicative,
                TimeSpan.FromDays(30));
        }

        if (GuardianHasShieldProfile && GetGuardianHook("G-4-3") is { } oneHanded)
        {
            ApplyGuardianEffect(
                _player.Actor,
                GuardianOneHandDamageEffectId,
                EffectStat.OutgoingDamageMultiplier,
                1 + oneHanded.Value / 100m,
                now,
                EffectModifierMode.Multiplicative,
                TimeSpan.FromDays(30));
        }

        SyncGuardianConditionalEffects(now);
    }

    private void ApplyGuardianAbilityHooks(CombatEvent combatEvent)
    {
        if (!IsGuardian || combatEvent.DefinitionId is null)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        Guid? targetActorId = combatEvent.TargetActorId;

        switch (combatEvent.DefinitionId)
        {
            case "LAST_STAND":
                SyncGuardianLastStand(now);
                break;

            case "REVENGE":
                if (targetActorId is { } revengeTarget)
                {
                    decimal threatBonus = 1 + (GetGuardianHook("G-2-3")?.Value ?? 0) / 100m;
                    AddGuardianFlatThreat(
                        revengeTarget,
                        GuardianRevengeBaseThreat * threatBonus,
                        now);
                    TryApplyGuardianRevengeStun(revengeTarget, now);
                }
                break;

            case "SHIELD_BLOCK":
                ApplyGuardianShieldBlockUpgrades(now);
                ApplyGuardianPartyMitigationForDefensiveWindow(
                    now,
                    ResolveGuardianShieldBlockRemainingDuration(now));
                break;

            case "PROVOKE":
                if (targetActorId is { } provokeTarget
                    && GetGuardianHook("G-4-5") is { } masterProvoke)
                {
                    _guardianProvokeThreatWindows[(_player.Actor.ActorId, provokeTarget)] =
                        now + (masterProvoke.Duration > TimeSpan.Zero
                            ? masterProvoke.Duration
                            : TimeSpan.FromSeconds(4));
                }
                break;

            case "SUNDER_ARMOR":
                if (targetActorId is { } sunderTarget)
                {
                    decimal threatBonus = 1 + (GetGuardianHook("G-3-6")?.Value ?? 0) / 100m;
                    AddGuardianFlatThreat(
                        sunderTarget,
                        GuardianSunderBaseThreat * threatBonus,
                        now);
                }
                break;

            case "CONCUSSION_BLOW":
                if (targetActorId is { } concussionTarget)
                    TryApplyGuardianConcussionStun(concussionTarget, now);
                break;

            case "SHIELD_BASH":
                if (targetActorId is { } bashTarget
                    && GetGuardianHook("G-4-2") is { } improvedBash)
                {
                    ApplyGuardianControlEffect(
                        bashTarget,
                        "GUARDIAN_IMPROVED_SHIELD_BASH",
                        EffectKind.Silence,
                        TimeSpan.FromSeconds((double)improvedBash.Value),
                        now);
                }
                break;

            case "BASTION":
                ApplyGuardianPartyMitigationForDefensiveWindow(
                    now,
                    TimeSpan.FromSeconds(6));
                break;

            case "CHALLENGING_SHOUT":
                foreach (CombatParticipantDefinition enemy in _enemies.Where(item => !item.Actor.IsDead))
                {
                    _enemyForcedTargets[enemy.Actor.ActorId].Set(
                        _player.Actor.ActorId,
                        now,
                        TimeSpan.FromSeconds(4));
                }
                break;

            case "SHIELD_SLAM":
                if (targetActorId is { } shieldSlamTarget)
                {
                    decimal threatBonus = 1 + (GetGuardianHook("G-6-2")?.Value ?? 0) / 100m;
                    AddGuardianFlatThreat(
                        shieldSlamTarget,
                        GuardianShieldSlamBaseThreat * threatBonus,
                        now);
                }
                break;
        }

        SyncGuardianConditionalEffects(now);
    }

    private void ApplyGuardianDamageTakenHooks(CombatEvent combatEvent)
    {
        if (!IsGuardian
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.Amount <= 0
            || combatEvent.IsPeriodic)
        {
            return;
        }

        SyncGuardianConditionalEffects(combatEvent.OccurredAtUtc);
    }

    private void ApplyGuardianBlockHooks(CombatEvent combatEvent)
    {
        if (!IsGuardian
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.SourceActorId is not { } enemyActorId
            || combatEvent.Amount <= 0
            || !_enemyThreatTables.TryGetValue(enemyActorId, out ThreatTable? threatTable))
        {
            return;
        }

        if (combatEvent.AmountBeforeShields <= 0
            && string.Equals(_player.ResourceType, "RAGE", StringComparison.Ordinal))
        {
            AddResource(
                _player.Actor,
                BaseRageFromDirectDamageTaken,
                combatEvent.OccurredAtUtc,
                "FULL_BLOCK");
        }

        if (GetGuardianHook("G-2-5") is { } shieldFury)
        {
            AddResource(
                _player.Actor,
                shieldFury.Value,
                combatEvent.OccurredAtUtc,
                shieldFury.TalentId);
        }

        threatTable.AddThreat(
            _player.Actor.ActorId,
            combatEvent.Amount,
            GuardianThreatMultiplierForTarget(enemyActorId, combatEvent.OccurredAtUtc));

        if (_playerTalents.UnlockedAbilityIds.Contains("REVENGE"))
        {
            ApplyGuardianEffect(
                _player.Actor,
                GuardianRevengeWindowEffectId,
                null,
                1,
                combatEvent.OccurredAtUtc,
                EffectModifierMode.Flat,
                TimeSpan.FromSeconds(5),
                EffectKind.Buff);
        }

        if (HasGuardianTalent("G-6-5"))
        {
            ReduceGuardianCooldown(
                "SHIELD_SLAM",
                TimeSpan.FromSeconds(1),
                combatEvent.OccurredAtUtc);
        }
    }

    private static void ApplyGuardianDodgeHooks(CombatEvent combatEvent)
    {
        _ = combatEvent;
    }

    private static void ApplyGuardianCriticalHooks(CombatEvent combatEvent)
    {
        _ = combatEvent;
    }

    private static void ApplyGuardianAutoAttackHooks(CombatEvent combatEvent)
    {
        _ = combatEvent;
    }

    private void ApplyGuardianShieldBlockUpgrades(DateTimeOffset now)
    {
        TimeSpan duration = ResolveGuardianShieldBlockRemainingDuration(now);
        if (duration <= TimeSpan.Zero)
            duration = TimeSpan.FromSeconds(5);

        if (GetGuardianHook("G-4-4") is { } heavyShield)
        {
            decimal percent = heavyShield.Value / 100m;
            ApplyGuardianEffect(
                _player.Actor,
                GuardianHeavyShieldMinEffectId,
                EffectStat.BlockValueMin,
                percent,
                now,
                EffectModifierMode.Percent,
                duration);
            ApplyGuardianEffect(
                _player.Actor,
                GuardianHeavyShieldMaxEffectId,
                EffectStat.BlockValueMax,
                percent,
                now,
                EffectModifierMode.Percent,
                duration);
        }

        if (HasGuardianTalent("G-6-5"))
        {
            ApplyGuardianEffect(
                _player.Actor,
                GuardianCapstoneShieldBlockEffectId,
                null,
                1,
                now,
                EffectModifierMode.Flat,
                duration,
                EffectKind.Buff);
        }
    }

    private void ApplyGuardianPartyMitigationForDefensiveWindow(
        DateTimeOffset now,
        TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero
            || GetGuardianHook("G-5-5") is not { } holdLine)
        {
            return;
        }

        decimal multiplier = Math.Max(0, 1 - holdLine.Value / 100m);
        foreach (CombatPlayerRuntimeState ally in _playerStatesByActorId.Values.Where(state =>
                     state.Definition.Actor.ActorId != _player.Actor.ActorId
                     && !state.Definition.Actor.IsDead))
        {
            ApplyGuardianEffect(
                ally.Definition.Actor,
                $"{GuardianHoldLineEffectId}_{_player.Actor.ActorId:N}",
                EffectStat.IncomingDamageMultiplier,
                multiplier,
                now,
                EffectModifierMode.Multiplicative,
                duration);
        }
    }

    private void TryApplyGuardianRevengeStun(Guid targetActorId, DateTimeOffset now)
    {
        if (GetGuardianHook("G-2-3") is not { } improvedRevenge
            || improvedRevenge.SecondaryValue <= 0
            || _random.NextUnit() >= improvedRevenge.SecondaryValue / 100m)
        {
            return;
        }

        if (_enemiesById.TryGetValue(targetActorId, out CombatParticipantDefinition? target)
            && target.MonsterRank != MonsterRank.Boss)
        {
            ApplyGuardianControlEffect(
                targetActorId,
                "GUARDIAN_IMPROVED_REVENGE_STUN",
                EffectKind.Stun,
                TimeSpan.FromSeconds(1),
                now);
        }
    }

    private void TryApplyGuardianConcussionStun(Guid targetActorId, DateTimeOffset now)
    {
        if (_enemiesById.TryGetValue(targetActorId, out CombatParticipantDefinition? target)
            && target.MonsterRank != MonsterRank.Boss)
        {
            ApplyGuardianControlEffect(
                targetActorId,
                "GUARDIAN_CONCUSSION_BLOW_STUN",
                EffectKind.Stun,
                TimeSpan.FromSeconds(3),
                now);
        }
    }

    private void ApplyGuardianControlEffect(
        Guid targetActorId,
        string effectId,
        EffectKind kind,
        TimeSpan duration,
        DateTimeOffset now)
    {
        if (duration <= TimeSpan.Zero
            || !_enemiesById.TryGetValue(targetActorId, out CombatParticipantDefinition? target)
            || target.Actor.IsDead)
        {
            return;
        }

        ApplyKernelEvents(
            EffectEngine.Apply(
                target.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    effectId,
                    kind,
                    duration,
                    1,
                    EffectStackPolicy.Replace,
                    1,
                    DispelCategory: "CONTROL"),
                now),
            _player.Actor.ActorId,
            targetActorId,
            effectId);
    }

    private void AddGuardianFlatThreat(
        Guid targetActorId,
        decimal amount,
        DateTimeOffset now)
    {
        if (amount <= 0
            || !_enemyThreatTables.TryGetValue(targetActorId, out ThreatTable? threatTable))
        {
            return;
        }

        threatTable.AddThreat(
            _player.Actor.ActorId,
            amount,
            GuardianThreatMultiplierForTarget(targetActorId, now));
    }

    private TimeSpan ResolveGuardianShieldBlockRemainingDuration(DateTimeOffset now)
    {
        DateTimeOffset? expiresAt = _player.Actor.ActiveEffects
            .Where(effect =>
                effect.ExpiresAtUtc > now
                && string.Equals(
                    effect.Definition.Id,
                    "GUARDIAN_SHIELD_BLOCK_CHANCE",
                    StringComparison.Ordinal))
            .Select(effect => (DateTimeOffset?)effect.ExpiresAtUtc)
            .Max();
        return expiresAt is null ? TimeSpan.Zero : expiresAt.Value - now;
    }

    private void SyncGuardianConditionalEffects(DateTimeOffset now)
    {
        if (!IsGuardian)
            return;

        SyncGuardianLastStand(now);
        decimal hpPercent = _player.Actor.MaxHp <= 0
            ? 0
            : _player.Actor.CurrentHp / _player.Actor.MaxHp * 100m;

        if (GetGuardianHook("G-6-4") is { } lastFortress
            && hpPercent < lastFortress.Threshold)
        {
            decimal percent = lastFortress.Value / 100m;
            ApplyGuardianEffect(
                _player.Actor,
                GuardianLowHpBlockMinEffectId,
                EffectStat.BlockValueMin,
                percent,
                now,
                EffectModifierMode.Percent,
                TimeSpan.FromSeconds(2));
            ApplyGuardianEffect(
                _player.Actor,
                GuardianLowHpBlockMaxEffectId,
                EffectStat.BlockValueMax,
                percent,
                now,
                EffectModifierMode.Percent,
                TimeSpan.FromSeconds(2));
        }
        else
        {
            RemoveGuardianEffect(GuardianLowHpBlockMinEffectId, now);
            RemoveGuardianEffect(GuardianLowHpBlockMaxEffectId, now);
        }

        foreach ((Guid PlayerId, Guid EnemyId) key in _guardianProvokeThreatWindows
                     .Where(pair => pair.Value <= now)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _guardianProvokeThreatWindows.Remove(key);
        }
    }

    private void SyncGuardianLastStand(DateTimeOffset now)
    {
        bool active = HasActiveGuardianEffect(
            _player.Actor,
            GuardianLastStandActiveEffectId,
            now);
        if (!active)
        {
            _player.Actor.RemoveTemporaryMaxHpPercentBonus(GuardianLastStandMaxHpSourceId);
            return;
        }

        decimal bonusPercent = 20 + (GetGuardianHook("G-5-4")?.Value ?? 0);
        _player.Actor.SetTemporaryMaxHpPercentBonus(
            GuardianLastStandMaxHpSourceId,
            bonusPercent,
            healByIncrease: true);
    }

    private void ReduceGuardianCooldown(
        string abilityId,
        TimeSpan reduction,
        DateTimeOffset now)
    {
        if (!_playerRuntime.Cooldowns.TryGetValue(abilityId, out DateTimeOffset readyAt)
            || readyAt <= now)
        {
            return;
        }

        DateTimeOffset reduced = readyAt - reduction;
        if (reduced <= now)
            _playerRuntime.Cooldowns.Remove(abilityId);
        else
            _playerRuntime.Cooldowns[abilityId] = reduced;
    }

    private ResolvedTalentEventHook? GetGuardianHook(string talentId) =>
        _playerTalents.EventHooks.FirstOrDefault(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    private bool HasGuardianTalent(string talentId) =>
        GetGuardianHook(talentId) is not null;

    private static bool HasActiveGuardianEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        actor.ActiveEffects.Any(effect =>
            effect.ExpiresAtUtc > now
            && string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal));

    private void ApplyGuardianEffect(
        CombatActorState target,
        string effectId,
        EffectStat? stat,
        decimal magnitude,
        DateTimeOffset now,
        EffectModifierMode mode,
        TimeSpan duration,
        EffectKind kind = EffectKind.StatModifier)
    {
        if (duration <= TimeSpan.Zero)
            return;

        EffectDefinition definition = new(
            effectId,
            kind,
            duration,
            1,
            EffectStackPolicy.Replace,
            Math.Max(0, magnitude),
            ModifiedStat: stat,
            ModifierMode: mode,
            DispelCategory: "GUARDIAN");
        ApplyKernelEvents(
            EffectEngine.Apply(target, _player.Actor.ActorId, definition, now),
            _player.Actor.ActorId,
            target.ActorId,
            effectId);
    }

    private void RemoveGuardianEffect(string effectId, DateTimeOffset now)
    {
        ApplyKernelEvents(
            EffectEngine.Remove(_player.Actor, effectId, now),
            _player.Actor.ActorId,
            _player.Actor.ActorId,
            effectId);
    }

    private decimal GuardianRageMultiplier => 1m;

    private decimal GuardianThreatMultiplier =>
        GuardianThreatMultiplierForTarget(null, CurrentTimeUtc);

    private decimal GuardianAutoAttackThreatMultiplier => GuardianThreatMultiplier;

    private decimal GuardianThreatMultiplierForTarget(
        Guid? enemyActorId,
        DateTimeOffset now)
    {
        if (!IsGuardian || !GuardianHasShieldProfile || !HasGuardianTalent("G-1-1"))
            return 1m;

        decimal multiplier = 1.30m;
        multiplier *= 1 + (GetGuardianHook("G-2-4")?.Value ?? 0) / 100m;

        if (HasGuardianTalent("G-6-5")
            && HasActiveGuardianEffect(_player.Actor, "BASTION_GUARD", now))
        {
            multiplier *= 1.30m;
        }

        if (enemyActorId is { } targetId
            && _guardianProvokeThreatWindows.TryGetValue(
                (_player.Actor.ActorId, targetId),
                out DateTimeOffset provokeEndsAt)
            && provokeEndsAt > now)
        {
            multiplier *= 1 + (GetGuardianHook("G-4-5")?.Value ?? 0) / 100m;
        }

        return multiplier;
    }

    private decimal? GetGuardianHookValue(string talentId) =>
        string.Equals(talentId, "G-4-2", StringComparison.Ordinal)
            ? null
            : GetGuardianHook(talentId)?.Value;
}
