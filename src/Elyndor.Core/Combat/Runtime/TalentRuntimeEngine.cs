using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat;

public sealed class TalentRuntimeEngine(TalentRuntimeState state)
{
    private readonly TalentRuntimeState _state = state
        ?? throw new ArgumentNullException(nameof(state));

    public TalentRuntimeSnapshot Snapshot => _state.Snapshot;

    public IReadOnlyList<TalentRuntimeAction> Publish(
        CombatRuntimeEvent combatEvent,
        ResolvedTalentModifiers modifiers) =>
        _state.Publish(combatEvent, modifiers);

    public IReadOnlyList<TalentRuntimeAction> Publish(
        CombatRuntimeEvent combatEvent,
        ResolvedTalentModifiers modifiers,
        Func<ResolvedTalentEventHook, bool> hookFilter) =>
        _state.Publish(combatEvent, modifiers, hookFilter);

    public void Reset() => _state.Reset();
}
