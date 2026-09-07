namespace Elyndor.Core.World;

public sealed class CharacterTravelState
{
    private CharacterTravelState()
    {
        FromLocationId = null!;
        TargetLocationId = null!;
    }

    public CharacterTravelState(
        Guid characterId,
        Guid requestId,
        string fromLocationId,
        string targetLocationId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endsAtUtc)
    {
        if (characterId == Guid.Empty || requestId == Guid.Empty)
            throw new ArgumentException("Travel identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(fromLocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLocationId);
        if (startedAtUtc.Offset != TimeSpan.Zero
            || endsAtUtc.Offset != TimeSpan.Zero
            || endsAtUtc <= startedAtUtc)
        {
            throw new ArgumentException("Travel timestamps must be UTC and have positive duration.");
        }

        CharacterId = characterId;
        RequestId = requestId;
        FromLocationId = fromLocationId;
        TargetLocationId = targetLocationId;
        StartedAtUtc = startedAtUtc;
        EndsAtUtc = endsAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public Guid RequestId { get; private set; }
    public string FromLocationId { get; private set; }
    public string TargetLocationId { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
}
