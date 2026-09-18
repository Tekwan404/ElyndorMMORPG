using Elyndor.Core.Combat.SetPassives;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly SetPassiveRuntime _setPassiveRuntime = new(SetPassiveCatalog.Definitions);
    private IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>>? _setPassiveLoadoutSnapshot;

    private void ApplySetPassiveHooks(CombatEvent combatEvent)
    {
        IReadOnlyList<SetPassiveActionInvocation> invocations = _setPassiveRuntime.Evaluate(
            combatEvent,
            GetSetPassiveLoadoutSnapshot());

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

    private IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> GetSetPassiveLoadoutSnapshot()
    {
        if (_setPassiveLoadoutSnapshot is not null)
        {
            return _setPassiveLoadoutSnapshot;
        }

        _setPassiveLoadoutSnapshot = _playerStatesByActorId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, int>)(pair.Value.Definition.EquippedSetPieces is { } setPieces
                ? setPieces.ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal)));
        return _setPassiveLoadoutSnapshot;
    }
}
