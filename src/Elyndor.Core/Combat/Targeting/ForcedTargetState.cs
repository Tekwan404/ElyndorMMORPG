namespace Elyndor.Core.Combat.Targeting;

public sealed class ForcedTargetState
{
    private Guid? _targetActorId;
    private DateTimeOffset? _expiresAtUtc;

    public void Set(Guid targetActorId, DateTimeOffset startedAtUtc, TimeSpan duration)
    {
        if (targetActorId == Guid.Empty)
            throw new ArgumentException("Forced target is required.", nameof(targetActorId));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        _targetActorId = targetActorId;
        _expiresAtUtc = startedAtUtc + duration;
    }

    public Guid? GetActive(DateTimeOffset now) =>
        _targetActorId is { } target
            && _expiresAtUtc is { } expiresAtUtc
            && expiresAtUtc > now
                ? target
                : null;

    public void Clear()
    {
        _targetActorId = null;
        _expiresAtUtc = null;
    }
}
