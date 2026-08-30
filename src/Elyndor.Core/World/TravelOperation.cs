namespace Elyndor.Core.World;

public sealed class TravelOperation
{
    private TravelOperation()
    {
        TargetLocationId = null!;
        ResultLocationId = null!;
    }

    public TravelOperation(
        Guid characterId,
        Guid requestId,
        string targetLocationId,
        string resultLocationId,
        long resultVersion,
        DateTimeOffset completedAtUtc)
    {
        CharacterId = characterId;
        RequestId = requestId;
        TargetLocationId = targetLocationId;
        ResultLocationId = resultLocationId;
        ResultVersion = resultVersion;
        CompletedAtUtc = completedAtUtc;
    }

    public Guid CharacterId { get; private set; }

    public Guid RequestId { get; private set; }

    public string TargetLocationId { get; private set; }

    public string ResultLocationId { get; private set; }

    public long ResultVersion { get; private set; }

    public DateTimeOffset CompletedAtUtc { get; private set; }
}
