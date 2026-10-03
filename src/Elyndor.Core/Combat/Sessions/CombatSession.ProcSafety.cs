using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly ProcGuard _procGuard = new();
    private int _procExecutionDepth;
    private string? _procOriginId;

    private void RunProcHooks(CombatEvent input, string path, Action action)
    {
        if (_procExecutionDepth != 0 || !ProcGuard.IsEligible(input)
            || !_procGuard.TryObserve(_player.Actor.ActorId, path, input.ProcDispatchToken, input.Sequence))
            return;
        int previousDepth = _procExecutionDepth;
        string? previousOrigin = _procOriginId;
        _procExecutionDepth = input.ProcDepth + 1;
        _procOriginId = path;
        try
        {
            action();
        }
        finally
        {
            _procExecutionDepth = previousDepth;
            _procOriginId = previousOrigin;
        }
    }

    private CombatEvent CaptureProcOrigin(CombatEvent input) => _procExecutionDepth == 0
        ? input
        : input with
        {
            IsProc = true,
            ProcDepth = Math.Max(input.ProcDepth, _procExecutionDepth),
            ProcOriginId = input.ProcOriginId ?? _procOriginId
        };

    private void RunResolvedProcHooks(AbilityExecutionResult execution, string path, Action action)
    {
        if (execution.Events.Count == 0)
            return;
        CombatEvent input = execution.Events[^1];
        if (_procExecutionDepth != 0 || !ProcGuard.IsEligible(input)
            || !_procGuard.TryObserve(_player.Actor.ActorId, path, input.ProcDispatchToken, input.Sequence))
            return;
        // These callbacks also execute primary class abilities (e.g. traps).
        // Only their secondary attacks, not the whole ability, are proc-origin.
        action();
    }
}
