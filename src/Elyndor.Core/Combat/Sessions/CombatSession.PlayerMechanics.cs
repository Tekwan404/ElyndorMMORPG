using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Contribution;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly Action<CombatEvent>? _mechanicsEventSink;

    internal static CombatSession CreatePlayerMechanics(
        Guid sessionId,
        CombatPlayerDefinition owner,
        CombatParticipantDefinition opponent,
        CombatRuntimeState ownerRuntime,
        CombatRuntimeState opponentRuntime,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IGameRandom random,
        DateTimeOffset now,
        Action<CombatEvent> publish) =>
        new(sessionId, owner, opponent, ownerRuntime, opponentRuntime, abilities, random, now, publish);

    private CombatSession(
        Guid sessionId,
        CombatPlayerDefinition owner,
        CombatParticipantDefinition opponent,
        CombatRuntimeState ownerRuntime,
        CombatRuntimeState opponentRuntime,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IGameRandom random,
        DateTimeOffset now,
        Action<CombatEvent> publish)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(opponent);
        ArgumentNullException.ThrowIfNull(ownerRuntime);
        ArgumentNullException.ThrowIfNull(opponentRuntime);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(publish);
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        if (owner.Participant.Kind != CombatActorKind.Player
            || opponent.Kind != CombatActorKind.Player
            || owner.Participant.Actor.ActorId == opponent.Actor.ActorId
            || !ReferenceEquals(ownerRuntime.Actor, owner.Participant.Actor)
            || !ReferenceEquals(opponentRuntime.Actor, opponent.Actor))
        {
            throw new ArgumentException("Mechanics hosts require distinct players and their shared runtimes.");
        }
        ValidateAutoAttack(owner.Participant.AutoAttack);
        if (owner.Participant.OffHandAutoAttack is { } offHand)
            ValidateAutoAttack(offHand);
        if (owner.Participant.ResourceRegenPerSecond < 0)
            throw new ArgumentException("Resource regeneration cannot be negative.", nameof(owner));

        _mechanicsEventSink = publish;
        _random = random;
        _combatStartedAtUtc = now;
        _abilities = abilities;
        _enemies = [opponent];
        _enemiesById = new() { [opponent.Actor.ActorId] = opponent };
        _primaryEnemyActorId = opponent.Actor.ActorId;
        _enemyRuntimes = new() { [opponent.Actor.ActorId] = opponentRuntime };
        _enemyAiRuntimes = [];
        // Existing Guardian handlers require these local stores even for block rage,
        // Revenge readiness and control. They never select targets or drive host AI.
        _enemyThreatTables = new() { [opponent.Actor.ActorId] = new ThreatTable() };
        _enemyForcedTargets = new() { [opponent.Actor.ActorId] = new ForcedTargetState() };
        _companion = null;
        _companionRuntime = null;
        _summonProfile = null;
        _activePlayerState = new CombatPlayerRuntimeState(owner, ownerRuntime)
        {
            SelectedTargetActorId = opponent.Actor.ActorId,
            LastResourceRegenAtUtc = now
        };
        _activePlayerState.InitializeTalentRuntime(random, _procGuard);
        _playerStatesByActorId = new() { [owner.Participant.Actor.ActorId] = _activePlayerState };
        _participantRoster = new CombatParticipantRoster(
            [new CombatParticipantIdentity(owner.AccountId, owner.Participant.Actor.ActorId,
                owner.Participant.Actor.ActorId)], now);
        _participantRoster.TryAttach(owner.Participant.Actor.ActorId, now, out _);
        _contributionLedger = new ContributionLedger(new Dictionary<Guid, Guid>());
        SessionId = sessionId;
        ContentVersion = "PLAYER_MECHANICS";
        BalanceVersion = "PLAYER_MECHANICS";
        CurrentTimeUtc = now;
        Status = CombatSessionStatus.Active;

        // Production defense is expressed by actor stats, effects and class hooks.
        // Replace preconfigured adapter defenses rather than composing them twice.
        _player.Actor.IncomingDamageModifier = null;
        _player.Actor.IncomingControlDurationMultiplier = 1;
        _player.Actor.IncomingCriticalDamageReductionPercent = 0;
        _player.Actor.OwnShieldMagnitudeMultiplier = 1;
        InitializeSetPassiveLoadoutSnapshot();
        if (IsActivePaladin)
            _ = ActivePaladinState();
        ApplyGuardianStartingEffects(now);
        ApplyWarlordPassiveEffects(now);
        InitializePlayerLoadoutMechanics(now);
        SyncMechanicsConditionalEffects(now);
    }

    private void InitializePlayerLoadoutMechanics(DateTimeOffset now)
    {
        InitializePaladinLoadouts(now);
        CombatPlayerRuntimeState previous = _activePlayerState;
        try
        {
            foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values)
            {
                _activePlayerState = state;
                ConfigureMageIncomingDamage();
            }
        }
        finally
        {
            _activePlayerState = previous;
        }
    }

    internal AbilityDefinition ResolveMechanicsAbility(AbilityDefinition baseAbility, DateTimeOffset now)
    {
        SyncBerserkerConditionalEffects(now);
        SyncArcherConditionalEffects(now);
        return ResolveMechanicsAbilityForSnapshot(baseAbility, now);
    }

    internal AbilityDefinition ResolveMechanicsAbilityForSnapshot(
        AbilityDefinition baseAbility, DateTimeOffset now) =>
        ResolveArcherAbility(ResolveMageAbility(ResolvePyromancerAbility(
            ResolveWarlordAbility(ResolvePlayerAbility(baseAbility, now), now), now), now), now);

    internal IReadOnlyDictionary<Guid, AbilityTargetModifier> ResolveMechanicsTargetModifiers(
        AbilityDefinition ability, IReadOnlyList<Guid> targetIds, DateTimeOffset now) =>
        ResolvePlayerAbilityTargetModifiers(ability, targetIds, now);

    internal void MechanicsAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        EventRouter.DispatchAbilityStarted(ability, now);
    }

    internal void MechanicsAbilityResolved(
        AbilityDefinition ability, AbilityExecutionResult execution, DateTimeOffset now)
    {
        EventRouter.DispatchAbilityResolved(ability, execution, now);
    }

    internal void HandleMechanicsEvents(
        IEnumerable<CombatEvent> events, DateTimeOffset now, string? definitionId = null,
        Guid? sourceActorId = null, Guid? targetActorId = null)
    {
        CurrentTimeUtc = now;
        ApplyKernelEvents(events, sourceActorId ?? _player.Actor.ActorId,
            targetActorId ?? _enemy.Actor.ActorId, definitionId);
    }

    // Forwarded events already exist in the enclosing fight's log. Observe only this
    // defender's reactions; never append the input or run the attacker's hooks again.
    internal void ObserveIncomingMechanicsEvent(CombatEvent combatEvent)
    {
        if (combatEvent.SourceActorId == _player.Actor.ActorId
            || combatEvent.TargetActorId != _player.Actor.ActorId)
            return;
        CurrentTimeUtc = combatEvent.OccurredAtUtc;
        EventRouter.Dispatch(combatEvent, hosted: true);
        ProcessGenericDamageReflection(combatEvent);
        if (!_player.Actor.IsDead
            && combatEvent.Type is CombatEventType.EffectApplied
                or CombatEventType.EffectRefreshed
                or CombatEventType.EffectRemoved
                or CombatEventType.EffectExpired)
            SyncMechanicsConditionalEffects(combatEvent.OccurredAtUtc);
    }

    internal void ResolveMechanicsAutoAttack(
        AutoAttackProfile profile, DateTimeOffset now)
    {
        CurrentTimeUtc = now;
        ResolvePlayerAutoAttack(_enemy, profile, now);
    }

    internal TimeSpan MechanicsAutoAttackInterval(AutoAttackProfile profile, DateTimeOffset now) =>
        EffectivePlayerAutoAttackInterval(profile, now);

    // EffectEngine.Process consumes these damage events and returns them with its tick
    // events. The enclosing fight publishes that combined batch exactly once.
    internal IReadOnlyList<CombatEvent> ResolveMechanicsPeriodicDamage(
        ActiveEffect effect, CombatActorState target, DateTimeOffset now) =>
        ResolvePeriodicEffectDamage(effect, target, now)
            .Select(item => item with
            {
                DefinitionId = item.DefinitionId ?? effect.Definition.Id,
                SourceActorId = item.SourceActorId ?? effect.SourceId,
                TargetActorId = item.TargetActorId ?? target.ActorId,
                IsPeriodic = true
            }).ToArray();

    internal void ResolveMechanicsExpiredEffect(
        ActiveEffect effect, CombatActorState target, DateTimeOffset now)
    {
        HandleMechanicsEvents(ResolveExpiredEffectActions(effect, target, now), now,
            effect.Definition.Id, effect.SourceId, target.ActorId);
        if (!_player.Actor.IsDead)
            SyncMechanicsConditionalEffects(now);
    }

    internal void AdvanceMechanics(DateTimeOffset now)
    {
        if (now < CurrentTimeUtc)
            throw new ArgumentOutOfRangeException(nameof(now), "Combat time cannot move backwards.");
        ApplyPlayerResourceRegen(now);
        CurrentTimeUtc = now;
        if (_player.Actor.IsDead)
            return;
        SyncMechanicsConditionalEffects(now);
    }

    private void SyncMechanicsConditionalEffects(DateTimeOffset now)
    {
        SyncBerserkerConditionalEffects(now);
        SyncGuardianConditionalEffects(now);
        SyncMageConditionalEffects(now);
        SyncArcherConditionalEffects(now);
    }

    internal DateTimeOffset? NextMechanicsDueAt
    {
        get
        {
            if (_player.Actor.IsDead)
                return null;
            DateTimeOffset? next = _coldBloodReadyAtUtc;
            next = Min(next, _nextSpiritBondAtUtc);
            foreach (PendingMageResourceRefund refund in _pendingMageResourceRefunds)
                next = Min(next, refund.DueAtUtc);
            return next;
        }
    }

    private void ApplyMechanicsKernelEvents(IEnumerable<CombatEvent> events)
    {
        foreach (CombatEvent combatEvent in events)
        {
            if (combatEvent.Type == CombatEventType.ActorDied
                && !_deadActors.Add(combatEvent.ActorId))
                continue;
            Append(combatEvent);
            EventRouter.Dispatch(combatEvent, hosted: true);
            if (combatEvent.TargetActorId == _player.Actor.ActorId)
                ProcessGenericDamageReflection(combatEvent);
            // CurrentHp also catches simultaneous deaths while the enclosing fight
            // temporarily defers IsDead during timestamp batching.
            if (combatEvent.Type == CombatEventType.ActorDied
                && _player.Actor.CurrentHp > 0
                && _enemiesById.TryGetValue(combatEvent.ActorId, out CombatParticipantDefinition? killed)
                && !killed.IsCombatObject)
                ApplyPlayerEnemyKilledHooks(combatEvent, killed);
        }
    }

    private void ApplyPlayerEnemyKilledHooks(
        CombatEvent death, CombatParticipantDefinition killedEnemy)
    {
        Append(new CombatEvent(
            CombatEventType.EnemyKilled,
            death.OccurredAtUtc,
            _player.Actor.ActorId,
            killedEnemy.DefinitionId,
            SourceActorId: death.SourceActorId ?? _player.Actor.ActorId,
            TargetActorId: killedEnemy.Actor.ActorId,
            IsPeriodic: death.IsPeriodic,
            DamageType: death.DamageType,
            WeaponHand: death.WeaponHand,
            WeaponDefinitionId: death.WeaponDefinitionId,
            IsUnblockable: death.IsUnblockable,
            IsCritical: death.IsCritical,
            IsReflected: death.IsReflected));
        EventRouter.DispatchKill(death);
    }
}
