using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

public sealed record ArenaFighter(Guid AccountId, Guid CharacterId, CombatActorState Actor,
    IReadOnlyDictionary<string, AbilityDefinition> Abilities, AutoAttackProfile AutoAttack,
    ResolvedTalentModifiers? TalentModifiers = null)
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
    private readonly ArenaFighter _first;
    private readonly ArenaFighter _second;
    private readonly CombatRuntimeState _firstRuntime;
    private readonly CombatRuntimeState _secondRuntime;
    private readonly IGameRandom _random;
    private readonly DateTimeOffset _startedAt;
    private DateTimeOffset _advancedTo;
    private DateTimeOffset _firstAutoAttackAt;
    private DateTimeOffset _secondAutoAttackAt;
    private long _sequence;
    private readonly List<CombatEvent> _events = [];
    private const int MaximumAdvanceSteps = 10_000;
    private const int MaximumTrackedCommands = 512;
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _duration;
    private readonly HashSet<(Guid AccountId, string CommandId)> _seenCommands = [];
    private readonly Dictionary<Guid, CrowdControlDiminishingReturns> _crowdControlDr;

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
        ValidateAbilities(first.Abilities);
        ValidateAbilities(second.Abilities);
        MatchId = matchId;
        _first = first;
        _second = second;
        _random = random;
        _startedAt = startedAt;
        _advancedTo = startedAt;
        _firstAutoAttackAt = startedAt + first.AutoAttack.Interval;
        _secondAutoAttackAt = startedAt + second.AutoAttack.Interval;
        _firstRuntime = new CombatRuntimeState(first.Actor);
        _firstRuntime.AddActor(second.Actor);
        _secondRuntime = new CombatRuntimeState(second.Actor);
        _secondRuntime.AddActor(first.Actor);
        _crowdControlDr = new Dictionary<Guid, CrowdControlDiminishingReturns>
        {
            [first.Actor.ActorId] = new CrowdControlDiminishingReturns(),
            [second.Actor.ActorId] = new CrowdControlDiminishingReturns()
        };
        Append(new CombatEvent(CombatEventType.CombatStarted, startedAt, first.Actor.ActorId));
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

    private CombatRuntimeState RuntimeFor(Guid accountId) => accountId == _first.AccountId
        ? _firstRuntime : accountId == _second.AccountId
            ? _secondRuntime : throw new ArgumentException("Account is not an arena participant.");

    private CombatRuntimeState RuntimeForActor(Guid actorId) => actorId == _first.Actor.ActorId
        ? _firstRuntime : actorId == _second.Actor.ActorId
            ? _secondRuntime : throw new ArgumentException("Actor is not an arena participant.");

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
        Guid expectedTarget = ability.TargetType switch
        {
            AbilityTargetType.Self or AbilityTargetType.SingleAlly => fighter.Actor.ActorId,
            AbilityTargetType.SingleEnemy or AbilityTargetType.AllEnemiesInCombat
                or AbilityTargetType.NEnemiesInCombat => opponent.Actor.ActorId,
            _ => Guid.Empty
        };
        if (expectedTarget == Guid.Empty || targetActorId != expectedTarget)
            return Result(false, "arena_invalid_target", before);
        IReadOnlyList<Guid>? targetIds = ability.TargetType is AbilityTargetType.AllEnemiesInCombat
            or AbilityTargetType.NEnemiesInCombat ? [opponent.Actor.ActorId] : null;

        PreparedCrowdControlAbility? prepared = null;
        AbilityDefinition executableAbility = ability;
        if (ability.Type != AbilityType.Casted)
        {
            prepared = PrepareCrowdControlAbility(
                ability,
                fighter.Actor.ActorId,
                runtime.Actors[targetActorId],
                now);
            executableAbility = prepared.Ability;
        }

        AbilityExecutionResult execution = AbilityEngine.Execute(runtime, executableAbility,
            new AbilityIntent(commandId, abilityId, targetActorId, targetIds), now, _random);
        if (!execution.Succeeded)
            return Result(false, execution.ErrorCode.ToString(), before);
        if (_seenCommands.Count >= MaximumTrackedCommands) _seenCommands.Clear();
        _seenCommands.Add((accountId, commandId));
        Append(execution.Events);
        if (prepared is not null)
        {
            CommitPreparedCrowdControl(prepared, now);
            Append(prepared.ImmuneEvents);
        }
        if (executableAbility.Type != AbilityType.Casted)
        {
            ApplyInterrupt(executableAbility, isFirst ? _secondRuntime : _firstRuntime,
                fighter.Actor.ActorId, now);
            ResolveTalentProcs(fighter, opponent.Actor, ability, execution, now);
        }
        ResolveOutcome(now);
        return Result(true, null, before);
    }

    public void AdvanceTo(DateTimeOffset now)
    {
        if (now.Offset != TimeSpan.Zero || now < _advancedTo)
            throw new ArgumentOutOfRangeException(nameof(now));
        int steps = 0;
        while (Outcome == ArenaMatchOutcome.Active && ++steps <= MaximumAdvanceSteps)
        {
            DateTimeOffset? next = NextExecutionAt();
            if (next is null || next > now) break;
            DateTimeOffset due = next.Value;
            CompleteCast(_firstRuntime, due);
            CompleteCast(_secondRuntime, due);
            ResolvePendingActions(_firstRuntime, due);
            ResolvePendingActions(_secondRuntime, due);
            Append(EffectEngine.Process(_first.Actor, due,
                (effect, tick) => ResolvePeriodicDamage(effect, _first.Actor, tick)));
            Append(EffectEngine.Process(_second.Actor, due,
                (effect, tick) => ResolvePeriodicDamage(effect, _second.Actor, tick)));
            ResolveAutoAttack(_first, _second, ref _firstAutoAttackAt, due);
            ResolveAutoAttack(_second, _first, ref _secondAutoAttackAt, due);
            ResolveOutcome(due);
        }
        if (steps > MaximumAdvanceSteps)
            throw new InvalidOperationException("Arena execution exceeded the safe step limit.");
        _advancedTo = now;
    }

    private void CompleteCast(CombatRuntimeState runtime, DateTimeOffset due)
    {
        if (runtime.Actor.IsDead || runtime.ActiveCast?.ResolvesAtUtc > due || runtime.ActiveCast is null) return;
        ActiveCast cast = runtime.ActiveCast;
        CombatActorState target = runtime.Actors[cast.TargetId];
        ArenaFighter fighter = runtime == _firstRuntime ? _first : _second;
        PreparedCrowdControlAbility prepared = PrepareCrowdControlAbility(
            cast.Ability,
            runtime.Actor.ActorId,
            target,
            due);
        runtime.ActiveCast = cast with { Ability = prepared.Ability };
        AbilityExecutionResult execution = AbilityEngine.CompleteCast(runtime, due, _random);
        Append(execution.Events);
        if (!execution.Succeeded)
            return;
        CommitPreparedCrowdControl(prepared, due);
        Append(prepared.ImmuneEvents);
        ApplyInterrupt(prepared.Ability, runtime == _firstRuntime ? _secondRuntime : _firstRuntime,
            runtime.Actor.ActorId, due);
        ResolveTalentProcs(fighter, target, cast.Ability, execution, due);
    }

    private void ResolvePendingActions(CombatRuntimeState runtime, DateTimeOffset due)
    {
        PendingAbilityAction[] dueActions = runtime.PendingActions
            .Where(action => action.ExecuteAtUtc <= due)
            .OrderBy(action => action.ExecuteAtUtc)
            .ThenBy(action => action.Sequence)
            .ToArray();
        if (dueActions.Length == 0)
            return;

        Dictionary<Guid, CrowdControlDiminishingReturns> stagedStates = [];
        Dictionary<Guid, HashSet<CrowdControlCategory>> appliedCategories = [];
        List<CombatEvent> immuneEvents = [];

        foreach (PendingAbilityAction pending in dueActions)
        {
            EffectDefinition? effect = pending.Action.Type == AbilityActionType.ApplyEffect
                ? pending.Action.Effect
                : null;
            if (effect is null
                || !CrowdControlCategoryResolver.TryResolve(effect.Kind, out CrowdControlCategory category))
            {
                continue;
            }

            CombatActorState target = runtime.Actors[pending.TargetId];
            if (!stagedStates.TryGetValue(target.ActorId, out CrowdControlDiminishingReturns? staged))
            {
                staged = _crowdControlDr[target.ActorId].Clone();
                stagedStates[target.ActorId] = staged;
                appliedCategories[target.ActorId] = [];
            }

            CrowdControlDrResolution resolution = staged.Resolve(category, effect.Duration, due);
            int index = runtime.PendingActions.IndexOf(pending);
            if (resolution.IsImmune)
            {
                if (index >= 0)
                    runtime.PendingActions.RemoveAt(index);
                immuneEvents.Add(ImmuneEvent(
                    due,
                    runtime.Actor.ActorId,
                    target.ActorId,
                    effect.Id));
                continue;
            }

            EffectDefinition adjustedEffect = effect with { Duration = resolution.EffectiveDuration };
            if (index >= 0)
            {
                runtime.PendingActions[index] = pending with
                {
                    Action = pending.Action with { Effect = adjustedEffect }
                };
            }
            staged.Commit(resolution, due + resolution.EffectiveDuration);
            appliedCategories[target.ActorId].Add(category);
        }

        IReadOnlyList<CombatEvent> events = AbilityEngine.ResolvePendingActions(runtime, due, _random);
        Append(events);
        foreach ((Guid targetActorId, CrowdControlDiminishingReturns staged) in stagedStates)
        {
            _crowdControlDr[targetActorId].ReplaceWith(staged);
            CombatActorState target = RuntimeForActor(targetActorId).Actor;
            RefreshResetWindows(target, appliedCategories[targetActorId], due);
            ApplyControlConsequences(
                runtime.Actor.ActorId,
                target,
                appliedCategories[targetActorId],
                due);
        }
        Append(immuneEvents);
    }

    private void ResolveTalentProcs(
        ArenaFighter fighter,
        CombatActorState target,
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        bool hitTarget = execution.Events.Any(combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.TargetActorId == target.ActorId
            && combatEvent.Amount > 0);
        if (!hitTarget)
            return;

        AbilityActionDefinition? proc = PyromancerImpactRuntime.TryResolveStunAction(
            fighter.EffectiveTalentModifiers,
            ability,
            _random);
        if (proc is not null)
            ApplyGeneratedEffectAction(fighter.Actor.ActorId, target, proc, now);
    }

    private void ApplyGeneratedEffectAction(
        Guid sourceActorId,
        CombatActorState target,
        AbilityActionDefinition action,
        DateTimeOffset now)
    {
        if (action.Type != AbilityActionType.ApplyEffect || action.Effect is null)
            throw new NotSupportedException("Arena talent proc produced an unsupported action.");

        var generated = new AbilityDefinition(
            $"ARENA_PROC_{action.Effect.Id}",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "NONE",
            Actions: [action]);
        PreparedCrowdControlAbility prepared = PrepareCrowdControlAbility(
            generated,
            sourceActorId,
            target,
            now);

        foreach (AbilityActionDefinition preparedAction in prepared.Ability.Actions ?? [])
        {
            if (preparedAction.Type != AbilityActionType.ApplyEffect || preparedAction.Effect is null)
                throw new NotSupportedException("Arena talent proc produced an unsupported action.");
            Append(EffectEngine.Apply(target, sourceActorId, preparedAction.Effect, now));
        }

        CommitPreparedCrowdControl(prepared, now);
        Append(prepared.ImmuneEvents);
    }

    private void ApplyInterrupt(AbilityDefinition ability, CombatRuntimeState targetRuntime,
        Guid sourceActorId, DateTimeOffset now)
    {
        AbilityActionDefinition? interrupt = ability.Actions?.FirstOrDefault(x => x.Type == AbilityActionType.Interrupt);
        if (interrupt is null) return;
        AbilityExecutionResult result = AbilityEngine.Interrupt(targetRuntime, now,
            interrupt.InterruptLockout ?? TimeSpan.Zero);
        Append(result.Events.Select(x => x with { SourceActorId = sourceActorId,
            TargetActorId = targetRuntime.Actor.ActorId }));
    }

    private void ResolveAutoAttack(ArenaFighter source, ArenaFighter target,
        ref DateTimeOffset nextAttack, DateTimeOffset due)
    {
        if (nextAttack > due || source.Actor.IsDead || target.Actor.IsDead) return;
        nextAttack = due + source.AutoAttack.Interval;
        if (EffectEngine.HasControl(source.Actor, EffectKind.Stun, due))
        {
            Append(new CombatEvent(
                CombatEventType.ActionRejected,
                due,
                source.Actor.ActorId,
                AbilityErrorCode.ActorStunned.ToString(),
                SourceActorId: source.Actor.ActorId,
                TargetActorId: target.Actor.ActorId));
            return;
        }
        decimal baseDamage = AutoAttackDamageRoller.RollPlayerDamage(source.AutoAttack,
            Math.Max(0, EffectEngine.CalculateStat(source.Actor, EffectStat.AttackPower,
                source.Actor.Stats.AttackPower, due)), _random);
        DamageResult damage = DamagePipeline.Resolve(new DamageRequest(source.Actor, target.Actor,
            baseDamage, source.AutoAttack.DamageType), _random, due);
        Append(damage.Events);
        if (damage.HpDamage > 0 && source.AutoAttack.ResourceOnHit > 0)
            source.Actor.AddResource(source.AutoAttack.ResourceOnHit);
    }

    private PreparedCrowdControlAbility PrepareCrowdControlAbility(
        AbilityDefinition ability,
        Guid sourceActorId,
        CombatActorState target,
        DateTimeOffset now)
    {
        CrowdControlDiminishingReturns staged = _crowdControlDr[target.ActorId].Clone();
        if (ability.Actions is null or { Count: 0 })
        {
            return new PreparedCrowdControlAbility(
                ability,
                staged,
                sourceActorId,
                target,
                [],
                new HashSet<CrowdControlCategory>());
        }

        List<AbilityActionDefinition> actions = [];
        List<CombatEvent> immuneEvents = [];
        HashSet<CrowdControlCategory> appliedCategories = [];

        foreach (AbilityActionDefinition action in ability.Actions)
        {
            bool immediate = action.Delay is null || action.Delay <= TimeSpan.Zero;
            EffectDefinition? effect = action.Type == AbilityActionType.ApplyEffect && immediate
                ? action.Effect
                : null;
            if (effect is null
                || !CrowdControlCategoryResolver.TryResolve(effect.Kind, out CrowdControlCategory category))
            {
                actions.Add(action);
                continue;
            }

            CrowdControlDrResolution resolution = staged.Resolve(category, effect.Duration, now);
            if (resolution.IsImmune)
            {
                immuneEvents.Add(ImmuneEvent(now, sourceActorId, target.ActorId, effect.Id));
                continue;
            }

            actions.Add(action with
            {
                Effect = effect with { Duration = resolution.EffectiveDuration }
            });
            staged.Commit(resolution, now + resolution.EffectiveDuration);
            appliedCategories.Add(category);
        }

        return new PreparedCrowdControlAbility(
            ability with { Actions = actions },
            staged,
            sourceActorId,
            target,
            immuneEvents,
            appliedCategories);
    }

    private void CommitPreparedCrowdControl(
        PreparedCrowdControlAbility prepared,
        DateTimeOffset now)
    {
        _crowdControlDr[prepared.Target.ActorId].ReplaceWith(prepared.StagedState);
        RefreshResetWindows(prepared.Target, prepared.AppliedCategories, now);
        ApplyControlConsequences(
            prepared.SourceActorId,
            prepared.Target,
            prepared.AppliedCategories,
            now);
    }

    private void RefreshResetWindows(
        CombatActorState target,
        IReadOnlySet<CrowdControlCategory> categories,
        DateTimeOffset now)
    {
        foreach (CrowdControlCategory category in categories)
        {
            DateTimeOffset controlEndsAt = target.ActiveEffects
                .Where(effect => effect.ExpiresAtUtc > now && IsCategory(effect, category))
                .Select(effect => effect.ExpiresAtUtc)
                .DefaultIfEmpty(now)
                .Max();
            if (controlEndsAt > now)
                _crowdControlDr[target.ActorId].RefreshResetWindow(category, controlEndsAt);
        }
    }

    private void ApplyControlConsequences(
        Guid sourceActorId,
        CombatActorState target,
        IReadOnlySet<CrowdControlCategory> categories,
        DateTimeOffset now)
    {
        if (!categories.Contains(CrowdControlCategory.Stun)
            || !EffectEngine.HasControl(target, EffectKind.Stun, now))
        {
            return;
        }

        CombatRuntimeState targetRuntime = RuntimeForActor(target.ActorId);
        AbilityExecutionResult interrupted = AbilityEngine.Interrupt(targetRuntime, now, TimeSpan.Zero);
        if (!interrupted.Succeeded)
            return;
        Append(interrupted.Events.Select(combatEvent => combatEvent with
        {
            SourceActorId = sourceActorId,
            TargetActorId = target.ActorId
        }));
    }

    private static bool IsCategory(ActiveEffect effect, CrowdControlCategory category) =>
        CrowdControlCategoryResolver.TryResolve(effect.Definition.Kind, out CrowdControlCategory resolved)
        && resolved == category;

    private static CombatEvent ImmuneEvent(
        DateTimeOffset now,
        Guid sourceActorId,
        Guid targetActorId,
        string effectId) =>
        new(
            CombatEventType.EffectImmune,
            now,
            targetActorId,
            effectId,
            SourceActorId: sourceActorId,
            TargetActorId: targetActorId);

    private IReadOnlyList<CombatEvent> ResolvePeriodicDamage(ActiveEffect effect,
        CombatActorState target, DateTimeOffset tickAt)
    {
        CombatActorState? source = effect.SourceId == _first.Actor.ActorId ? _first.Actor
            : effect.SourceId == _second.Actor.ActorId ? _second.Actor : null;
        if (source is null || source.IsDead || target.IsDead) return [];
        return DamagePipeline.Resolve(new DamageRequest(source, target,
            effect.Definition.Magnitude * effect.Stacks, effect.Definition.PeriodicDamageType,
            CanMiss: false, CanDodge: false, CanCrit: false, MinimumDamage: 0), _random, tickAt).Events;
    }

    private DateTimeOffset? NextExecutionAt()
    {
        DateTimeOffset next = _firstAutoAttackAt < _secondAutoAttackAt ? _firstAutoAttackAt : _secondAutoAttackAt;
        DateTimeOffset timeout = TimeoutAtUtc;
        if (timeout < next) next = timeout;
        foreach (CombatRuntimeState runtime in new[] { _firstRuntime, _secondRuntime })
        {
            if (runtime.ActiveCast is { } cast && cast.ResolvesAtUtc < next) next = cast.ResolvesAtUtc;
            if (runtime.NextPendingActionAtUtc is { } pending && pending < next) next = pending;
        }
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
    private void Append(CombatEvent combatEvent) => _events.Add(combatEvent with { Sequence = ++_sequence });

    public static void ValidateAbilities(IReadOnlyDictionary<string, AbilityDefinition> abilities)
    {
        foreach (AbilityDefinition ability in abilities.Values)
        {
            if (ability.Type is not (AbilityType.Instant or AbilityType.Casted)
                || ability.RuntimeParameters?.Count > 0
                || ability.Actions is null or { Count: 0 }
                || ability.TargetType is not (AbilityTargetType.Self or AbilityTargetType.SingleAlly
                or AbilityTargetType.SingleEnemy or AbilityTargetType.AllEnemiesInCombat
                or AbilityTargetType.NEnemiesInCombat)
                || ability.TargetType == AbilityTargetType.SingleAlly && !ability.AllowSelfTarget
                || ability.Actions?.Any(action => action.Type is AbilityActionType.Taunt
                    or AbilityActionType.AddThreat or AbilityActionType.DropThreatPercent
                    or AbilityActionType.ClearThreat or AbilityActionType.Fixate
                    || action.Effect?.OnExpireActions is { Count: > 0 }
                    || action.Type == AbilityActionType.Interrupt
                    && (action.InterruptLockout is null || action.InterruptLockout < TimeSpan.Zero)) == true)
                throw new NotSupportedException($"Ability {ability.Id} is not supported in the arena runtime.");
        }
    }

    private sealed record PreparedCrowdControlAbility(
        AbilityDefinition Ability,
        CrowdControlDiminishingReturns StagedState,
        Guid SourceActorId,
        CombatActorState Target,
        IReadOnlyList<CombatEvent> ImmuneEvents,
        IReadOnlySet<CrowdControlCategory> AppliedCategories);
}
