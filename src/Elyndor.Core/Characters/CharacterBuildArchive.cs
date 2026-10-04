namespace Elyndor.Core.Characters;

public sealed class CharacterBuildArchive
{
    private CharacterBuildArchive() { BuildHash = null!; PayloadJson = null!; }

    public CharacterBuildArchive(string buildHash, string payloadJson, DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        BuildHash = buildHash;
        PayloadJson = payloadJson;
        CapturedAtUtc = capturedAtUtc;
    }

    public string BuildHash { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; }
}

public sealed class TrainingBuildReference
{
    private TrainingBuildReference() { BuildHash = null!; }
    public TrainingBuildReference(Guid sessionId, Guid accountId, string buildHash)
    {
        SessionId = sessionId;
        AccountId = accountId;
        BuildHash = buildHash;
    }
    public Guid SessionId { get; private set; }
    public Guid AccountId { get; private set; }
    public string BuildHash { get; private set; }
}
