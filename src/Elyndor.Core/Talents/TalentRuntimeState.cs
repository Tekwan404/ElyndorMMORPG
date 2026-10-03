using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Talents;

public enum TalentRuntimeActionKind
{
    ResourceChange
}

public sealed record TalentRuntimeAction(
    TalentRuntimeActionKind Kind,
    string TalentId,
    decimal Value,
    DateTimeOffset OccurredAtUtc,
    int ProcDepth,
    string? TargetId = null);

public sealed record TalentRuntimeSnapshot(
    IReadOnlyDictionary<string, int> Stacks,
    IReadOnlySet<string> Flags,
    IReadOnlyDictionary<string, DateTimeOffset> InternalCooldowns);

public sealed class TalentRuntimeState
{
    private readonly Guid _ownerActorId;
    private readonly IGameRandom _random;
    private readonly Dictionary<string, int> _stacks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _internalCooldowns = new(StringComparer.Ordinal);
    private long _lastSequence;
    private readonly ProcGuard _procGuard;

    public TalentRuntimeState(Guid ownerActorId, IGameRandom random, ProcGuard? procGuard = null)
    {
        if (ownerActorId == Guid.Empty)
            throw new ArgumentException("Talent runtime owner is required.", nameof(ownerActorId));

        ArgumentNullException.ThrowIfNull(random);
        _ownerActorId = ownerActorId;
        _random = random;
        _procGuard = procGuard ?? new();
    }

    public TalentRuntimeSnapshot Snapshot => new(
        new Dictionary<string, int>(_stacks, StringComparer.Ordinal),
        new HashSet<string>(_flags, StringComparer.Ordinal),
        new Dictionary<string, DateTimeOffset>(_internalCooldowns, StringComparer.Ordinal));

    public IReadOnlyList<TalentRuntimeAction> Publish(
        CombatRuntimeEvent combatEvent,
        ResolvedTalentModifiers modifiers)
        => Publish(combatEvent, modifiers, null);

    public IReadOnlyList<TalentRuntimeAction> Publish(
        CombatRuntimeEvent combatEvent,
        ResolvedTalentModifiers modifiers,
        Func<ResolvedTalentEventHook, bool>? hookFilter)
    {
        ArgumentNullException.ThrowIfNull(combatEvent);
        ArgumentNullException.ThrowIfNull(modifiers);
        if (!_procGuard.TryObserve(_ownerActorId, "generic-dispatch", combatEvent.ProcDispatchToken, 0))
            return [];
        if (combatEvent.Sequence > 0 && combatEvent.Sequence == _lastSequence)
            return [];
        ValidateSequence(combatEvent.Sequence);
        if (!ProcGuard.IsEligible(combatEvent))
            return [];

        string? key = EventKey(combatEvent);
        if (key is null)
            return [];

        List<TalentRuntimeAction> actions = [];
        foreach (ResolvedTalentEventHook hook in modifiers.EventHooks
                     .Where(item => string.Equals(item.Key, key, StringComparison.Ordinal))
                     .Where(item => !PaladinTalentRuntimeCatalog.OwnsTalentId(item.TalentId))
                     .Where(item => hookFilter is null || hookFilter(item))
                     .OrderBy(item => item.TalentId, StringComparer.Ordinal))
        {
            if (!MatchesOwner(key, combatEvent)
                || combatEvent.IsProc && !hook.CanTriggerFromProc
                || !_procGuard.TryObserve(_ownerActorId, hook.TalentId, combatEvent.ProcDispatchToken, combatEvent.Sequence)
                || !_procGuard.IsReady(_ownerActorId, hook.TalentId, combatEvent.OccurredAtUtc)
                || !Roll(hook.ChancePercent))
            {
                continue;
            }

            _stacks[hook.TalentId] = _stacks.GetValueOrDefault(hook.TalentId) + 1;
            _procGuard.StartCooldown(_ownerActorId, hook.TalentId, combatEvent.OccurredAtUtc, hook.InternalCooldown);
            if (hook.InternalCooldown > TimeSpan.Zero)
            {
                _internalCooldowns[hook.TalentId] =
                    combatEvent.OccurredAtUtc + hook.InternalCooldown;
            }

            actions.Add(new(
                TalentRuntimeActionKind.ResourceChange,
                hook.TalentId,
                hook.Value,
                combatEvent.OccurredAtUtc,
                combatEvent.ProcDepth + 1,
                hook.TargetId));
        }

        return actions;
    }

    public void Reset()
    {
        _stacks.Clear();
        _flags.Clear();
        _internalCooldowns.Clear();
        _lastSequence = 0;
        _procGuard.Reset();
    }

    private void ValidateSequence(long sequence)
    {
        if (sequence <= 0)
            return;
        if (_lastSequence > 0 && sequence <= _lastSequence)
            throw new InvalidOperationException("Combat runtime events must be published in sequence order.");

        _lastSequence = sequence;
    }

    private bool Roll(decimal chancePercent) =>
        chancePercent >= 100 || chancePercent > 0 && _random.NextUnit() < chancePercent / 100m;

    private bool MatchesOwner(string key, CombatRuntimeEvent combatEvent) => key switch
    {
        TalentModifierKeys.OnDamageTaken or TalentModifierKeys.OnDodge =>
            combatEvent.TargetActorId == _ownerActorId,
        TalentModifierKeys.OnPartyEvent => combatEvent.SourceActorId == _ownerActorId,
        _ => combatEvent.SourceActorId == _ownerActorId
    };

    private static string? EventKey(CombatRuntimeEvent combatEvent) => combatEvent.Kind switch
    {
        CombatRuntimeEventKind.AbilityCompleted => TalentModifierKeys.OnAbilityUsed,
        CombatRuntimeEventKind.AutoAttackStarted => TalentModifierKeys.OnAutoAttack,
        CombatRuntimeEventKind.DamageTaken => TalentModifierKeys.OnDamageTaken,
        CombatRuntimeEventKind.Dodge => TalentModifierKeys.OnDodge,
        CombatRuntimeEventKind.CriticalHit => TalentModifierKeys.OnCriticalHit,
        CombatRuntimeEventKind.EnemyKilled => TalentModifierKeys.OnEnemyKilled,
        CombatRuntimeEventKind.HpThresholdReached => TalentModifierKeys.OnHpThreshold,
        CombatRuntimeEventKind.PartyEvent => TalentModifierKeys.OnPartyEvent,
        _ => null
    };
}
