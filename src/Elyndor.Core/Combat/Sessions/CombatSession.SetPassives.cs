using Elyndor.Core.Combat.SetPassives;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly SetPassiveRuntime _setPassiveRuntime = new(SetPassiveCatalog.Definitions);
    private IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> _setPassiveLoadoutSnapshot =
        new Dictionary<Guid, IReadOnlyDictionary<string, int>>();

    /// <summary>
    /// Equipped set pieces are captured once, when the session is created: equipment cannot
    /// change while a combat session is active, and the roster is fixed by the constructor.
    /// </summary>
    private void InitializeSetPassiveLoadoutSnapshot()
    {
        _setPassiveLoadoutSnapshot = _playerStatesByActorId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, int>)(pair.Value.Definition.EquippedSetPieces is { } setPieces
                ? setPieces.ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private void ApplySetPassiveHooks(CombatEvent combatEvent)
    {
        if (!_setPassiveRuntime.HandlesEventType(combatEvent.Type))
            return;

        IReadOnlyList<SetPassiveActionInvocation> invocations = _setPassiveRuntime.Evaluate(
            combatEvent,
            _setPassiveLoadoutSnapshot);

        foreach (SetPassiveActionInvocation invocation in invocations)
        {
            if (!_playerStatesByActorId.TryGetValue(
                    invocation.ActorId,
                    out CombatPlayerRuntimeState? ownerState))
            {
                continue;
            }

            IReadOnlyList<CombatEvent> actionEvents = SetPassiveActionExecutor.Execute(
                invocation,
                ownerState.Definition.Actor);
            if (actionEvents.Count == 0)
            {
                continue;
            }

            ApplyKernelEvents(
                actionEvents,
                invocation.ActorId,
                invocation.ActorId,
                invocation.Action.ReferenceId);
        }
    }
}
