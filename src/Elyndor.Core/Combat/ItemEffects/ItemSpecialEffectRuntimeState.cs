namespace Elyndor.Core.Combat.ItemEffects;

public sealed record ItemSpecialEffectProcState(
    Guid ActorId,
    string EffectId,
    int EventCounter,
    DateTimeOffset? CooldownUntil,
    DateTimeOffset? LastProcAt = null);

public sealed class ItemSpecialEffectRuntimeState
{
    internal ProcGuard Guard { get; } = new();
    private readonly Dictionary<(Guid ActorId, string EffectId), ItemSpecialEffectProcState> _states = new();

    public int Count => _states.Count;

    public ItemSpecialEffectProcState Get(Guid actorId, string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);

        return _states.TryGetValue((actorId, effectId), out ItemSpecialEffectProcState? state)
            ? state
            : new ItemSpecialEffectProcState(actorId, effectId, 0, null);
    }

    internal void Set(ItemSpecialEffectProcState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states[(state.ActorId, state.EffectId)] = state;
    }
}
