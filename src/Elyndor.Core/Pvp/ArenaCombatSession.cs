using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Resources;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

public sealed record ArenaFighter(Guid AccountId, Guid CharacterId, CombatActorState Actor,
    IReadOnlyDictionary<string, AbilityDefinition> Abilities, AutoAttackProfile AutoAttack,
    ResolvedTalentModifiers? TalentModifiers = null, decimal ResourceRegenPerSecond = 0,
    bool CanAutoAttack = true, AutoAttackProfile? OffHandAutoAttack = null,
    CombatPlayerDefinition? PlayerDefinition = null,
    IReadOnlyDictionary<string, AbilityDefinition>? BaseAbilities = null)
{
    public ResolvedTalentModifiers EffectiveTalentModifiers =>
        TalentModifiers ?? ResolvedTalentModifiers.Empty;
}

public sealed record ArenaActorSnapshot(Guid ActorId, decimal CurrentHp, decimal MaxHp,
    decimal CurrentResource, decimal MaxResource);

public sealed record ArenaCombatSnapshot(Guid MatchId, long Sequence, ArenaMatchOutcome Outcome,
    ArenaActorSnapshot ActorA, ArenaActorSnapshot ActorB);

public sealed record ArenaCommandResult(bool Succeeded, string? ErrorCode, ArenaCombatSnapshot Snapshot,
    IReadOnlyList<CombatEvent> Events);

public sealed class ArenaCombatSession
{
    private readonly ProcGuard _procGuard = new();
    private bool _executingTalentProc;
    private static readonly AbilityDefinition AutoAttackTalentMarker = new(
        "AUTO_ATTACK", AbilityType.Instant, AbilityTargetType.SingleEnemy,
        0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None,
        false, "PHYSICAL");
    private readonly ArenaFighter _first;
    private readonly ArenaFighter _second;
    private readonly CombatRuntimeState _firstRuntime;
    private readonly CombatRuntimeState _secondRuntime;
    private readonly IGameRandom _random;
    private readonly DateTimeOffset _startedAt;
    private DateTimeOffset _advancedTo;
    private DateTimeOffset _firstAutoAttackAt;
    private DateTimeOffset _secondAutoAttackAt;
    private DateTimeOffset _firstOffHandAttackAt;
    private DateTimeOffset _secondOffHandAttackAt;
    private bool _firstAutoAttackEnabled;
    private bool _secondAutoAttackEnabled;
    private long _sequence;
    private readonly List<CombatEvent> _events = [];
    private const int MaximumAdvanceSteps = 10_000;
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _duration;
    private readonly HashSet<(Guid AccountId, string CommandId)> _seenCommands = [];
    private readonly Dictionary<Guid, CrowdControlDiminishingReturns> _crowdControlDr;
    private CombatSession? _firstMechanics;
    private CombatSession? _secondMechanics;

    public ArenaCombatSession(Guid matchId, ArenaFighter first, ArenaFighter second,
        IGameRandom random, DateTimeOffset startedAt, TimeSpan? duration = null)
    {
        _duration = duration ?? DefaultDuration;
        if (_duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (matchId == Guid.Empty || first.AccountId == Guid.Empty || second.AccountId == Guid.Empty
            || first.AccountId == second.AccountId || first.CharacterId == Guid.Empty
            || second.CharacterId == Guid.Empty || first.CharacterId == second.CharacterId
            || first.Actor.ActorId == second.Actor.ActorId)
            throw new ArgumentException("Arena requires two distinct authenticated player actors.");
        if (startedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Start time must be UTC.");
        if (first.AutoAttack.Interval <= TimeSpan.Zero || second.AutoAttack.Interval <= TimeSpan.Zero)
            throw new ArgumentException("Auto attack interval must be positive.");
        if (first.OffHandAutoAttack is { } firstOffHand && firstOffHand.Interval <= TimeSpan.Zero
            || second.OffHandAutoAttack is { } secondOffHand && secondOffHand.Interval <= TimeSpan.Zero)
            throw new ArgumentException("Off-hand attack interval must be positive.");
        if (first.ResourceRegenPerSecond < 0 || second.ResourceRegenPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(first), "Resource regeneration cannot be negative.");
        ValidateAbilities(first.Abilities);
        ValidateAbilities(second.Abilities);
        bool hasPlayerMechanics = first.PlayerDefinition is not null && second.PlayerDefinition is not null;
        ValidateClassAbilityHandlers(first, hasPlayerMechanics);
        ValidateClassAbilityHandlers(second, hasPlayerMechanics);
        MatchId = matchId;
        _first = first;
        _second = second;
        _random = random;
        _startedAt = startedAt;
        _advancedTo = startedAt;
        _firstAutoAttackEnabled = first.CanAutoAttack;
        _secondAutoAttackEnabled = second.CanAutoAttack;
        _firstAutoAttackAt = first.CanAutoAttack
            ? startedAt + first.AutoAttack.Interval
            : DateTimeOffset.MaxValue;
        _secondAutoAttackAt = second.CanAutoAttack
            ? startedAt + second.AutoAttack.Interval
            : DateTimeOffset.MaxValue;
        _firstOffHandAttackAt = first.CanAutoAttack && first.OffHandAutoAttack is { } firstWeapon
            ? startedAt + InitialOffHandDelay(firstWeapon) : DateTimeOffset.MaxValue;
        _secondOffHandAttackAt = second.CanAutoAttack && second.OffHandAutoAttack is { } secondWeapon
            ? startedAt + InitialOffHandDelay(secondWeapon) : DateTimeOffset.MaxValue;
        _firstRuntime = new CombatRuntimeState(first.Actor);
        _firstRuntime.AddActor(second.Actor);
        _secondRuntime = new CombatRuntimeState(second.Actor);
        _secondRuntime.AddActor(first.Actor);
        _crowdControlDr = new Dictionary<Guid, CrowdControlDiminishingReturns>
        {
            [first.Actor.ActorId] = new CrowdControlDiminishingReturns(),
            [second.Actor.ActorId] = new CrowdControlDiminishingReturns()
        };
        ConfigureCrowdControlPolicy(first.Actor);
        ConfigureCrowdControlPolicy(second.Actor);
        Append(new CombatEvent(CombatEventType.CombatStarted, startedAt, first.Actor.ActorId));
        if (first.PlayerDefinition is { } firstPlayer && second.PlayerDefinition is { } secondPlayer)
        {
            _firstMechanics = CombatSession.CreatePlayerMechanics(matchId, firstPlayer,
                secondPlayer.Participant, _firstRuntime, _secondRuntime,
                first.BaseAbilities ?? first.Abilities, random, startedAt,
                e => PublishMechanicsEvent(first.Actor.ActorId, e));
            _secondMechanics = CombatSession.CreatePlayerMechanics(matchId, secondPlayer,
                firstPlayer.Participant, _secondRuntime, _firstRuntime,
                second.BaseAbilities ?? second.Abilities, random, startedAt,
                e => PublishMechanicsEvent(second.Actor.ActorId, e));
        }
    }

    public Guid MatchId { get; }
    public DateTimeOffset StartedAtUtc => _startedAt;
    public DateTimeOffset TimeoutAtUtc => _startedAt + _duration;
    public Guid FirstAccountId => _first.AccountId;
    public Guid SecondAccountId => _second.AccountId;
    public ArenaMatchOutcome Outcome { get; private set; } = ArenaMatchOutcome.Active;
    public ArenaCombatSnapshot Snapshot => new(MatchId, _sequence, Outcome,
        ActorSnapshot(_first.Actor), ActorSnapshot(_second.Actor));
    public IReadOnlyList<CombatEvent> GetEventsAfter(long sequence) =>
        _events.Where(x => x.Sequence > sequence).ToArray();

    public IReadOnlyDictionary<string, DateTimeOffset> CooldownsFor(Guid accountId) =>
        new Dictionary<string, DateTimeOffset>(RuntimeFor(accountId).Cooldowns, StringComparer.Ordinal);

    public ActiveCast? ActiveCastFor(Guid accountId) => RuntimeFor(accountId).ActiveCast;

    public IReadOnlyList<ActiveEffect> ActiveEffectsFor(Guid accountId) =>
        RuntimeFor(accountId).Actor.ActiveEffects.ToArray();

    public IReadOnlyList<CombatAbilitySnapshot> AbilitySnapshotsFor(Guid accountId)
    {
        ArenaFighter fighter = accountId == _first.AccountId ? _first
            : accountId == _second.AccountId ? _second
            : throw new ArgumentException("Account is not an arena participant.");
        CombatSession? mechanics = MechanicsFor(fighter.Actor.ActorId);
        return fighter.Abilities.Values.Select(ability =>
        {
            AbilityDefinition resolved = mechanics is null ? ability
                : mechanics.ResolveMechanicsAbilityForSnapshot(
                    (fighter.BaseAbilities ?? fighter.Abilities)[ability.Id], _advancedTo);
            return new CombatAbilitySnapshot(resolved.Id, resolved.ResourceCost,
                resolved.Cooldown, resolved.TargetType);
        }).ToArray();
    }

    private CombatRuntimeState RuntimeFor(Guid accountId) => accountId == _first.AccountId
        ? _firstRuntime : accountId == _second.AccountId
            ? _secondRuntime : throw new ArgumentException("Account is not an arena participant.");

    private void ConfigureCrowdControlPolicy(CombatActorState target)
    {
        target.EffectApplicationPolicy = (sourceId, definition, now) =>
        {
            if (sourceId == target.ActorId
                || !CrowdControlCategoryResolver.TryResolve(definition.Kind, out CrowdControlCategory category))
                return new EffectApplicationPolicyResult(definition);

            CrowdControlDrResolution resolution = _crowdControlDr[target.ActorId]
                .Resolve(category, definition.Duration, now);
            if (resolution.IsImmune)
                return new EffectApplicationPolicyResult(null);
            return new EffectApplicationPolicyResult(
                definition with { Duration = resolution.EffectiveDuration },
                () =>
                {
                    _crowdControlDr[target.ActorId].Commit(resolution, now + resolution.EffectiveDuration);
                    RefreshResetWindow(target, category, now);
                    ApplyControlConsequences(sourceId, target, category, now);
                });
        };
    }

    private CombatRuntimeState RuntimeForActor(Guid actorId) => actorId == _first.Actor.ActorId
        ? _firstRuntime : actorId == _second.Actor.ActorId
            ? _secondRuntime : throw new ArgumentException("Actor is not an arena participant.");

    private CombatSession? MechanicsFor(Guid actorId) => actorId == _first.Actor.ActorId
        ? _firstMechanics : actorId == _second.Actor.ActorId ? _secondMechanics : null;

    private void PublishMechanicsEvent(Guid ownerActorId, CombatEvent combatEvent)
    {
        Append(combatEvent);
        if (combatEvent.TargetActorId is { } targetId && targetId != ownerActorId)
            MechanicsFor(targetId)?.ObserveIncomingMechanicsEvent(combatEvent);
    }

    private void ProcessKernelEvents(IEnumerable<CombatEvent> events, Guid sourceId,
        DateTimeOffset now, string? abilityId = null, Guid? targetId = null)
    {
        if (MechanicsFor(sourceId) is { } mechanics)
            mechanics.HandleMechanicsEvents(events, now, abilityId, sourceId, targetId);
        else
            Append(events);
    }

    private ArenaFighter FighterForActor(Guid actorId) => actorId == _first.Actor.ActorId
        ? _first : actorId == _second.Actor.ActorId
            ? _second : throw new ArgumentException("Actor is not an arena participant.");

    public bool Forfeit(Guid accountId, DateTimeOffset now)
    {
        if (accountId != _first.AccountId && accountId != _second.AccountId)
            return false;
        if (now.Offset != TimeSpan.Zero || now < _advancedTo)
            return false;
        // Resolve everything that happened before the forfeit so an earlier death/timeout wins.
        AdvanceTo(now);
        if (Outcome != ArenaMatchOutcome.Active)
            return false;
        Outcome = accountId == _first.AccountId ? ArenaMatchOutcome.WinnerB : ArenaMatchOutcome.WinnerA;
        _advancedTo = now;
        Append(new CombatEvent(CombatEventType.CombatEnded, now, _first.Actor.ActorId,
            Outcome.ToString()));
        return true;
    }

    /// <summary>Infrastructure cancellation (e.g. both players gone): no winner, no rewards.</summary>
    public bool Cancel(DateTimeOffset now)
    {
        if (now.Offset != TimeSpan.Zero || now < _advancedTo || Outcome != ArenaMatchOutcome.Active)
            return false;
        Outcome = ArenaMatchOutcome.Cancelled;
        _advancedTo = now;
        Append(new CombatEvent(CombatEventType.CombatEnded, now, _first.Actor.ActorId,
            Outcome.ToString()));
        return true;
    }

    public bool AutoAttackEnabledFor(Guid accountId) =>
        accountId == _first.AccountId ? _firstAutoAttackEnabled
            : accountId == _second.AccountId && _secondAutoAttackEnabled;

    public ArenaCommandResult SetAutoAttack(Guid accountId, string commandId, bool enabled, DateTimeOffset now)
    {
        bool first = accountId == _first.AccountId;
        if (!first && accountId != _second.AccountId) return Result(false, "arena_not_participant", _sequence);
        if (now.Offset != TimeSpan.Zero || now < _advancedTo) return Result(false, "arena_invalid_time", _sequence);
        if (string.IsNullOrWhiteSpace(commandId) || commandId.Length > 64)
            return Result(false, "arena_invalid_command", _sequence);
        AdvanceTo(now);
        long before = _sequence;
        if (Outcome != ArenaMatchOutcome.Active) return Result(false, "arena_ended", before);
        if (_seenCommands.Contains((accountId, commandId))) return Result(false, "arena_duplicate_command", before);
        ArenaFighter fighter = first ? _first : _second;
        if (enabled && !fighter.CanAutoAttack) return Result(false, "arena_auto_attack_unavailable", before);
        _seenCommands.Add((accountId, commandId));
        if (AutoAttackEnabledFor(accountId) == enabled) return Result(true, null, before);
        TimeSpan interval = MechanicsFor(fighter.Actor.ActorId)?.MechanicsAutoAttackInterval(fighter.AutoAttack, now)
            ?? fighter.AutoAttack.Interval;
        DateTimeOffset next = enabled ? now + interval : DateTimeOffset.MaxValue;
        DateTimeOffset offHand = enabled && fighter.OffHandAutoAttack is { } weapon
            ? now + TimeSpan.FromTicks((MechanicsFor(fighter.Actor.ActorId)?.MechanicsAutoAttackInterval(weapon, now)
                ?? weapon.Interval).Ticks / 2) : DateTimeOffset.MaxValue;
        if (first)
        {
            _firstAutoAttackEnabled = enabled;
            _firstAutoAttackAt = next;
            _firstOffHandAttackAt = offHand;
        }
        else
        {
            _secondAutoAttackEnabled = enabled;
            _secondAutoAttackAt = next;
            _secondOffHandAttackAt = offHand;
        }
        Append(new CombatEvent(enabled ? CombatEventType.AutoAttackStarted : CombatEventType.AutoAttackStopped,
            now, fighter.Actor.ActorId));
        return Result(true, null, before);
    }

    public ArenaCommandResult UseAbility(Guid accountId, string commandId, string abilityId,
        Guid targetActorId, DateTimeOffset now)
    {
        bool isFirst = accountId == _first.AccountId;
        if (!isFirst && accountId != _second.AccountId)
            return Result(false, "arena_not_participant", _sequence);
        if (now.Offset != TimeSpan.Zero || now < _advancedTo)
            return Result(false, "arena_invalid_time", _sequence);
        if (string.IsNullOrWhiteSpace(commandId) || commandId.Length > 64)
            return Result(false, "arena_invalid_command", _sequence);
        AdvanceTo(now);
        long before = _sequence;
        if (Outcome != ArenaMatchOutcome.Active) return Result(false, "arena_ended", before);
        if (_seenCommands.Contains((accountId, commandId)))
            return Result(false, "arena_duplicate_command", before);
        ArenaFighter fighter = isFirst ? _first : _second;
        ArenaFighter opponent = isFirst ? _second : _first;
        CombatRuntimeState runtime = isFirst ? _firstRuntime : _secondRuntime;
        if (!fighter.Abilities.TryGetValue(abilityId, out AbilityDefinition? ability))
            return Result(false, "arena_ability_not_known", before);
        CombatSession? mechanics = MechanicsFor(fighter.Actor.ActorId);
        if (mechanics is not null)
            ability = mechanics.ResolveMechanicsAbility(
                (fighter.BaseAbilities ?? fighter.Abilities)[abilityId], now);
        Guid expectedTarget = ability.TargetType switch
        {
            AbilityTargetType.Self or AbilityTargetType.SingleAlly => fighter.Actor.ActorId,
            AbilityTargetType.SelfAndPartyMembersInCombat when IsSupportedSoloPartyAbility(ability) =>
                fighter.Actor.ActorId,
            AbilityTargetType.SingleEnemy or AbilityTargetType.AllEnemiesInCombat
                or AbilityTargetType.NEnemiesInCombat => opponent.Actor.ActorId,
            _ => Guid.Empty
        };
        if (expectedTarget == Guid.Empty || targetActorId != expectedTarget)
            return Result(false, "arena_invalid_target", before);
        if (ability.TargetType == AbilityTargetType.SingleAlly && !ability.AllowSelfTarget)
            return Result(false, "arena_invalid_target", before);
        IReadOnlyList<Guid>? targetIds = ability.TargetType switch
        {
            AbilityTargetType.AllEnemiesInCombat or AbilityTargetType.NEnemiesInCombat =>
                [opponent.Actor.ActorId],
            AbilityTargetType.SelfAndPartyMembersInCombat when IsSupportedSoloPartyAbility(ability) =>
                [fighter.Actor.ActorId],
            _ => null
        };

        IReadOnlyDictionary<Guid, AbilityTargetModifier>? fireTargetModifiers =
            ability.Type is AbilityType.Casted or AbilityType.Channelled ? null : mechanics is not null
                ? mechanics.ResolveMechanicsTargetModifiers(ability, targetIds ?? [targetActorId], now)
                : null;
        AbilityExecutionResult execution = AbilityEngine.Execute(runtime, ability,
            new AbilityIntent(commandId, abilityId, targetActorId, targetIds, fireTargetModifiers), now, _random);
        if (!execution.Succeeded)
            return Result(false, execution.ErrorCode.ToString(), before);
        _seenCommands.Add((accountId, commandId));
        ProcessKernelEvents(execution.Events, fighter.Actor.ActorId, now, ability.Id, targetActorId);
        mechanics?.MechanicsAbilityStarted(ability, now);
        if (ability.Type is not (AbilityType.Casted or AbilityType.Channelled))
        {
            ApplyInterrupt(ability, isFirst ? _secondRuntime : _firstRuntime,
                fighter.Actor.ActorId, now);
            if (mechanics is not null)
                mechanics.MechanicsAbilityResolved(ability, execution, now);
            else
                DispatchSuccessfulAbilityTalentEvents(fighter, targetActorId, ability, execution.Events, now);
        }
        ResolveOutcome(now);
        return Result(true, null, before);
    }

    public void AdvanceTo(DateTimeOffset now)
    {
        if (now.Offset != TimeSpan.Zero || now < _advancedTo)
            throw new ArgumentOutOfRangeException(nameof(now));
        int steps = 0;
        DateTimeOffset resourceAdvancedTo = _advancedTo;
        while (Outcome == ArenaMatchOutcome.Active && ++steps <= MaximumAdvanceSteps)
        {
            DateTimeOffset? next = NextExecutionAt();
            if (next is null || next > now) break;
            DateTimeOffset due = next.Value;
            RegenerateResources(resourceAdvancedTo, due);
            resourceAdvancedTo = due;
            ResolveTimestampBatch(due);
            ResolveOutcome(due);
        }
        if (steps > MaximumAdvanceSteps)
            throw new InvalidOperationException("Arena execution exceeded the safe step limit.");
        if (Outcome == ArenaMatchOutcome.Active)
            RegenerateResources(resourceAdvancedTo, now);
        _advancedTo = now;
    }

    private void ResolveTimestampBatch(DateTimeOffset due)
    {
        TimestampBatchEntry[] entries =
        [
            SnapshotTimestampEntry(
                _first,
                _second,
                _firstRuntime,
                _firstAutoAttackAt,
                _firstOffHandAttackAt,
                due,
                isFirst: true),
            SnapshotTimestampEntry(
                _second,
                _first,
                _secondRuntime,
                _secondAutoAttackAt,
                _secondOffHandAttackAt,
                due,
                isFirst: false)
        ];
        Array.Sort(entries, static (left, right) =>
            left.Fighter.Actor.ActorId.CompareTo(right.Fighter.Actor.ActorId));

        long batchStartSequence = _sequence;
        foreach (TimestampBatchEntry entry in entries)
        {
            if (entry.DueCast is not null
                && entry.Runtime.ActiveCast?.NextResolutionAtUtc <= due)
            {
                // A cast that was already due at T is committed to this batch. Remove it from
                // mutable runtime state so another action at T cannot retroactively interrupt it.
                entry.Runtime.ActiveCast = null;
            }
        }

        IDisposable? firstDeathDeferral = entries.Single(entry => entry.IsFirst).WasAlive
            ? _first.Actor.DeferDeathForCurrentBatch()
            : null;
        IDisposable? secondDeathDeferral = entries.Single(entry => !entry.IsFirst).WasAlive
            ? _second.Actor.DeferDeathForCurrentBatch()
            : null;
        try
        {
            // Effects are processed from the batch-start snapshot before casts can dispel or
            // replace a tick that was already due at this timestamp.
            foreach (TimestampBatchEntry entry in entries)
            {
                ActiveEffect[] expiring = entry.Fighter.Actor.ActiveEffects
                    .Where(effect => effect.ExpiresAtUtc <= due).ToArray();
                IReadOnlyList<CombatEvent> effectEvents = EffectEngine.Process(
                    entry.Fighter.Actor,
                    due,
                    (effect, tick) => ResolvePeriodicDamage(effect, entry.Fighter.Actor, tick));
                foreach (CombatEvent e in effectEvents)
                    ProcessKernelEvents([e], e.SourceActorId ?? entry.Fighter.Actor.ActorId,
                        e.OccurredAtUtc, e.DefinitionId, e.TargetActorId);
                foreach (ActiveEffect expired in expiring)
                    if (!entry.Fighter.Actor.ActiveEffects.Contains(expired))
                        ResolveExpiredEffect(expired, entry.Fighter.Actor, expired.ExpiresAtUtc);
            }

            foreach (TimestampBatchEntry entry in entries)
                CompleteCast(entry.Runtime, entry.DueCast, entry.WasAlive, due);

            foreach (TimestampBatchEntry entry in entries)
                ResolvePendingActions(entry.Runtime, entry.DuePendingActions, entry.WasAlive, due);

            foreach (TimestampBatchEntry entry in entries)
            {
                if (entry.IsFirst)
                {
                    ResolveAutoAttack(
                        entry.Fighter,
                        entry.Opponent,
                        entry.Fighter.AutoAttack,
                        ref _firstAutoAttackAt,
                        due,
                        entry.AutoAttackWasDue,
                        entry.WasAlive,
                        entry.OpponentWasAlive,
                        entry.WasStunned);
                    if (entry.Fighter.OffHandAutoAttack is { } offHand)
                        ResolveAutoAttack(entry.Fighter, entry.Opponent, offHand,
                            ref _firstOffHandAttackAt, due, entry.OffHandWasDue,
                            entry.WasAlive, entry.OpponentWasAlive, entry.WasStunned);
                }
                else
                {
                    ResolveAutoAttack(
                        entry.Fighter,
                        entry.Opponent,
                        entry.Fighter.AutoAttack,
                        ref _secondAutoAttackAt,
                        due,
                        entry.AutoAttackWasDue,
                        entry.WasAlive,
                        entry.OpponentWasAlive,
                        entry.WasStunned);
                    if (entry.Fighter.OffHandAutoAttack is { } offHand)
                        ResolveAutoAttack(entry.Fighter, entry.Opponent, offHand,
                            ref _secondOffHandAttackAt, due, entry.OffHandWasDue,
                            entry.WasAlive, entry.OpponentWasAlive, entry.WasStunned);
                }
            }
        }
        finally
        {
            secondDeathDeferral?.Dispose();
            firstDeathDeferral?.Dispose();
        }

        AppendDeferredDeathIfNeeded(_first.Actor, entries.Single(entry => entry.IsFirst).WasAlive,
            due, batchStartSequence);
        AppendDeferredDeathIfNeeded(_second.Actor, entries.Single(entry => !entry.IsFirst).WasAlive,
            due, batchStartSequence);
    }

    private static TimestampBatchEntry SnapshotTimestampEntry(
        ArenaFighter fighter,
        ArenaFighter opponent,
        CombatRuntimeState runtime,
        DateTimeOffset nextAutoAttackAt,
        DateTimeOffset nextOffHandAttackAt,
        DateTimeOffset due,
        bool isFirst)
    {
        bool wasAlive = !fighter.Actor.IsDead;
        bool opponentWasAlive = !opponent.Actor.IsDead;
        ActiveCast? dueCast = runtime.ActiveCast is { } cast && cast.NextResolutionAtUtc <= due
            ? cast
            : null;
        PendingAbilityAction[] duePendingActions = runtime.PendingActions
            .Where(action => action.ExecuteAtUtc <= due)
            .OrderBy(action => action.ExecuteAtUtc)
            .ThenBy(action => action.Sequence)
            .ToArray();
        return new TimestampBatchEntry(
            fighter,
            opponent,
            runtime,
            dueCast,
            duePendingActions,
            wasAlive,
            opponentWasAlive,
            fighter.CanAutoAttack && nextAutoAttackAt <= due,
            fighter.CanAutoAttack && fighter.OffHandAutoAttack is not null
                && nextOffHandAttackAt <= due,
            wasAlive && AutoAttackControlError(fighter.Actor, due) is not null,
            isFirst);
    }

    private void AppendDeferredDeathIfNeeded(
        CombatActorState actor,
        bool wasAlive,
        DateTimeOffset due,
        long batchStartSequence)
    {
        if (!wasAlive || !actor.IsDead)
            return;
        if (_events.Any(combatEvent => combatEvent.Sequence > batchStartSequence
            && combatEvent.Type == CombatEventType.ActorDied
            && combatEvent.ActorId == actor.ActorId
            && combatEvent.OccurredAtUtc == due))
        {
            return;
        }

        CombatEvent? cause = _events
            .Where(combatEvent => combatEvent.Sequence > batchStartSequence
                && combatEvent.OccurredAtUtc == due
                && combatEvent.TargetActorId == actor.ActorId
                && combatEvent.Type == CombatEventType.DamageDealt
                && combatEvent.Amount > 0)
            .LastOrDefault();
        CombatEvent death = new(
            CombatEventType.ActorDied,
            due,
            actor.ActorId,
            cause?.DefinitionId,
            SourceActorId: cause?.SourceActorId,
            TargetActorId: actor.ActorId,
            IsPeriodic: cause?.IsPeriodic ?? false,
            DamageType: cause?.DamageType,
            WeaponHand: cause?.WeaponHand,
            WeaponDefinitionId: cause?.WeaponDefinitionId,
            IsUnblockable: cause?.IsUnblockable ?? false,
            IsCritical: cause?.IsCritical ?? false,
            IsReflected: cause?.IsReflected ?? false);
        ProcessKernelEvents([death], cause?.SourceActorId ?? actor.ActorId, due,
            cause?.DefinitionId, actor.ActorId);
    }

    private void RegenerateResources(DateTimeOffset from, DateTimeOffset to)
    {
        if (_firstMechanics is not null && _secondMechanics is not null)
        {
            _firstMechanics.AdvanceMechanics(to);
            _secondMechanics.AdvanceMechanics(to);
            return;
        }
        if (to <= from) return;
        decimal elapsedSeconds = (decimal)(to - from).TotalSeconds;
        RegenerateResource(_first, elapsedSeconds, to);
        RegenerateResource(_second, elapsedSeconds, to);
    }

    private void RegenerateResource(ArenaFighter fighter, decimal elapsedSeconds, DateTimeOffset now)
    {
        if (fighter.ResourceRegenPerSecond <= 0 || fighter.Actor.IsDead) return;
        CombatEvent result = CombatResourceRuntime.Change(
            fighter.Actor, fighter.ResourceRegenPerSecond * elapsedSeconds, now,
            "COMBAT_REGEN", fighter.Actor.ActorId);
        if (result.Amount != 0) Append(result);
    }

    private void CompleteCast(
        CombatRuntimeState runtime,
        ActiveCast? committedCast,
        bool wasAlive,
        DateTimeOffset due)
    {
        if (committedCast is null)
            return;
        if (!wasAlive)
            return;

        ActiveCast cast = committedCast;
        ArenaFighter fighter = runtime == _firstRuntime ? _first : _second;
        ArenaFighter opponent = runtime == _firstRuntime ? _second : _first;
        CombatSession? mechanics = MechanicsFor(fighter.Actor.ActorId);
        IReadOnlyDictionary<Guid, AbilityTargetModifier>? targetModifiers = mechanics is not null
            ? mechanics.ResolveMechanicsTargetModifiers(cast.Ability, cast.TargetIds ?? [cast.TargetId], due)
            : cast.TargetModifiers;
        runtime.ActiveCast = cast with { TargetModifiers = targetModifiers };
        AbilityExecutionResult execution = AbilityEngine.CompleteCast(runtime, due, _random);
        ProcessKernelEvents(execution.Events, fighter.Actor.ActorId, due, cast.Ability.Id, cast.TargetId);
        // The due tick is committed with the batch, but control applied in that
        // same batch must still cancel the rest of a channel.
        if (runtime.ActiveCast?.Ability.Type == AbilityType.Channelled
            && runtime.Actor.ActiveEffects.Any(effect => effect.AppliedAtUtc <= due && effect.ExpiresAtUtc > due
                && (effect.Definition.Kind is EffectKind.Stun or EffectKind.Fear
                    || cast.Ability.IsSpell && effect.Definition.Kind == EffectKind.Silence)))
            ProcessKernelEvents(AbilityEngine.Interrupt(runtime, due, TimeSpan.Zero).Events,
                fighter.Actor.ActorId, due, cast.Ability.Id, cast.TargetId);
        if (!execution.Succeeded || !(execution.Events.Any(e => e.Type == CombatEventType.AbilityCompleted)
            || cast.Ability.Type == AbilityType.Channelled && execution.Events.Any(e => e.Type == CombatEventType.DamageDealt)))
            return;
        ApplyInterrupt(cast.Ability, runtime == _firstRuntime ? _secondRuntime : _firstRuntime,
            runtime.Actor.ActorId, due);
        if (mechanics is not null)
            mechanics.MechanicsAbilityResolved(cast.Ability, execution, due);
        else
            DispatchSuccessfulAbilityTalentEvents(fighter, cast.TargetId, cast.Ability, execution.Events, due);
    }

    private void ResolvePendingActions(
        CombatRuntimeState runtime,
        PendingAbilityAction[] committedActions,
        bool wasAlive,
        DateTimeOffset due)
    {
        if (committedActions.Length == 0)
            return;
        if (!wasAlive)
        {
            foreach (PendingAbilityAction pending in committedActions)
                runtime.PendingActions.Remove(pending);
            return;
        }

        IReadOnlyList<CombatEvent> events = AbilityEngine.ResolvePendingActions(runtime, due, _random);
        ProcessKernelEvents(events, runtime.Actor.ActorId, due);
        if (MechanicsFor(runtime.Actor.ActorId) is null)
            DispatchDamageTalentEvents(events, due);
    }

    private void DispatchSuccessfulAbilityTalentEvents(
        ArenaFighter source,
        Guid targetActorId,
        AbilityDefinition ability,
        IReadOnlyList<CombatEvent> combatEvents,
        DateTimeOffset now)
    {
        IReadOnlyList<ArenaTalentCombatEvent> talentEvents =
            ArenaTalentEventDispatcher.FromSuccessfulAbility(
                source.Actor.ActorId,
                targetActorId,
                ability,
                combatEvents,
                now);
        DispatchTalentEvents(talentEvents, now);
    }

    private void DispatchDamageTalentEvents(
        IReadOnlyList<CombatEvent> combatEvents,
        DateTimeOffset now,
        AbilityDefinition? sourceAbility = null)
    {
        DispatchTalentEvents(ArenaTalentEventDispatcher.FromDamageEvents(combatEvents, sourceAbility), now);
    }

    private void DispatchTalentEvents(
        IReadOnlyList<ArenaTalentCombatEvent> talentEvents,
        DateTimeOffset now)
    {
        foreach (ArenaTalentCombatEvent talentEvent in talentEvents)
        {
            ArenaFighter owner = talentEvent.Type == ArenaTalentEventType.OnDamageTaken
                ? FighterForActor(talentEvent.TargetActorId)
                : FighterForActor(talentEvent.SourceActorId);
            IReadOnlyList<ArenaTalentRuntimeEffect> effects = ArenaTalentEventDispatcher.Dispatch(
                owner.EffectiveTalentModifiers,
                talentEvent,
                _random,
                _procGuard);
            foreach (ArenaTalentRuntimeEffect effect in effects)
            {
                _executingTalentProc = true;
                try
                {
                    ExecuteTalentEffect(effect, now);
                }
                finally
                {
                    _executingTalentProc = false;
                }
            }
        }
    }

    private void ExecuteTalentEffect(ArenaTalentRuntimeEffect effect, DateTimeOffset now)
    {
        switch (effect.Kind)
        {
            case ArenaTalentEffectKind.GainResource:
            {
                CombatActorState target = RuntimeForActor(effect.TargetActorId).Actor;
                Append(CombatResourceRuntime.Change(
                    target, effect.Amount, now, effect.SourceTalentId, effect.SourceActorId));
                break;
            }
            case ArenaTalentEffectKind.ModifyCooldown:
                ArenaTalentStateEffectExecutor.ExecuteCooldownMutation(
                    RuntimeForActor(effect.TargetActorId), effect, now);
                break;
            case ArenaTalentEffectKind.ApplyBuff:
            case ArenaTalentEffectKind.ApplyDebuff:
            {
                CombatActorState target = RuntimeForActor(effect.TargetActorId).Actor;
                if (!target.CanDie || target.CurrentHp > 0)
                    Append(ArenaTalentStateEffectExecutor.ExecuteStatusEffect(target, effect, now));
                break;
            }
            case ArenaTalentEffectKind.ApplyAbilityAction:
                if (effect.Action is null)
                    throw new NotSupportedException("Arena talent effect is missing its ability action.");
                ApplyGeneratedEffectAction(
                    effect.SourceActorId,
                    RuntimeForActor(effect.TargetActorId).Actor,
                    effect.SourceTalentId,
                    effect.Action,
                    now);
                break;
            default:
                throw new NotSupportedException($"Arena talent effect {effect.Kind} is unsupported.");
        }
    }

    private void ApplyGeneratedEffectAction(
        Guid sourceActorId,
        CombatActorState target,
        string sourceTalentId,
        AbilityActionDefinition action,
        DateTimeOffset now)
    {
        if (action.Type == AbilityActionType.ApplyEffect && action.Effect is null)
            throw new NotSupportedException("Arena talent proc is missing its effect definition.");
        if (action.Type is not (AbilityActionType.ApplyEffect or AbilityActionType.Damage
            or AbilityActionType.Healing or AbilityActionType.ResourceChange
            or AbilityActionType.Dispel))
            throw new NotSupportedException("Arena talent proc produced an unsupported action.");

        var generated = new AbilityDefinition(
            $"ARENA_PROC_{sourceTalentId}",
            AbilityType.Instant,
            sourceActorId == target.ActorId
                ? AbilityTargetType.Self : AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "NONE",
            Actions: [action]);
        if (action.Type != AbilityActionType.ApplyEffect)
        {
            // Generated damage/healing uses the production AbilityEngine, but
            // does not re-dispatch talent hooks by default: a proc is not a
            // second player cast or an extra chance to trigger itself.
            Append(AbilityEngine.ResolveTriggeredAction(
                RuntimeForActor(sourceActorId), generated, action,
                target.ActorId, now, _random).Select(combatEvent => combatEvent with
                {
                    DefinitionId = combatEvent.DefinitionId ?? generated.Id,
                    SourceActorId = combatEvent.SourceActorId ?? sourceActorId,
                    TargetActorId = combatEvent.TargetActorId ?? target.ActorId
                }));
            return;
        }

        Append(EffectEngine.Apply(target, sourceActorId, action.Effect!, now));
    }

    private void ApplyInterrupt(AbilityDefinition ability, CombatRuntimeState targetRuntime,
        Guid sourceActorId, DateTimeOffset now)
    {
        AbilityActionDefinition? interrupt = ability.Actions?.FirstOrDefault(x => x.Type == AbilityActionType.Interrupt);
        if (interrupt is null) return;
        AbilityExecutionResult result = AbilityEngine.Interrupt(targetRuntime, now,
            interrupt.InterruptLockout ?? TimeSpan.Zero);
        ProcessKernelEvents(result.Events.Select(x => x with { SourceActorId = sourceActorId,
            TargetActorId = targetRuntime.Actor.ActorId }), sourceActorId, now,
            targetId: targetRuntime.Actor.ActorId);
    }

    private void ResolveAutoAttack(
        ArenaFighter source,
        ArenaFighter target,
        AutoAttackProfile profile,
        ref DateTimeOffset nextAttack,
        DateTimeOffset due,
        bool wasDue,
        bool sourceWasAlive,
        bool targetWasAlive,
        bool wasStunned)
    {
        if (!wasDue || !source.CanAutoAttack)
            return;
        if (!sourceWasAlive || !targetWasAlive)
        {
            nextAttack = DateTimeOffset.MaxValue;
            return;
        }

        if (RuntimeFor(source.AccountId).ActiveCast is { } activeCast)
        {
            nextAttack = activeCast.ResolvesAtUtc + profile.Interval;
            return;
        }
        ScheduleNextAutoAttack(source, profile, ref nextAttack, due);
        if (!target.Actor.IsTargetable(due))
            return;
        if (wasStunned)
        {
            Append(new CombatEvent(
                CombatEventType.ActionRejected,
                due,
                source.Actor.ActorId,
                (AutoAttackControlError(source.Actor, due) ?? AbilityErrorCode.ActorStunned).ToString(),
                SourceActorId: source.Actor.ActorId,
                TargetActorId: target.Actor.ActorId));
            return;
        }
        if (MechanicsFor(source.Actor.ActorId) is { } mechanics)
        {
            mechanics.ResolveMechanicsAutoAttack(profile, due);
            ScheduleNextAutoAttack(source, profile, ref nextAttack, due);
            return;
        }
        decimal baseDamage = AutoAttackDamageRoller.RollPlayerDamage(profile,
            Math.Max(0, EffectEngine.CalculateStat(source.Actor, EffectStat.AttackPower,
                source.Actor.Stats.AttackPower, due)), _random);
        DamageResult damage = DamagePipeline.Resolve(new DamageRequest(source.Actor, target.Actor,
            baseDamage, profile.DamageType), _random, due);
        Append(damage.Events);
        bool successfulAutoAttack = damage.Avoidance == DamageAvoidance.None && damage.HpDamage > 0;
        if (successfulAutoAttack)
        {
            DispatchTalentEvents(
            [
                new ArenaTalentCombatEvent(
                    ArenaTalentEventType.OnAutoAttack,
                    source.Actor.ActorId,
                    target.Actor.ActorId,
                    due,
                    FinalDamage: damage.HpDamage,
                    DamageType: profile.DamageType,
                    WasCritical: damage.IsCritical,
                    WasBlocked: damage.WasBlocked)
            ], due);
        }
        DispatchDamageTalentEvents(damage.Events, due, AutoAttackTalentMarker);
        if (successfulAutoAttack)
            ScheduleNextAutoAttack(source, profile, ref nextAttack, due);
        if (damage.HpDamage > 0 && profile.ResourceOnHit > 0)
            source.Actor.AddResource(profile.ResourceOnHit);
    }

    private void ScheduleNextAutoAttack(
        ArenaFighter source,
        AutoAttackProfile profile,
        ref DateTimeOffset nextAttack,
        DateTimeOffset due)
    {
        if (MechanicsFor(source.Actor.ActorId) is { } mechanics)
        {
            nextAttack = due + mechanics.MechanicsAutoAttackInterval(profile, due);
            return;
        }
        decimal attackSpeed = Math.Max(0.01m, EffectEngine.CalculateStat(
            source.Actor, EffectStat.AttackSpeed, 1m, due));
        double adjustedTicks = profile.Interval.Ticks / (double)attackSpeed;
        nextAttack = due + TimeSpan.FromTicks(Math.Max(1, (long)Math.Ceiling(adjustedTicks)));
    }

    private static AbilityErrorCode? AutoAttackControlError(CombatActorState actor, DateTimeOffset now)
    {
        if (EffectEngine.HasControl(actor, EffectKind.Stun, now)) return AbilityErrorCode.ActorStunned;
        if (EffectEngine.HasControl(actor, EffectKind.Fear, now)) return AbilityErrorCode.ActorFeared;
        if (EffectEngine.HasControl(actor, EffectKind.Disarm, now)) return AbilityErrorCode.ActorDisarmed;
        return null;
    }

    private static TimeSpan InitialOffHandDelay(AutoAttackProfile profile) =>
        TimeSpan.FromTicks(Math.Max(1, profile.Interval.Ticks / 2));


    private void RefreshResetWindow(
        CombatActorState target,
        CrowdControlCategory category,
        DateTimeOffset now)
    {
        DateTimeOffset controlEndsAt = target.ActiveEffects
            .Where(effect => effect.ExpiresAtUtc > now && IsCategory(effect, category))
            .Select(effect => effect.ExpiresAtUtc).DefaultIfEmpty(now).Max();
        if (controlEndsAt > now)
            _crowdControlDr[target.ActorId].RefreshResetWindow(category, controlEndsAt);
    }

    private void ApplyControlConsequences(
        Guid sourceActorId,
        CombatActorState target,
        CrowdControlCategory category,
        DateTimeOffset now)
    {
        CombatRuntimeState targetRuntime = RuntimeForActor(target.ActorId);
        if (targetRuntime.ActiveCast is not { } cast
            || category is not (CrowdControlCategory.Stun or CrowdControlCategory.Fear
                or CrowdControlCategory.Silence)
            || category == CrowdControlCategory.Silence && !cast.Ability.IsSpell)
            return;
        AbilityExecutionResult interrupted = AbilityEngine.Interrupt(targetRuntime, now, TimeSpan.Zero);
        if (!interrupted.Succeeded)
            return;
        ProcessKernelEvents(interrupted.Events.Select(combatEvent => combatEvent with
        {
            SourceActorId = sourceActorId,
            TargetActorId = target.ActorId
        }), sourceActorId, now, targetId: target.ActorId);
    }

    private static bool IsCategory(ActiveEffect effect, CrowdControlCategory category) =>
        CrowdControlCategoryResolver.TryResolve(effect.Definition.Kind, out CrowdControlCategory resolved)
        && resolved == category;


    private IReadOnlyList<CombatEvent> ResolvePeriodicDamage(ActiveEffect effect,
        CombatActorState target, DateTimeOffset tickAt)
    {
        if (MechanicsFor(effect.SourceId) is { } mechanics)
            return mechanics.ResolveMechanicsPeriodicDamage(effect, target, tickAt);
        CombatActorState? source = effect.SourceId == _first.Actor.ActorId ? _first.Actor
            : effect.SourceId == _second.Actor.ActorId ? _second.Actor : null;
        if (source is null || source.IsDead || target.IsDead) return [];
        return DamagePipeline.Resolve(new DamageRequest(source, target,
            effect.Definition.Magnitude * effect.Stacks, effect.Definition.PeriodicDamageType,
            CanMiss: false, CanDodge: false, CanCrit: false, MinimumDamage: 0), _random, tickAt).Events;
    }

    private void ResolveExpiredEffect(ActiveEffect effect, CombatActorState target, DateTimeOffset now)
    {
        if (effect.SourceId != target.ActorId
            && CrowdControlCategoryResolver.TryResolve(effect.Definition.Kind, out CrowdControlCategory category))
        {
            DateTimeOffset controlEndsAt = target.ActiveEffects
                .Where(active => active.ExpiresAtUtc > now && IsCategory(active, category))
                .Select(active => active.ExpiresAtUtc).DefaultIfEmpty(now).Max();
            _crowdControlDr[target.ActorId].RefreshResetWindow(category, controlEndsAt);
        }
        if (MechanicsFor(effect.SourceId) is { } mechanics)
        {
            mechanics.ResolveMechanicsExpiredEffect(effect, target, now);
            return;
        }
        if (target.IsDead || RuntimeForActor(effect.SourceId).Actor.IsDead)
            return;
        foreach (EffectExpirationActionDefinition action in effect.Definition.OnExpireActions ?? [])
        {
            // A 1v1 actor has no other friendly participants.
            if (action.TargetScope == EffectExpirationTargetScope.EffectTargetAllies)
                continue;
            AbilityActionDefinition executable = action.Type switch
            {
                EffectExpirationActionType.Damage => new(AbilityActionType.Damage,
                    action.Amount, action.DamageType, CanMiss: false, CanCrit: false, CanDodge: false),
                EffectExpirationActionType.ApplyEffect => new(AbilityActionType.ApplyEffect, Effect: action.Effect),
                _ => throw new NotSupportedException($"Unknown expiration action {action.Type}.")
            };
            ApplyGeneratedEffectAction(effect.SourceId, target, effect.Definition.Id, executable, now);
        }
    }

    private DateTimeOffset? NextExecutionAt()
    {
        DateTimeOffset next = _firstAutoAttackAt < _secondAutoAttackAt ? _firstAutoAttackAt : _secondAutoAttackAt;
        if (_firstOffHandAttackAt < next) next = _firstOffHandAttackAt;
        if (_secondOffHandAttackAt < next) next = _secondOffHandAttackAt;
        DateTimeOffset timeout = TimeoutAtUtc;
        if (timeout < next) next = timeout;
        foreach (CombatRuntimeState runtime in new[] { _firstRuntime, _secondRuntime })
        {
            if (runtime.ActiveCast is { } cast && cast.NextResolutionAtUtc < next) next = cast.NextResolutionAtUtc;
            if (runtime.NextPendingActionAtUtc is { } pending && pending < next) next = pending;
        }
        foreach (CombatSession? mechanics in new[] { _firstMechanics, _secondMechanics })
            if (mechanics?.NextMechanicsDueAt is { } pending && pending < next)
                next = pending;
        foreach (CombatActorState actor in new[] { _first.Actor, _second.Actor })
        foreach (ActiveEffect effect in actor.ActiveEffects)
        {
            if (effect.NextTickAtUtc is { } tick && tick < next) next = tick;
            if (effect.ExpiresAtUtc < next) next = effect.ExpiresAtUtc;
        }
        return next;
    }

    private void ResolveOutcome(DateTimeOffset now)
    {
        ArenaMatchOutcome next = ArenaMatchRules.Resolve(Outcome, !_first.Actor.IsDead,
            !_second.Actor.IsDead, timedOut: now >= TimeoutAtUtc);
        if (next == Outcome) return;
        Outcome = next;
        Append(new CombatEvent(CombatEventType.CombatEnded, now, _first.Actor.ActorId, next.ToString()));
    }

    private ArenaCommandResult Result(bool succeeded, string? errorCode, long before) =>
        new(succeeded, errorCode, Snapshot, _events.Where(x => x.Sequence > before).ToArray());

    private static ArenaActorSnapshot ActorSnapshot(CombatActorState actor) =>
        new(actor.ActorId, actor.CurrentHp, actor.MaxHp, actor.CurrentResource, actor.MaxResource);

    private void Append(IEnumerable<CombatEvent> events)
    {
        foreach (CombatEvent combatEvent in events) Append(combatEvent);
    }
    private void Append(CombatEvent combatEvent) => _events.Add(combatEvent with
    {
        Sequence = ++_sequence,
        IsProc = combatEvent.IsProc || _executingTalentProc,
        ProcDepth = _executingTalentProc ? Math.Max(1, combatEvent.ProcDepth) : combatEvent.ProcDepth
    });

    private static void ValidateClassAbilityHandlers(ArenaFighter fighter, bool hasPlayerMechanics)
    {
        foreach (AbilityDefinition ability in fighter.Abilities.Values)
        {
            // Synthetic kernel-only abilities remain valid when their actions are self-contained.
            bool requiresClassHandler = ability.Actions is null or { Count: 0 }
                || ability.RuntimeParameters?.Count > 0;
            if (requiresClassHandler && (!hasPlayerMechanics
                || !PlayerCombatMechanicsCapabilities.HasClassAbilityHandler(
                    ability.Id, fighter.PlayerDefinition!.Participant.DefinitionId)))
                throw new NotSupportedException($"Ability {ability.Id} requires its production class runtime.");
        }
    }

    public static void ValidateAbilities(IReadOnlyDictionary<string, AbilityDefinition> abilities)
    {
        foreach (AbilityDefinition ability in abilities.Values)
        {
            bool classHandled = PlayerCombatMechanicsCapabilities.HasClassAbilityHandler(ability.Id);
            if (ability.Type is not (AbilityType.Instant or AbilityType.Casted or AbilityType.Channelled or AbilityType.Taunt
                    or AbilityType.NextAttackModifier)
                || !classHandled && ability.RuntimeParameters?.Count > 0
                || !classHandled && ability.Actions is null or { Count: 0 }
                || ability.TargetType is not (AbilityTargetType.Self or AbilityTargetType.SingleAlly
                or AbilityTargetType.SingleDeadAlly
                or AbilityTargetType.SingleEnemy or AbilityTargetType.AllEnemiesInCombat
                or AbilityTargetType.NEnemiesInCombat)
                && !IsSupportedSoloPartyAbility(ability)
                || !AbilityEngine.IsValidChannel(ability)
                || ability.Actions?.Any(action => action.Type == AbilityActionType.Interrupt
                    && (action.InterruptLockout is null || action.InterruptLockout < TimeSpan.Zero)) == true)
                throw new NotSupportedException($"Ability {ability.Id} is not supported in the arena runtime.");
        }
    }

    private static bool IsSupportedSoloPartyAbility(AbilityDefinition ability) =>
        ability.Type is AbilityType.Instant or AbilityType.Casted
        && ability.TargetType == AbilityTargetType.SelfAndPartyMembersInCombat
        && ability.Actions is { Count: > 0 }
        && ability.Actions.All(action => action.Type is AbilityActionType.ApplyEffect
            or AbilityActionType.Healing or AbilityActionType.ResourceChange or AbilityActionType.Dispel);

    private sealed record TimestampBatchEntry(
        ArenaFighter Fighter,
        ArenaFighter Opponent,
        CombatRuntimeState Runtime,
        ActiveCast? DueCast,
        PendingAbilityAction[] DuePendingActions,
        bool WasAlive,
        bool OpponentWasAlive,
        bool AutoAttackWasDue,
        bool OffHandWasDue,
        bool WasStunned,
        bool IsFirst);

}
