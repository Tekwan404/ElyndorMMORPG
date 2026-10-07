namespace Elyndor.Core.Combat.ItemEffects;

public sealed class ItemSpecialEffectRuntime
{
    private readonly ItemSpecialEffectEvaluator _evaluator;
    private readonly ItemSpecialEffectRuntimeState _state = new();

    public ItemSpecialEffectRuntime(IEnumerable<ItemSpecialEffectDefinition> definitions)
    {
        _evaluator = new ItemSpecialEffectEvaluator(definitions);
    }

    public ItemSpecialEffectRuntimeState State => _state;

    public bool HandlesEventType(CombatEventType eventType) =>
        _evaluator.TriggerEventTypes.Contains(eventType);

    public IReadOnlyList<ItemSpecialEffectInvocation> Evaluate(
        CombatEvent combatEvent,
        IReadOnlyDictionary<Guid, IReadOnlySet<string>> equippedSpecialEffectIds,
        ProcGuard? guard = null) =>
        _evaluator.Evaluate(
            combatEvent,
            equippedSpecialEffectIds,
            _state,
            guard);
}
