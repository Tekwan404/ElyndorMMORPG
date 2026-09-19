namespace Elyndor.Core.Combat.SetPassives;

public sealed record SetPassiveProcState(
    Guid ActorId,
    string PassiveId,
    int EventCounter,
    DateTimeOffset? CooldownUntil,
    DateTimeOffset? LastProcAt = null);

public sealed class SetPassiveRuntimeState
{
    private readonly Dictionary<(Guid ActorId, string PassiveId), SetPassiveProcState> _states = new();

    public int Count => _states.Count;

    public SetPassiveProcState Get(Guid actorId, string passiveId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passiveId);

        return _states.TryGetValue((actorId, passiveId), out SetPassiveProcState? state)
            ? state
            : new SetPassiveProcState(actorId, passiveId, 0, null);
    }

    internal void Set(SetPassiveProcState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states[(state.ActorId, state.PassiveId)] = state;
    }
}
