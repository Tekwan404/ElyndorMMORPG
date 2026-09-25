using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Targeting;

namespace Elyndor.Core.Combat.Sessions;

public sealed record KaelMorCombatEncounterProfile(
    EncounterEnemyProfile Defender,
    EncounterEnemyProfile Caster,
    KaelMorTrialEncounterDefinition? Definition = null);

public sealed partial class CombatSession
{
    private const string KaelMorTrialTimerEffectId = "KAEL_MOR_TRIAL_TIMER";
    private const string KaelMorTrialSuccessDamageEffectId = "KAEL_MOR_TRIAL_SUCCESS_DAMAGE";
    private const string KaelMorTrialSuccessHealingEffectId = "KAEL_MOR_TRIAL_SUCCESS_HEALING";
    private const string KaelMorTrialFailureEffectId = "KAEL_MOR_SOUL_OF_THE_FALLEN";
    private static readonly TimeSpan KaelMorPersistentEffectDuration = TimeSpan.FromHours(24);

    private KaelMorTrialEncounterRuntime? _kaelMorTrialRuntime;
    private KaelMorCombatEncounterProfile? _kaelMorEncounterProfile;
    private Guid? _kaelMorBossActorId;

    public void ConfigureKaelMorEncounter(KaelMorCombatEncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_kaelMorTrialRuntime is not null)
            throw new InvalidOperationException("Kael-Mor encounter is already configured for this combat session.");
        if (Status != CombatSessionStatus.Active)
            throw new InvalidOperationException("Kael-Mor encounter can only be configured for an active combat session.");

        ValidateKaelMorProfile(profile);
        _kaelMorEncounterProfile = profile;
        _kaelMorTrialRuntime = new KaelMorTrialEncounterRuntime(
            profile.Definition ?? KaelMorTrialEncounterDefinition.Default);
        _kaelMorBossActorId = _primaryEnemyActorId;
    }

    private void ProcessKaelMorEncounterEvent(CombatEvent combatEvent)
    {
        if (_kaelMorTrialRuntime is null
            || _kaelMorEncounterProfile is null
            || _kaelMorBossActorId is not { } bossActorId
            || Status != CombatSessionStatus.Active)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.ActorDied)
        {
            if (combatEvent.ActorId == bossActorId)
            {
                KaelMorTrialState? cancelled = _kaelMorTrialRuntime.CancelActiveTrial();
                foreach (Guid addActorId in cancelled?.ActiveAddActorIds ?? [])
                {
                    DeactivateEncounterEnemy(
                        addActorId,
                        bossActorId,
                        combatEvent.OccurredAtUtc,
                        "KAEL_MOR_TRIAL_CANCELLED");
                }
                return;
            }

            KaelMorTrialState? success = _kaelMorTrialRuntime.RegisterAddDeath(combatEvent.ActorId);
            if (success is not null)
            {
                ResolveKaelMorTrialSuccess(success, combatEvent.OccurredAtUtc);
                RetargetPlayersFromEncounterEnemy(combatEvent.ActorId, combatEvent.OccurredAtUtc);
                return;
            }
        }

        if (combatEvent.Type == CombatEventType.EffectExpired
            && string.Equals(combatEvent.DefinitionId, KaelMorTrialTimerEffectId, StringComparison.Ordinal)
            && combatEvent.TargetActorId is { } expiredActorId)
        {
            KaelMorTrialState? failure = _kaelMorTrialRuntime.RegisterTimeout(
                expiredActorId,
                combatEvent.OccurredAtUtc);
            if (failure is not null)
                ResolveKaelMorTrialFailure(failure, combatEvent.OccurredAtUtc);
            return;
        }

        if (combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.TargetActorId != bossActorId
            || combatEvent.IsPeriodic
            || _kaelMorTrialRuntime.HasActiveTrial)
        {
            return;
        }

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        Guid[] eligibleOwners = _participantRoster.Participants
            .Where(item => item.Status == CombatParticipantStatus.Active)
            .Select(item => item.ActorId)
            .Where(actorId =>
                _playerStatesByActorId.TryGetValue(actorId, out CombatPlayerRuntimeState? state)
                && !state.Definition.Actor.IsDead)
            .OrderBy(_ => _random.NextUnit())
            .ToArray();

        if (!_kaelMorTrialRuntime.TryBeginNextTrial(
                boss.CurrentHp,
                boss.MaxHp,
                eligibleOwners,
                combatEvent.OccurredAtUtc,
                out Guid ownerActorId))
        {
            return;
        }

        SpawnKaelMorTrial(ownerActorId, combatEvent.OccurredAtUtc);
    }

    private bool CanPlayerTargetKaelMorEnemy(Guid playerActorId, Guid targetActorId) =>
        _kaelMorTrialRuntime?.CanActorTargetEnemy(playerActorId, targetActorId) ?? true;

    private void SpawnKaelMorTrial(Guid ownerActorId, DateTimeOffset now)
    {
        if (_kaelMorTrialRuntime is null
            || _kaelMorEncounterProfile is null
            || _kaelMorBossActorId is not { } bossActorId
            || !_playerStatesByActorId.TryGetValue(ownerActorId, out CombatPlayerRuntimeState? owner))
        {
            return;
        }

        CombatParticipantDefinition defender = SpawnKaelMorTrialAdd(
            _kaelMorEncounterProfile.Defender,
            ownerActorId,
            bossActorId,
            now);
        CombatParticipantDefinition caster = SpawnKaelMorTrialAdd(
            _kaelMorEncounterProfile.Caster,
            ownerActorId,
            bossActorId,
            now);

        owner.SelectedTargetActorId = defender.Actor.ActorId;
        Append(new CombatEvent(
            CombatEventType.TargetChanged,
            now,
            ownerActorId,
            defender.DefinitionId,
            SourceActorId: ownerActorId,
            TargetActorId: defender.Actor.ActorId));

        _ = caster;
    }

    private CombatParticipantDefinition SpawnKaelMorTrialAdd(
        EncounterEnemyProfile profile,
        Guid ownerActorId,
        Guid bossActorId,
        DateTimeOffset now)
    {
        if (_kaelMorTrialRuntime is null)
            throw new InvalidOperationException("Kael-Mor trial runtime is missing.");

        CombatParticipantDefinition add = SpawnEncounterEnemy(
            profile,
            bossActorId,
            now,
            rewardEligible: false);
        _kaelMorTrialRuntime.RegisterAdd(add.Actor.ActorId);

        ThreatTable threat = _enemyThreatTables[add.Actor.ActorId];
        threat.Clear();
        threat.AddExplicitThreat(ownerActorId, 1);
        _enemyForcedTargets[add.Actor.ActorId].Set(
            ownerActorId,
            now,
            _kaelMorTrialRuntime.TrialLifetime);

        ApplyKernelEvents(
            EffectEngine.Apply(
                add.Actor,
                bossActorId,
                new EffectDefinition(
                    KaelMorTrialTimerEffectId,
                    EffectKind.Buff,
                    _kaelMorTrialRuntime.TrialLifetime,
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now),
            bossActorId,
            add.Actor.ActorId,
            KaelMorTrialTimerEffectId);
        return add;
    }

    private void ResolveKaelMorTrialSuccess(KaelMorTrialState trial, DateTimeOffset now)
    {
        if (_kaelMorTrialRuntime is null
            || !_playerStatesByActorId.TryGetValue(trial.OwnerActorId, out CombatPlayerRuntimeState? owner))
        {
            return;
        }

        ApplyKernelEvents(
            EffectEngine.Apply(
                owner.Definition.Actor,
                trial.OwnerActorId,
                new EffectDefinition(
                    KaelMorTrialSuccessDamageEffectId,
                    EffectKind.StatModifier,
                    _kaelMorTrialRuntime.SuccessBuffDuration,
                    1,
                    EffectStackPolicy.Replace,
                    1.10m,
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                now),
            trial.OwnerActorId,
            trial.OwnerActorId,
            KaelMorTrialSuccessDamageEffectId);
        ApplyKernelEvents(
            EffectEngine.Apply(
                owner.Definition.Actor,
                trial.OwnerActorId,
                new EffectDefinition(
                    KaelMorTrialSuccessHealingEffectId,
                    EffectKind.StatModifier,
                    _kaelMorTrialRuntime.SuccessBuffDuration,
                    1,
                    EffectStackPolicy.Replace,
                    1.10m,
                    ModifiedStat: EffectStat.OutgoingHealingMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                now),
            trial.OwnerActorId,
            trial.OwnerActorId,
            KaelMorTrialSuccessHealingEffectId);
    }

    private void ResolveKaelMorTrialFailure(KaelMorTrialState trial, DateTimeOffset now)
    {
        if (_kaelMorTrialRuntime is null || _kaelMorBossActorId is not { } bossActorId)
            return;

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        HealEncounterActor(
            boss,
            boss.MaxHp * _kaelMorTrialRuntime.FailureHealMaxHpRatio,
            bossActorId,
            now,
            KaelMorTrialFailureEffectId);
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    KaelMorTrialFailureEffectId,
                    EffectKind.StatModifier,
                    KaelMorPersistentEffectDuration,
                    99,
                    EffectStackPolicy.Stack,
                    _kaelMorTrialRuntime.FailureDamageBonusPercent,
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Percent),
                now),
            bossActorId,
            bossActorId,
            KaelMorTrialFailureEffectId);

        foreach (Guid addActorId in trial.ActiveAddActorIds)
        {
            DeactivateEncounterEnemy(
                addActorId,
                bossActorId,
                now,
                "KAEL_MOR_TRIAL_FAILED");
        }
    }

    private void ValidateKaelMorProfile(KaelMorCombatEncounterProfile profile)
    {
        foreach (EncounterEnemyProfile add in new[] { profile.Defender, profile.Caster })
        {
            if (add.Monster.MaxHp <= 0 || add.Monster.AutoAttackInterval <= TimeSpan.Zero)
                throw new ArgumentException("Kael-Mor trial add profile is invalid.", nameof(profile));
            if (add.Monster.AbilityIds.Any(abilityId => !_abilities.ContainsKey(abilityId)))
                throw new ArgumentException("Kael-Mor trial add references an unknown ability.", nameof(profile));
        }
    }
}
