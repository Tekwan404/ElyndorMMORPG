using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Monsters;

namespace Elyndor.Core.Combat.Sessions;

public sealed record MirrorEncounterAddProfile(
    MirrorEncounterAddRole Role,
    MonsterDefinition Monster,
    MonsterAiProfile AiProfile);

public sealed record MirrorCombatEncounterProfile(
    IReadOnlyList<MirrorEncounterAddProfile> Adds,
    MirrorBarrierEncounterDefinition? Definition = null);

public sealed partial class CombatSession
{
    private const string MirrorPhaseGuardEffectId = "MIRROR_PHASE_GUARD";
    private const string MirrorBarrierStrongEffectId = "MIRROR_BARRIER_70";
    private const string MirrorBarrierEffectId = "MIRROR_BARRIER_50";
    private const string BrokenMirrorEffectId = "MIRROR_BROKEN";
    private const string MirrorReflectionDefinitionId = "MIRROR_BARRIER_REFLECTION";
    private static readonly TimeSpan MirrorPersistentEffectDuration = TimeSpan.FromHours(24);

    private MirrorBarrierEncounterRuntime? _mirrorEncounterRuntime;
    private MirrorCombatEncounterProfile? _mirrorEncounterProfile;
    private Guid? _mirrorBossActorId;

    public void ConfigureMirrorEncounter(MirrorCombatEncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_mirrorEncounterRuntime is not null)
            throw new InvalidOperationException("Mirror encounter is already configured for this combat session.");
        if (Status != CombatSessionStatus.Active)
            throw new InvalidOperationException("Mirror encounter can only be configured for an active combat session.");

        ValidateMirrorProfile(profile);
        _mirrorEncounterProfile = profile;
        _mirrorEncounterRuntime = new MirrorBarrierEncounterRuntime(
            profile.Definition ?? MirrorBarrierEncounterDefinition.Default);
        _mirrorBossActorId = _primaryEnemyActorId;
        EnsureMirrorPhaseGuard(CurrentTimeUtc);
    }

    private void ProcessMirrorEncounterEvent(CombatEvent combatEvent)
    {
        MirrorBarrierEncounterRuntime? runtime = _mirrorEncounterRuntime;
        if (runtime is null
            || _mirrorEncounterProfile is null
            || _mirrorBossActorId is not { } bossActorId
            || Status != CombatSessionStatus.Active)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.TargetActorId == bossActorId
            && combatEvent.Amount > 0)
        {
            bool barrierWasActive = runtime.BarrierActive;
            decimal reflectionRatio = runtime.ReflectionRatio;

            if (barrierWasActive
                && !combatEvent.IsPeriodic
                && combatEvent.SourceActorId is { } sourceActorId)
            {
                ReflectMirrorDamage(
                    sourceActorId,
                    combatEvent.Amount,
                    reflectionRatio,
                    combatEvent.OccurredAtUtc);
                if (Status != CombatSessionStatus.Active)
                    return;
            }

            CombatParticipantDefinition boss = _enemiesById[bossActorId];
            if (barrierWasActive)
            {
                EnsureMirrorPhaseGuard(combatEvent.OccurredAtUtc);
                return;
            }

            // Dynamic enemy registration mutates the encounter roster. Periodic effects are
            // currently processed while the roster is being enumerated, so defer a threshold
            // crossed only by a DoT until the next direct hit instead of invalidating that loop.
            if (combatEvent.IsPeriodic)
            {
                EnsureMirrorPhaseGuard(combatEvent.OccurredAtUtc);
                return;
            }

            if (runtime.TryBeginNextWave(
                    boss.Actor.CurrentHp,
                    boss.Actor.MaxHp,
                    out MirrorEncounterWave? wave)
                && wave is not null)
            {
                BeginMirrorWave(wave, combatEvent.OccurredAtUtc);
            }
            return;
        }

        if (combatEvent.Type != CombatEventType.ActorDied)
            return;

        MirrorEncounterAddRole? role = runtime.GetRole(combatEvent.ActorId);
        if (role is null)
            return;

        MirrorBarrierBroken? broken = runtime.RegisterAddDeath(
            combatEvent.ActorId,
            combatEvent.OccurredAtUtc);
        if (broken is null)
        {
            if (role == MirrorEncounterAddRole.Guardian)
                SetMirrorBarrierVisual(strong: false, combatEvent.OccurredAtUtc);
            return;
        }

        BreakMirrorBarrier(broken);
    }

    private void BeginMirrorWave(MirrorEncounterWave wave, DateTimeOffset now)
    {
        if (_mirrorEncounterRuntime is null
            || _mirrorEncounterProfile is null
            || _mirrorBossActorId is not { } bossActorId)
        {
            throw new InvalidOperationException("Mirror encounter is not configured.");
        }

        EnsureMirrorPhaseGuard(now);
        RemoveMirrorEffect(BrokenMirrorEffectId, now);
        SetMirrorBarrierVisual(strong: true, now);

        foreach (MirrorEncounterAddRole role in wave.RequiredRoles)
        {
            MirrorEncounterAddProfile addProfile = _mirrorEncounterProfile.Adds.Single(item =>
                item.Role == role);
            CombatParticipantDefinition add = SpawnMirrorAdd(
                addProfile,
                bossActorId,
                now);
            _mirrorEncounterRuntime.RegisterAdd(add.Actor.ActorId, role);
        }
    }

    private CombatParticipantDefinition SpawnMirrorAdd(
        MirrorEncounterAddProfile profile,
        Guid bossActorId,
        DateTimeOffset now)
    {
        CombatParticipantDefinition summoned = CreateSummonedParticipant(profile.Monster);
        CombatActorState[] existingEnemyActors = _enemies
            .Select(enemy => enemy.Actor)
            .ToArray();

        foreach (CombatPlayerRuntimeState playerState in _playerStatesByActorId.Values)
            playerState.Runtime.AddActor(summoned.Actor);
        _companionRuntime?.AddActor(summoned.Actor);
        foreach (CombatRuntimeState runtime in _enemyRuntimes.Values)
            runtime.AddActor(summoned.Actor);

        _enemies.Add(summoned);
        _enemiesById.Add(summoned.Actor.ActorId, summoned);
        _enemyRuntimes.Add(
            summoned.Actor.ActorId,
            CreateRuntime(
                summoned.Actor,
                _playerStatesByActorId.Values.Select(state => state.Definition.Actor)
                    .Concat(existingEnemyActors)
                    .Concat(_companion is null ? [] : new[] { _companion.Actor })));
        _enemyAiRuntimes.Add(
            summoned.Actor.ActorId,
            new EnemyAiRuntime(
                profile.AiProfile,
                now + summoned.AutoAttack.Interval));

        ThreatTable threat = new();
        foreach (CombatPlayerRuntimeState playerState in _playerStatesByActorId.Values)
            threat.AddThreat(playerState.Definition.Actor.ActorId, 1);
        if (_companion is not null)
            threat.AddThreat(_companion.Actor.ActorId, 1);
        _enemyThreatTables.Add(summoned.Actor.ActorId, threat);
        _enemyForcedTargets.Add(summoned.Actor.ActorId, new ForcedTargetState());

        Append(new CombatEvent(
            CombatEventType.ActorSummoned,
            now,
            summoned.Actor.ActorId,
            summoned.DefinitionId,
            SourceActorId: bossActorId,
            TargetActorId: summoned.Actor.ActorId));
        return summoned;
    }

    private void ReflectMirrorDamage(
        Guid attackerActorId,
        decimal directDamage,
        decimal reflectionRatio,
        DateTimeOffset now)
    {
        if (reflectionRatio <= 0
            || directDamage <= 0
            || _mirrorBossActorId is not { } bossActorId
            || !_enemiesById.TryGetValue(bossActorId, out CombatParticipantDefinition? boss))
        {
            return;
        }

        CombatActorState? attacker = ResolveMirrorReflectionTarget(attackerActorId);
        if (attacker is null || attacker.IsDead)
            return;

        DamageResult reflected = DamagePipeline.Resolve(
            new DamageRequest(
                boss.Actor,
                attacker,
                directDamage * reflectionRatio,
                DamageType.True,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                MinimumDamage: 0,
                SkipDefenseMitigation: true,
                CanBlock: false),
            _random,
            now);
        ApplyKernelEvents(
            reflected.Events,
            boss.Actor.ActorId,
            attacker.ActorId,
            MirrorReflectionDefinitionId);
    }

    private CombatActorState? ResolveMirrorReflectionTarget(Guid actorId)
    {
        if (_playerStatesByActorId.TryGetValue(actorId, out CombatPlayerRuntimeState? playerState))
            return playerState.Definition.Actor;
        if (_companion is not null && _companion.Actor.ActorId == actorId)
            return _companion.Actor;
        return null;
    }

    private void BreakMirrorBarrier(MirrorBarrierBroken broken)
    {
        RemoveMirrorEffect(MirrorBarrierStrongEffectId, broken.StartedAtUtc);
        RemoveMirrorEffect(MirrorBarrierEffectId, broken.StartedAtUtc);

        if (_mirrorBossActorId is not { } bossActorId)
            return;

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    BrokenMirrorEffectId,
                    EffectKind.StatModifier,
                    broken.ExpiresAtUtc - broken.StartedAtUtc,
                    1,
                    EffectStackPolicy.Replace,
                    broken.DamageTakenMultiplier,
                    ModifiedStat: EffectStat.IncomingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                broken.StartedAtUtc),
            bossActorId,
            bossActorId,
            BrokenMirrorEffectId);

        if (_mirrorEncounterRuntime?.RemainingWaves > 0)
            EnsureMirrorPhaseGuard(broken.StartedAtUtc);
        else
            RemoveMirrorEffect(MirrorPhaseGuardEffectId, broken.StartedAtUtc);
    }

    private void SetMirrorBarrierVisual(bool strong, DateTimeOffset now)
    {
        if (_mirrorBossActorId is not { } bossActorId)
            return;

        string desiredId = strong
            ? MirrorBarrierStrongEffectId
            : MirrorBarrierEffectId;
        string otherId = strong
            ? MirrorBarrierEffectId
            : MirrorBarrierStrongEffectId;
        RemoveMirrorEffect(otherId, now);

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (boss.ActiveEffects.Any(effect =>
                string.Equals(effect.Definition.Id, desiredId, StringComparison.Ordinal)))
        {
            return;
        }

        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    desiredId,
                    EffectKind.Buff,
                    MirrorPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    _mirrorEncounterRuntime?.ReflectionRatio ?? 0),
                now),
            bossActorId,
            bossActorId,
            desiredId);
    }

    private void EnsureMirrorPhaseGuard(DateTimeOffset now)
    {
        if (_mirrorBossActorId is not { } bossActorId)
            return;
        if (_mirrorEncounterRuntime is { RemainingWaves: 0, BarrierActive: false })
            return;

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (boss.ActiveEffects.Any(effect =>
                string.Equals(
                    effect.Definition.Id,
                    MirrorPhaseGuardEffectId,
                    StringComparison.Ordinal)))
        {
            return;
        }

        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    MirrorPhaseGuardEffectId,
                    EffectKind.LethalDamagePrevention,
                    MirrorPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    0),
                now),
            bossActorId,
            bossActorId,
            MirrorPhaseGuardEffectId);
    }

    private void RemoveMirrorEffect(string effectId, DateTimeOffset now)
    {
        if (_mirrorBossActorId is not { } bossActorId)
            return;
        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (!boss.ActiveEffects.Any(effect =>
                string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)))
        {
            return;
        }

        ApplyKernelEvents(
            EffectEngine.Remove(boss, effectId, now),
            bossActorId,
            bossActorId,
            effectId);
    }

    private void ValidateMirrorProfile(MirrorCombatEncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile.Adds);
        MirrorEncounterAddRole[] roles = Enum.GetValues<MirrorEncounterAddRole>();
        if (profile.Adds.Count != roles.Length
            || profile.Adds.Select(item => item.Role).Distinct().Count() != roles.Length
            || roles.Any(role => profile.Adds.All(item => item.Role != role)))
        {
            throw new ArgumentException(
                "Mirror encounter requires exactly one Guardian, Priest, and Executioner add profile.",
                nameof(profile));
        }

        foreach (MirrorEncounterAddProfile add in profile.Adds)
        {
            ArgumentNullException.ThrowIfNull(add.Monster);
            ArgumentNullException.ThrowIfNull(add.AiProfile);
            if (add.Monster.MaxHp <= 0 || add.Monster.AutoAttackInterval <= TimeSpan.Zero)
                throw new ArgumentException("Mirror add combat profile is invalid.", nameof(profile));
            if (add.Monster.AbilityIds.Any(abilityId => !_abilities.ContainsKey(abilityId)))
                throw new ArgumentException("Mirror add references an unknown ability.", nameof(profile));
            if (add.AiProfile.PriorityAbilityIds.Any(abilityId =>
                    !add.Monster.AbilityIds.Contains(abilityId, StringComparer.Ordinal)))
            {
                throw new ArgumentException(
                    "Mirror add AI references an ability the add does not know.",
                    nameof(profile));
            }
        }
    }
}
