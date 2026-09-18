namespace Elyndor.Core.Combat.SetPassives;

public sealed class SetPassiveRuntime
{
    private readonly SetPassiveEvaluator _evaluator;
    private readonly SetPassiveRuntimeState _state = new();

    public SetPassiveRuntime(IEnumerable<SetPassiveDefinition> definitions)
    {
        _evaluator = new SetPassiveEvaluator(definitions);
    }

    public SetPassiveRuntimeState State => _state;

    public IReadOnlyList<SetPassiveActionInvocation> Evaluate(
        CombatEvent combatEvent,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> equippedSetPieceCounts) =>
        _evaluator.Evaluate(combatEvent, equippedSetPieceCounts, _state);
}
