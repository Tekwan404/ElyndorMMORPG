using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Participants;

namespace Elyndor.Core.Combat.Sessions;

public sealed record MorEtCombatEncounterProfile(
    IReadOnlyDictionary<string, EncounterEnemyProfile> SoulProfilesByClassId,
    MorEtSoulEncounterDefinition? Definition = null);

public sealed partial class CombatSession
{
    private const string MorEtPhaseGuardEffectId = "MOR_ET_PHASE_GUARD";
    private const string MorEtSoulTimerEffectId = "MOR_ET_SOUL_TIMER";
    private const string MorEtEmptiedDamageEffectId = "MOR_ET_EMPTIED_DAMAGE";
    private const string MorEtEmptiedDotEffectId = "MOR_ET_EMPTIED_DOT";
    private const string MorEtReturnedSoulEffectId = "MOR_ET_RETURNED_SOUL";
    private const string MorEtReturnedSoulHealingEffectId = "MOR_ET_RETURNED_SOUL_HEALING";
    private const string MorEtFailureStackEffectId = "MOR_ET_FAILURE_STACK";
    private static readonly TimeSpan MorEtPersistentEffectDuration = TimeSpan.FromHours(24);

    private MorEtSoulEncounterRuntime? _morEtEncounterRuntime;
    private MorEtCombatEncounterProfile? _morEtEncounterProfile;
    private Guid? _morEtBossActorId;

    public void ConfigureMorEtEncounter(MorEtCombatEncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profile.SoulProfilesByClassId);
        if (_morEtEncounterRuntime is not null)
            throw new InvalidOperationException("Mor-Et encounter is already configured for this combat session.");
        if (Status != CombatSessionStatus.Active)
            throw new InvalidOperationException("Mor-Et encounter can only be configured for an active combat session.");

        ValidateMorEtProfile(profile);
        _morEtEncounterProfile = profile;
        _morEtEncounterRuntime = new MorEtSoulEncounterRuntime(
            profile.Definition ?? MorEtSoulEncounterDefinition.Default);
        _morEtBossActorId = _primaryEnemyActorId;
        EnsureMorEtPhaseGuard(CurrentTimeUtc);
    }

    private void ProcessMorEtEncounterEvent(CombatEvent combatEvent)
    {
        if (_morEtEncounterRuntime is null
            || _morEtEncounterProfile is null
            || _morEtBossActorId is not { } bossActorId
            || Status != CombatSessionStatus.Active)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.ActorDied)
        {
            MorEtSoulState? soul = _morEtEncounterRuntime.RegisterSoulDeath(combatEvent.ActorId);
            if (soul is not null)
            {
                ResolveMorEtSoulSuccess(soul, combatEvent.OccurredAtUtc);
                CompleteMorEtIfReady(combatEvent.OccurredAtUtc);
                return;
            }
        }

        if (combatEvent.Type == CombatEventType.EffectExpired
            && string.Equals(combatEvent.DefinitionId, MorEtSoulTimerEffectId, StringComparison.Ordinal)
            && combatEvent.TargetActorId is { } expiredSoulActorId)
        {
            MorEtSoulState? expired = _morEtEncounterRuntime.RegisterSoulTimeout(
                expiredSoulActorId,
                combatEvent.OccurredAtUtc);
            if (expired is not null)
            {
                ResolveMorEtSoulFailure(expired, combatEvent.OccurredAtUtc);
                CompleteMorEtIfReady(combatEvent.OccurredAtUtc);
                return;
            }
        }

        if (combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.TargetActorId != bossActorId
            || combatEvent.IsPeriodic
            || _morEtEncounterRuntime.HasActiveWave)
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
        if (!_morEtEncounterRuntime.TryBeginNextWave(
                boss.CurrentHp,
                boss.MaxHp,
                eligibleOwners,
                out IReadOnlyList<Guid> selectedOwners))
        {
            return;
        }

        foreach (Guid ownerActorId in selectedOwners)
            SpawnMorEtSoul(ownerActorId, combatEvent.OccurredAtUtc);
    }

    private void SpawnMorEtSoul(Guid ownerActorId, DateTimeOffset now)
    {
        if (_morEtEncounterRuntime is null
            || _morEtEncounterProfile is null
            || _morEtBossActorId is not { } bossActorId
            || !_playerStatesByActorId.TryGetValue(ownerActorId, out CombatPlayerRuntimeState? owner))
        {
            return;
        }

        string classId = owner.Definition.DefinitionId;
        if (!_morEtEncounterProfile.SoulProfilesByClassId.TryGetValue(
                classId,
                out EncounterEnemyProfile? profile))
        {
            throw new InvalidOperationException($"Mor-Et has no soul profile for class '{classId}'.");
        }

        CombatParticipantDefinition soul = SpawnEncounterEnemy(profile, bossActorId, now);
        _morEtEncounterRuntime.RegisterSoul(soul.Actor.ActorId, ownerActorId, now);
        ApplyKernelEvents(
            EffectEngine.Apply(
                soul.Actor,
                bossActorId,
                new EffectDefinition(
                    MorEtSoulTimerEffectId,
                    EffectKind.Buff,
                    _morEtEncounterRuntime.SoulLifetime,
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now),
            bossActorId,
            soul.Actor.ActorId,
            MorEtSoulTimerEffectId);
        ApplyMorEtOwnerDebuffs(owner.Definition.Actor, bossActorId, now);
    }

    private void ApplyMorEtOwnerDebuffs(
        CombatActorState owner,
        Guid bossActorId,
        DateTimeOffset now)
    {
        ApplyKernelEvents(
            EffectEngine.Apply(
                owner,
                bossActorId,
                new EffectDefinition(
                    MorEtEmptiedDamageEffectId,
                    EffectKind.StatModifier,
                    MorEtPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    0.70m,
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                now),
            bossActorId,
            owner.ActorId,
            MorEtEmptiedDamageEffectId);
        ApplyKernelEvents(
            EffectEngine.Apply(
                owner,
                bossActorId,
                new EffectDefinition(
                    MorEtEmptiedDotEffectId,
                    EffectKind.DamageOverTime,
                    MorEtPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    35,
                    TickInterval: TimeSpan.FromSeconds(3),
                    PeriodicDamageType: DamageType.True),
                now),
            bossActorId,
            owner.ActorId,
            MorEtEmptiedDotEffectId);
    }

    private void ResolveMorEtSoulSuccess(MorEtSoulState soul, DateTimeOffset now)
    {
        if (_morEtEncounterRuntime is null
            || !_playerStatesByActorId.TryGetValue(soul.OwnerActorId, out CombatPlayerRuntimeState? owner))
        {
            return;
        }

        RemoveMorEtOwnerDebuffs(owner.Definition.Actor, now);
        ApplyKernelEvents(
            EffectEngine.Apply(
                owner.Definition.Actor,
                soul.OwnerActorId,
                new EffectDefinition(
                    MorEtReturnedSoulEffectId,
                    EffectKind.StatModifier,
                    _morEtEncounterRuntime.SuccessBuffDuration,
                    1,
                    EffectStackPolicy.Replace,
                    1.25m,
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                now),
            soul.OwnerActorId,
            soul.OwnerActorId,
            MorEtReturnedSoulEffectId);
        ApplyKernelEvents(
            EffectEngine.Apply(
                owner.Definition.Actor,
                soul.OwnerActorId,
                new EffectDefinition(
                    MorEtReturnedSoulHealingEffectId,
                    EffectKind.StatModifier,
                    _morEtEncounterRuntime.SuccessBuffDuration,
                    1,
                    EffectStackPolicy.Replace,
                    1.25m,
                    ModifiedStat: EffectStat.OutgoingHealingMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                now),
            soul.OwnerActorId,
            soul.OwnerActorId,
            MorEtReturnedSoulHealingEffectId);
    }

    private void ResolveMorEtSoulFailure(MorEtSoulState soul, DateTimeOffset now)
    {
        if (_morEtEncounterRuntime is null
            || _morEtBossActorId is not { } bossActorId)
        {
            return;
        }

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        HealEncounterActor(
            boss,
            boss.MaxHp * _morEtEncounterRuntime.FailureHealMaxHpRatio,
            bossActorId,
            now,
            MorEtFailureStackEffectId);
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    MorEtFailureStackEffectId,
                    EffectKind.StatModifier,
                    MorEtPersistentEffectDuration,
                    99,
                    EffectStackPolicy.Stack,
                    _morEtEncounterRuntime.FailureDamageBonusPercent,
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Percent),
                now),
            bossActorId,
            bossActorId,
            MorEtFailureStackEffectId);

        if (_playerStatesByActorId.TryGetValue(soul.OwnerActorId, out CombatPlayerRuntimeState? owner))
            RemoveMorEtOwnerDebuffs(owner.Definition.Actor, now);
        DeactivateEncounterEnemy(
            soul.SoulActorId,
            bossActorId,
            now,
            "MOR_ET_SOUL_CONSUMED");
    }

    private void RemoveMorEtOwnerDebuffs(CombatActorState owner, DateTimeOffset now)
    {
        if (_morEtBossActorId is not { } bossActorId)
            return;
        foreach (string effectId in new[] { MorEtEmptiedDamageEffectId, MorEtEmptiedDotEffectId })
        {
            if (!owner.ActiveEffects.Any(effect =>
                    string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)))
            {
                continue;
            }
            ApplyKernelEvents(
                EffectEngine.Remove(owner, effectId, now),
                bossActorId,
                owner.ActorId,
                effectId);
        }
    }

    private void CompleteMorEtIfReady(DateTimeOffset now)
    {
        if (_morEtEncounterRuntime?.IsComplete != true
            || _morEtBossActorId is not { } bossActorId)
        {
            return;
        }

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (boss.ActiveEffects.Any(effect =>
                string.Equals(effect.Definition.Id, MorEtPhaseGuardEffectId, StringComparison.Ordinal)))
        {
            ApplyKernelEvents(
                EffectEngine.Remove(boss, MorEtPhaseGuardEffectId, now),
                bossActorId,
                bossActorId,
                MorEtPhaseGuardEffectId);
        }
    }

    private void EnsureMorEtPhaseGuard(DateTimeOffset now)
    {
        if (_morEtBossActorId is not { } bossActorId)
            return;
        CombatActorState boss = _enemiesById[bossActorId].Actor;
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    MorEtPhaseGuardEffectId,
                    EffectKind.LethalDamagePrevention,
                    MorEtPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    0),
                now),
            bossActorId,
            bossActorId,
            MorEtPhaseGuardEffectId);
    }

    private void ValidateMorEtProfile(MorEtCombatEncounterProfile profile)
    {
        string[] requiredClasses = ["WARRIOR", "MAGE", "ARCHER", "PALADIN"];
        if (requiredClasses.Any(classId => !profile.SoulProfilesByClassId.ContainsKey(classId)))
            throw new ArgumentException("Mor-Et requires a soul profile for every playable class.", nameof(profile));

        foreach (EncounterEnemyProfile soul in profile.SoulProfilesByClassId.Values)
        {
            if (soul.Monster.MaxHp <= 0 || soul.Monster.AutoAttackInterval <= TimeSpan.Zero)
                throw new ArgumentException("Mor-Et soul combat profile is invalid.", nameof(profile));
            if (soul.Monster.AbilityIds.Any(abilityId => !_abilities.ContainsKey(abilityId)))
                throw new ArgumentException("Mor-Et soul references an unknown ability.", nameof(profile));
        }
    }
}
