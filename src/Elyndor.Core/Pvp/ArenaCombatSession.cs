using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Core.Pvp;

public sealed record ArenaFighter(Guid AccountId, Guid CharacterId, CombatActorState Actor,
    IReadOnlyDictionary<string, AbilityDefinition> Abilities, AutoAttackProfile AutoAttack);

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

    public ArenaCombatSession(Guid matchId, ArenaFighter first, ArenaFighter second,
        IGameRandom random, DateTimeOffset startedAt)
    {
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
        Append(new CombatEvent(CombatEventType.CombatStarted, startedAt, first.Actor.ActorId));
    }

    public Guid MatchId { get; }
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

    public bool Forfeit(Guid accountId, DateTimeOffset now)
    {
        if (accountId != _first.AccountId && accountId != _second.AccountId)
            return false;
        if (now.Offset != TimeSpan.Zero || now < _advancedTo || Outcome != ArenaMatchOutcome.Active)
            return false;
        Outcome = accountId == _first.AccountId ? ArenaMatchOutcome.WinnerB : ArenaMatchOutcome.WinnerA;
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
        AdvanceTo(now);
        long before = _sequence;
        if (Outcome != ArenaMatchOutcome.Active) return Result(false, "arena_ended", before);
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
        AbilityExecutionResult execution = AbilityEngine.Execute(runtime, ability,
            new AbilityIntent(commandId, abilityId, targetActorId, targetIds), now, _random);
        if (!execution.Succeeded)
            return Result(false, execution.ErrorCode.ToString(), before);
        Append(execution.Events);
        if (ability.Type != AbilityType.Casted)
            ApplyInterrupt(ability, isFirst ? _secondRuntime : _firstRuntime,
                fighter.Actor.ActorId, now);
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
            Append(AbilityEngine.ResolvePendingActions(_firstRuntime, due, _random));
            Append(AbilityEngine.ResolvePendingActions(_secondRuntime, due, _random));
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
        AbilityDefinition ability = runtime.ActiveCast.Ability;
        Append(AbilityEngine.CompleteCast(runtime, due, _random).Events);
        ApplyInterrupt(ability, runtime == _firstRuntime ? _secondRuntime : _firstRuntime,
            runtime.Actor.ActorId, due);
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
        decimal baseDamage = AutoAttackDamageRoller.RollPlayerDamage(source.AutoAttack,
            Math.Max(0, EffectEngine.CalculateStat(source.Actor, EffectStat.AttackPower,
                source.Actor.Stats.AttackPower, due)), _random);
        DamageResult damage = DamagePipeline.Resolve(new DamageRequest(source.Actor, target.Actor,
            baseDamage, source.AutoAttack.DamageType), _random, due);
        Append(damage.Events);
        if (damage.HpDamage > 0 && source.AutoAttack.ResourceOnHit > 0)
            source.Actor.AddResource(source.AutoAttack.ResourceOnHit);
        nextAttack = due + source.AutoAttack.Interval;
    }

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
        DateTimeOffset timeout = _startedAt + TimeSpan.FromMinutes(5);
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
            !_second.Actor.IsDead, timedOut: now - _startedAt >= TimeSpan.FromMinutes(5));
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
}
