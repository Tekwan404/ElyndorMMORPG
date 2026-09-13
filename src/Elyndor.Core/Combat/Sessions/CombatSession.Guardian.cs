using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string GuardianFirstLineEffectId = "GUARDIAN_FIRST_LINE";
    private const string GuardianLastStandEffectId = "GUARDIAN_LAST_STAND";
    private const string GuardianBloodArmorEffectId = "GUARDIAN_BLOOD_ARMOR";
    private const string GuardianBastionBlockEffectId = "GUARDIAN_UNBREAKABLE_BASTION";
    private const string GuardianRevengeWindowEffectId = "GUARDIAN_REVENGE_WINDOW";
    private const string GuardianCapstoneShieldBlockEffectId = "GUARDIAN_CAPSTONE_SHIELD_BLOCK";
    private const decimal GuardianShieldSlamBonusThreat = 200m;
    private bool _guardianEmergencyTriggered;

    private bool IsGuardian =>
        string.Equals(_player.DefinitionId, "WARRIOR", StringComparison.Ordinal)
        && (_playerTalents.EventHooks.Any(hook => hook.TalentId.StartsWith("G-", StringComparison.Ordinal))
            || _playerTalents.UnlockedAbilityIds.Any(abilityId =>
                abilityId is "PROVOKE" or "REVENGE" or "SHIELD_SLAM" or "SHIELD_BLOCK" or "BASTION"));

    private void ApplyGuardianStartingEffects(DateTimeOffset now)
    {
        if (!IsGuardian)
            return;

        if (GetGuardianHook("G-3-4") is { } criticalReduction)
        {
            _player.Actor.IncomingCriticalDamageReductionPercent = criticalReduction.Value;
        }
        decimal controlReduction =
            (GetGuardianHook("G-3-2")?.Value ?? 0)
            + (GetGuardianHook("G-6-3")?.Value ?? 0);
        _player.Actor.IncomingControlDurationMultiplier =
            Math.Max(0, 1 - controlReduction / 100m);
        _player.Actor.OwnShieldMagnitudeMultiplier =
            1 + (GetGuardianHook("G-6-4")?.Value ?? 0) / 100m;

        if (HasGuardianTalent("G-7-2"))
        {
            EffectEngine.Apply(
                _player.Actor,
                _player.Actor.ActorId,
                new EffectDefinition(
                    "GUARDIAN_SHIELD_OF_ETERNITY",
                    EffectKind.LethalDamagePrevention,
                    TimeSpan.FromDays(1),
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now);
        }

        SyncGuardianConditionalEffects(now);
    }

    private void ApplyGuardianAbilityHooks(CombatEvent combatEvent)
    {
        if (!IsGuardian || combatEvent.DefinitionId is null)
            return;

        _ = PublishGuardianEvent(
            TalentModifierKeys.OnAbilityUsed,
            combatEvent,
            hook => hook.TalentId is "G-3-2" or "G-6-3" or "G-8-2");

        if (string.Equals(combatEvent.DefinitionId, "SHIELD_SLAM", StringComparison.Ordinal)
            && combatEvent.TargetActorId is { } shieldSlamTarget
            && _enemyThreatTables.TryGetValue(shieldSlamTarget, out ThreatTable? shieldSlamThreat))
        {
            shieldSlamThreat.AddThreat(
                _player.Actor.ActorId,
                GuardianShieldSlamBonusThreat,
                GuardianThreatMultiplier);
        }

        if (string.Equals(combatEvent.DefinitionId, "SHIELD_BLOCK", StringComparison.Ordinal)
            && HasGuardianTalent("G-9-1"))
        {
            ApplyGuardianEffect(
                _player.Actor,
                GuardianCapstoneShieldBlockEffectId,
                null,
                1,
                combatEvent.OccurredAtUtc,
                EffectModifierMode.Flat,
                TimeSpan.FromSeconds(5),
                EffectKind.Buff);
        }

        if (string.Equals(combatEvent.DefinitionId, "BASTION", StringComparison.Ordinal))
        {
            if (GetGuardianHook("G-8-2") is { } bastionBlock)
            {
                ApplyGuardianEffect(
                    _player.Actor,
                    GuardianBastionBlockEffectId,
                    EffectStat.BlockChance,
                    bastionBlock.Value,
                    combatEvent.OccurredAtUtc,
                    EffectModifierMode.Flat,
                    TimeSpan.FromSeconds(6));
            }

            if (HasGuardianTalent("G-9-1"))
            {
                foreach (CombatParticipantDefinition enemy in _enemies.Where(item => !item.Actor.IsDead))
                {
                    if (_enemyThreatTables.TryGetValue(enemy.Actor.ActorId, out ThreatTable? threatTable))
                    {
                        RaiseTaunterToTopThreat(threatTable, _player.Actor.ActorId);
                    }
                }
            }
        }

        SyncGuardianConditionalEffects(combatEvent.OccurredAtUtc);
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

        _ = PublishGuardianEvent(
            TalentModifierKeys.OnDamageTaken,
            combatEvent,
            hook => hook.TalentId is "G-3-4" or "G-5-2" or "G-8-3" or "G-9-1");

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
                BaseRageFromDirectDamageTaken * GuardianRageMultiplier,
                combatEvent.OccurredAtUtc,
                "FULL_BLOCK");
        }

        foreach (TalentRuntimeAction action in PublishGuardianEvent(
                     TalentModifierKeys.OnDamageTaken,
                     combatEvent,
                     hook => hook.TalentId is "G-1-2" or "G-4-4"))
        {
            AddResource(
                _player.Actor,
                action.Value,
                combatEvent.OccurredAtUtc,
                action.TalentId);
        }

        threatTable.AddThreat(
            _player.Actor.ActorId,
            combatEvent.Amount,
            GuardianThreatMultiplier);

        if (_playerTalents.UnlockedAbilityIds.Contains("REVENGE"))
        {
            ApplyGuardianEffect(
                _player.Actor,
                GuardianRevengeWindowEffectId,
                null,
                1,
                combatEvent.OccurredAtUtc,
                EffectModifierMode.Flat,
                TimeSpan.FromSeconds(3),
                EffectKind.Buff);
        }

        if (HasGuardianTalent("G-9-1"))
        {
            ReduceGuardianCooldown(
                "SHIELD_SLAM",
                TimeSpan.FromSeconds(1),
                combatEvent.OccurredAtUtc);
        }
    }

    private void ApplyGuardianDodgeHooks(CombatEvent combatEvent)
    {
        // Guardian V2 deliberately has no Dodge-based runtime talents.
        _ = combatEvent;
    }

    private void ApplyGuardianCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsGuardian || combatEvent.TargetActorId != _player.Actor.ActorId)
            return;

        ResolvedTalentEventHook? reflection = GetGuardianHook("G-8-1");
        if (reflection is null || _random.NextUnit() >= reflection.ChancePercent / 100m)
        {
            return;
        }

        if (combatEvent.SourceActorId is not { } sourceId
            || !_enemiesById.TryGetValue(sourceId, out CombatParticipantDefinition? source)
            || source.Actor.IsDead)
        {
            return;
        }

        DamageResult damage = DamagePipeline.Resolve(
            new DamageRequest(
                _player.Actor,
                source.Actor,
                _player.Actor.Stats.AttackPower * reflection.Value / 100m,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            _random,
            combatEvent.OccurredAtUtc);
        ApplyKernelEvents(
            damage.Events,
            _player.Actor.ActorId,
            source.Actor.ActorId,
            "G-8-1");
    }

    private IReadOnlyList<TalentRuntimeAction> PublishGuardianEvent(
        string key,
        CombatEvent combatEvent,
        Func<ResolvedTalentEventHook, bool> filter)
    {
        CombatRuntimeEventKind kind = key switch
        {
            TalentModifierKeys.OnDamageTaken => CombatRuntimeEventKind.DamageTaken,
            TalentModifierKeys.OnDodge => CombatRuntimeEventKind.Dodge,
            TalentModifierKeys.OnAbilityUsed => CombatRuntimeEventKind.AbilityCompleted,
            _ => throw new InvalidOperationException($"Unsupported Guardian event key '{key}'.")
        };
        return _talentRuntimeEngine.Publish(
            ToRuntimeEvent(combatEvent, kind),
            _playerTalents,
            hook => hook.TalentId.StartsWith("G-", StringComparison.Ordinal)
                && string.Equals(hook.Key, key, StringComparison.Ordinal)
                && filter(hook));
    }

    private void ApplyGuardianAutoAttackHooks(CombatEvent combatEvent)
    {
        // Guardian V2 uses Block rather than random auto-attack shields as its
        // reactive defensive loop. Kept as a compatibility hook for CombatSession.
        _ = combatEvent;
    }

    private void SyncGuardianConditionalEffects(DateTimeOffset now)
    {
        if (!IsGuardian)
            return;

        decimal hpPercent = _player.Actor.CurrentHp / _player.Actor.MaxHp * 100m;

        if (GetGuardianHook("G-2-4") is { } firstLine)
        {
            if (hpPercent > firstLine.Threshold)
            {
                ApplyGuardianEffect(
                    _player.Actor,
                    GuardianFirstLineEffectId,
                    EffectStat.IncomingDamageMultiplier,
                    Math.Max(0, 1 - firstLine.Value / 100m),
                    now,
                    EffectModifierMode.Multiplicative,
                    TimeSpan.FromDays(1));
            }
            else
            {
                RemoveGuardianEffect(GuardianFirstLineEffectId, now);
            }
        }

        if (hpPercent < 25
            && !_guardianEmergencyTriggered
            && GetGuardianHook("G-4-1") is { } emergency)
        {
            _guardianEmergencyTriggered = true;
            AddResource(_player.Actor, 15, now, emergency.TalentId);
            ApplyGuardianEffect(
                _player.Actor,
                GuardianLastStandEffectId,
                EffectStat.IncomingDamageMultiplier,
                0.88m,
                now,
                EffectModifierMode.Multiplicative,
                TimeSpan.FromSeconds(6));
        }

        if (GetGuardianHook("G-6-2") is { } bloodArmor)
        {
            decimal armorBonus = Math.Min(
                bloodArmor.SecondaryValue > 0 ? bloodArmor.SecondaryValue : 6,
                Math.Max(0, (_player.Actor.CurrentResource - 50) / 25m)
                    * bloodArmor.Value);
            ApplyGuardianEffect(
                _player.Actor,
                GuardianBloodArmorEffectId,
                EffectStat.Armor,
                armorBonus,
                now,
                EffectModifierMode.Percent,
                TimeSpan.FromSeconds(2));
        }

        if (GetGuardianHook("G-7-4") is { } lastBoundary)
        {
            if (hpPercent < lastBoundary.Threshold)
            {
                ApplyGuardianEffect(
                    _player.Actor,
                    "GUARDIAN_LAST_BOUNDARY_HEALING",
                    EffectStat.HealingReceivedMultiplier,
                    1 + lastBoundary.Value / 100m,
                    now,
                    EffectModifierMode.Multiplicative,
                    TimeSpan.FromDays(1));
                if (lastBoundary.SecondaryValue > 0)
                {
                    ApplyGuardianEffect(
                        _player.Actor,
                        "GUARDIAN_LAST_BOUNDARY_RESISTANCE",
                        EffectStat.MagicResistance,
                        lastBoundary.SecondaryValue / 100m,
                        now,
                        EffectModifierMode.Percent,
                        TimeSpan.FromDays(1));
                }
            }
            else
            {
                RemoveGuardianEffect("GUARDIAN_LAST_BOUNDARY_HEALING", now);
                RemoveGuardianEffect("GUARDIAN_LAST_BOUNDARY_RESISTANCE", now);
            }
        }
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

    private decimal GuardianRageMultiplier =>
        1 + (GetGuardianHook("G-8-3")?.Value ?? 0) / 100m;

    private decimal GuardianThreatMultiplier =>
        1 + (GetGuardianHook("G-5-2")?.Value ?? 0) / 100m;

    private decimal GuardianAutoAttackThreatMultiplier =>
        GuardianThreatMultiplier
        + (GetGuardianHook("G-1-4")?.Value ?? 0) / 100m;

    private decimal? GetGuardianHookValue(string talentId) =>
        GetGuardianHook(talentId)?.Value;
}
