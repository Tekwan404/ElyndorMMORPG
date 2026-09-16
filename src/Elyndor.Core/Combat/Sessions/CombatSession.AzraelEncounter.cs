using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;

namespace Elyndor.Core.Combat.Sessions;

public sealed record AzraelCombatEncounterProfile(
    IReadOnlyDictionary<AzraelCloneRole, EncounterEnemyProfile> CloneProfiles,
    AzraelTriuneEncounterDefinition? Definition = null);

public sealed partial class CombatSession
{
    private const string AzraelPhaseGuardEffectId = "AZRAEL_PHASE_GUARD";
    private const string AzraelSplitImmunityEffectId = "AZRAEL_SPLIT_IMMUNITY";
    private const string AzraelReviveWindowEffectId = "AZRAEL_REVIVE_WINDOW";
    private const string AzraelRevivalPowerEffectId = "AZRAEL_REVIVAL_POWER";
    private const string AzraelFinalPhaseEffectId = "AZRAEL_FINAL_PHASE";
    private const string AzraelFrostWardAbilityId = "AZRAEL_FROST_WARD";
    private const string AzraelFrostWardShieldEffectId = "AZRAEL_FROST_WARD_SHIELD";
    private const string AzraelVoidMendAbilityId = "AZRAEL_VOID_MEND";
    private static readonly TimeSpan AzraelPersistentEffectDuration = TimeSpan.FromHours(24);

    private AzraelTriuneEncounterRuntime? _azraelEncounterRuntime;
    private AzraelCombatEncounterProfile? _azraelEncounterProfile;
    private Guid? _azraelBossActorId;

    public void ConfigureAzraelEncounter(AzraelCombatEncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profile.CloneProfiles);
        if (_azraelEncounterRuntime is not null)
            throw new InvalidOperationException("Azrael encounter is already configured for this combat session.");
        if (Status != CombatSessionStatus.Active)
            throw new InvalidOperationException("Azrael encounter can only be configured for an active combat session.");

        ValidateAzraelProfile(profile);
        _azraelEncounterProfile = profile;
        _azraelEncounterRuntime = new AzraelTriuneEncounterRuntime(
            profile.Definition ?? AzraelTriuneEncounterDefinition.Default);
        _azraelBossActorId = _primaryEnemyActorId;
        EnsureAzraelPhaseGuard(CurrentTimeUtc);
    }

    private void ProcessAzraelEncounterEvent(CombatEvent combatEvent)
    {
        if (_azraelEncounterRuntime is null
            || _azraelEncounterProfile is null
            || _azraelBossActorId is not { } bossActorId
            || Status != CombatSessionStatus.Active)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.AbilityCompleted
            && combatEvent.SourceActorId is { } cloneCasterId
            && _azraelEncounterRuntime.Clones.TryGetValue(cloneCasterId, out AzraelCloneRole casterRole))
        {
            if (casterRole == AzraelCloneRole.Frost
                && string.Equals(combatEvent.DefinitionId, AzraelFrostWardAbilityId, StringComparison.Ordinal))
            {
                ApplyAzraelFrostWard(cloneCasterId, combatEvent.OccurredAtUtc);
                return;
            }
            if (casterRole == AzraelCloneRole.Void
                && string.Equals(combatEvent.DefinitionId, AzraelVoidMendAbilityId, StringComparison.Ordinal))
            {
                ApplyAzraelVoidMend(cloneCasterId, combatEvent.OccurredAtUtc);
                return;
            }
        }

        if (combatEvent.Type == CombatEventType.ActorDied
            && _azraelEncounterRuntime.Clones.ContainsKey(combatEvent.ActorId))
        {
            AzraelCloneDeathResult death = _azraelEncounterRuntime.RegisterCloneDeath(
                combatEvent.ActorId,
                combatEvent.OccurredAtUtc);
            if (!death.Accepted)
                return;
            if (death.SplitCompleted)
            {
                EndAzraelSplit(combatEvent.OccurredAtUtc);
                return;
            }
            if (death.WindowStarted)
                StartAzraelReviveWindow(combatEvent.OccurredAtUtc);
            return;
        }

        if (combatEvent.Type == CombatEventType.EffectExpired
            && combatEvent.TargetActorId == bossActorId
            && string.Equals(combatEvent.DefinitionId, AzraelReviveWindowEffectId, StringComparison.Ordinal))
        {
            IReadOnlyList<Guid> revived = _azraelEncounterRuntime.ExpireReviveWindow(
                combatEvent.OccurredAtUtc);
            foreach (Guid cloneActorId in revived)
            {
                ReviveEncounterEnemy(
                    cloneActorId,
                    _azraelEncounterRuntime.ReviveHpPercent,
                    bossActorId,
                    combatEvent.OccurredAtUtc,
                    "AZRAEL_CLONE_REVIVE");
                ApplyAzraelRevivalPower(combatEvent.OccurredAtUtc);
            }
            return;
        }

        if (combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.TargetActorId != bossActorId
            || combatEvent.IsPeriodic
            || _azraelEncounterRuntime.SplitTriggered)
        {
            return;
        }

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (_azraelEncounterRuntime.TryTriggerSplit(boss.CurrentHp, boss.MaxHp))
            BeginAzraelSplit(combatEvent.OccurredAtUtc);
    }

    private void BeginAzraelSplit(DateTimeOffset now)
    {
        if (_azraelEncounterRuntime is null
            || _azraelEncounterProfile is null
            || _azraelBossActorId is not { } bossActorId)
        {
            return;
        }

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    AzraelSplitImmunityEffectId,
                    EffectKind.StatModifier,
                    AzraelPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    0,
                    ModifiedStat: EffectStat.IncomingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                now),
            bossActorId,
            bossActorId,
            AzraelSplitImmunityEffectId);
        SetEncounterEnemyAiPaused(bossActorId, true, now);

        foreach (AzraelCloneRole role in new[]
                 {
                     AzraelCloneRole.Fire,
                     AzraelCloneRole.Frost,
                     AzraelCloneRole.Void
                 })
        {
            CombatParticipantDefinition clone = SpawnEncounterEnemy(
                _azraelEncounterProfile.CloneProfiles[role],
                bossActorId,
                now);
            _azraelEncounterRuntime.RegisterClone(clone.Actor.ActorId, role);
        }
    }

    private void StartAzraelReviveWindow(DateTimeOffset now)
    {
        if (_azraelEncounterRuntime is null
            || _azraelBossActorId is not { } bossActorId)
        {
            return;
        }

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    AzraelReviveWindowEffectId,
                    EffectKind.Buff,
                    _azraelEncounterRuntime.ReviveWindow,
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now),
            bossActorId,
            bossActorId,
            AzraelReviveWindowEffectId);
    }

    private void ApplyAzraelFrostWard(Guid sourceActorId, DateTimeOffset now)
    {
        if (_azraelEncounterRuntime is null)
            return;
        CombatParticipantDefinition? weakest = _azraelEncounterRuntime.Clones.Keys
            .Select(actorId => _enemiesById.GetValueOrDefault(actorId))
            .Where(enemy => enemy is not null && !enemy.Actor.IsDead)
            .OrderBy(enemy => enemy!.Actor.CurrentHp / enemy.Actor.MaxHp)
            .ThenBy(enemy => enemy!.Actor.ActorId)
            .FirstOrDefault();
        if (weakest is null)
            return;

        decimal shield = weakest.Actor.MaxHp * 0.10m;
        ApplyKernelEvents(
            EffectEngine.Apply(
                weakest.Actor,
                sourceActorId,
                new EffectDefinition(
                    AzraelFrostWardShieldEffectId,
                    EffectKind.Shield,
                    TimeSpan.FromSeconds(6),
                    1,
                    EffectStackPolicy.Replace,
                    shield),
                now),
            sourceActorId,
            weakest.Actor.ActorId,
            AzraelFrostWardShieldEffectId);
    }

    private void ApplyAzraelVoidMend(Guid sourceActorId, DateTimeOffset now)
    {
        if (_azraelEncounterRuntime is null)
            return;
        foreach (Guid cloneActorId in _azraelEncounterRuntime.Clones.Keys)
        {
            CombatParticipantDefinition clone = _enemiesById[cloneActorId];
            if (!clone.Actor.IsDead)
            {
                HealEncounterActor(
                    clone.Actor,
                    clone.Actor.MaxHp * 0.08m,
                    sourceActorId,
                    now,
                    AzraelVoidMendAbilityId);
            }
        }
    }

    private void ApplyAzraelRevivalPower(DateTimeOffset now)
    {
        if (_azraelEncounterRuntime is null
            || _azraelBossActorId is not { } bossActorId)
        {
            return;
        }
        CombatActorState boss = _enemiesById[bossActorId].Actor;
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    AzraelRevivalPowerEffectId,
                    EffectKind.StatModifier,
                    AzraelPersistentEffectDuration,
                    99,
                    EffectStackPolicy.Stack,
                    _azraelEncounterRuntime.FinalDamageBonusPerRevive,
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Percent),
                now),
            bossActorId,
            bossActorId,
            AzraelRevivalPowerEffectId);
    }

    private void EndAzraelSplit(DateTimeOffset now)
    {
        if (_azraelEncounterRuntime is null
            || _azraelBossActorId is not { } bossActorId)
        {
            return;
        }
        CombatActorState boss = _enemiesById[bossActorId].Actor;
        foreach (string effectId in new[]
                 {
                     AzraelReviveWindowEffectId,
                     AzraelSplitImmunityEffectId,
                     AzraelPhaseGuardEffectId
                 })
        {
            if (!boss.ActiveEffects.Any(effect =>
                    string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)))
            {
                continue;
            }
            ApplyKernelEvents(
                EffectEngine.Remove(boss, effectId, now),
                bossActorId,
                bossActorId,
                effectId);
        }

        boss.SetCurrentHp(boss.MaxHp * _azraelEncounterRuntime.FinalPhaseHpPercent);
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    AzraelFinalPhaseEffectId,
                    EffectKind.Buff,
                    AzraelPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now),
            bossActorId,
            bossActorId,
            AzraelFinalPhaseEffectId);
        SetEncounterEnemyAiPaused(bossActorId, false, now);
    }

    private void EnsureAzraelPhaseGuard(DateTimeOffset now)
    {
        if (_azraelBossActorId is not { } bossActorId)
            return;
        CombatActorState boss = _enemiesById[bossActorId].Actor;
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    AzraelPhaseGuardEffectId,
                    EffectKind.LethalDamagePrevention,
                    AzraelPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    0),
                now),
            bossActorId,
            bossActorId,
            AzraelPhaseGuardEffectId);
    }

    private void ValidateAzraelProfile(AzraelCombatEncounterProfile profile)
    {
        AzraelCloneRole[] requiredRoles =
        [
            AzraelCloneRole.Fire,
            AzraelCloneRole.Frost,
            AzraelCloneRole.Void
        ];
        if (profile.CloneProfiles.Count != requiredRoles.Length
            || requiredRoles.Any(role => !profile.CloneProfiles.ContainsKey(role)))
        {
            throw new ArgumentException("Azrael requires exactly one Fire, Frost, and Void clone profile.", nameof(profile));
        }

        foreach (EncounterEnemyProfile clone in profile.CloneProfiles.Values)
        {
            if (clone.Monster.MaxHp <= 0 || clone.Monster.AutoAttackInterval <= TimeSpan.Zero)
                throw new ArgumentException("Azrael clone combat profile is invalid.", nameof(profile));
            if (clone.Monster.AbilityIds.Any(abilityId => !_abilities.ContainsKey(abilityId)))
                throw new ArgumentException("Azrael clone references an unknown ability.", nameof(profile));
        }
    }
}
