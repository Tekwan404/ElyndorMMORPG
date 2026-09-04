namespace Elyndor.Core.Characters;

public sealed class CharacterMutation
{
    private CharacterMutation()
    {
        OperationType = null!;
        RequestFingerprint = null!;
    }

    public CharacterMutation(
        Guid characterId,
        Guid mutationId,
        string operationType,
        string requestFingerprint,
        DateTimeOffset committedAtUtc)
    {
        if (characterId == Guid.Empty || mutationId == Guid.Empty)
            throw new ArgumentException("Mutation identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        if (requestFingerprint.Length != 64)
            throw new ArgumentException("Mutation fingerprint must be a SHA-256 hex digest.", nameof(requestFingerprint));
        if (committedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Mutation timestamps must be UTC.", nameof(committedAtUtc));

        CharacterId = characterId;
        MutationId = mutationId;
        OperationType = operationType;
        RequestFingerprint = requestFingerprint;
        CommittedAtUtc = committedAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public Guid MutationId { get; private set; }
    public string OperationType { get; private set; }
    public string RequestFingerprint { get; private set; }
    public DateTimeOffset CommittedAtUtc { get; private set; }
}
