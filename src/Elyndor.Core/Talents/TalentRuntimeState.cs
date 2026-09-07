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

    public TalentRuntimeState(Guid ownerActorId, IGameRandom random)
    {
        if (ownerActorId == Guid.Empty)
            throw new ArgumentException("Talent runtime owner is required.", nameof(ownerActorId));

        ArgumentNullException.ThrowIfNull(random);
        _ownerActorId = ownerActorId;
        _random = random;
    }

    public TalentRuntimeSnapshot Snapshot => new(
        new Dictionary<string, int>(_stacks, StringComparer.Ordinal),
        new HashSet<string>(_flags, StringComparer.Ordinal),
        new Dictionary<string, DateTimeOffset>(_internalCooldowns, StringComparer.Ordinal));

    public IReadOnlyList<TalentRuntimeAction> Publish(
        CombatRuntimeEvent combatEvent,
        ResolvedTalentModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(combatEvent);
        ArgumentNullException.ThrowIfNull(modifiers);
        ValidateSequence(combatEvent.Sequence);

        string? key = EventKey(combatEvent);
        if (key is null)
            return [];

        List<TalentRuntimeAction> actions = [];
        foreach (ResolvedTalentEventHook hook in modifiers.EventHooks
                     .Where(item => string.Equals(item.Key, key, StringComparison.Ordinal))
                     .OrderBy(item => item.TalentId, StringComparer.Ordinal))
        {
            if (!MatchesOwner(key, combatEvent)
                || combatEvent.IsProc && !hook.CanTriggerFromProc
                || IsOnCooldown(hook.TalentId, combatEvent.OccurredAtUtc)
                || !Roll(hook.ChancePercent))
            {
                continue;
            }

            _stacks[hook.TalentId] = _stacks.GetValueOrDefault(hook.TalentId) + 1;
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
    }

    private void ValidateSequence(long sequence)
    {
        if (sequence <= 0)
            return;
        if (_lastSequence > 0 && sequence <= _lastSequence)
            throw new InvalidOperationException("Combat runtime events must be published in sequence order.");

        _lastSequence = sequence;
    }

    private bool IsOnCooldown(string talentId, DateTimeOffset occurredAtUtc) =>
        _internalCooldowns.TryGetValue(talentId, out DateTimeOffset readyAtUtc)
        && readyAtUtc > occurredAtUtc;

    private bool Roll(decimal chancePercent) =>
        chancePercent >= 100 || chancePercent > 0 && _random.NextUnit() < chancePercent / 100m;

    private bool MatchesOwner(string key, CombatRuntimeEvent combatEvent) => key switch
    {
        TalentModifierKeys.OnDamageTaken => combatEvent.TargetActorId == _ownerActorId,
        TalentModifierKeys.OnPartyEvent => combatEvent.SourceActorId == _ownerActorId,
        _ => combatEvent.SourceActorId == _ownerActorId
    };

    private static string? EventKey(CombatRuntimeEvent combatEvent) => combatEvent.Kind switch
    {
        CombatRuntimeEventKind.AbilityCompleted => TalentModifierKeys.OnAbilityUsed,
        CombatRuntimeEventKind.AutoAttackStarted => TalentModifierKeys.OnAutoAttack,
        CombatRuntimeEventKind.DamageTaken => TalentModifierKeys.OnDamageTaken,
        CombatRuntimeEventKind.CriticalHit => TalentModifierKeys.OnCriticalHit,
        CombatRuntimeEventKind.EnemyKilled => TalentModifierKeys.OnEnemyKilled,
        CombatRuntimeEventKind.HpThresholdReached => TalentModifierKeys.OnHpThreshold,
        CombatRuntimeEventKind.PartyEvent => TalentModifierKeys.OnPartyEvent,
        _ => null
    };
}
