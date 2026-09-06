namespace Elyndor.Core.World;

public sealed record WorldContractDefinition(
    string Id,
    string DisplayName,
    string Description,
    int RequiredLevel,
    string TargetMonsterId,
    string UnlockLocationId,
    string? OfferLocationId = null,
    int RewardXp = 0,
    int RewardGold = 0);

public sealed class CharacterContractAcceptance
{
    private CharacterContractAcceptance()
    {
        ContractId = null!;
    }

    public CharacterContractAcceptance(
        Guid characterId,
        string contractId,
        DateTimeOffset acceptedAtUtc)
    {
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (acceptedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Contract timestamps must be UTC.", nameof(acceptedAtUtc));

        CharacterId = characterId;
        ContractId = contractId;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public string ContractId { get; private set; }
    public DateTimeOffset AcceptedAtUtc { get; private set; }
}

public sealed class CharacterContractCompletion
{
    private CharacterContractCompletion()
    {
        ContractId = null!;
        TargetMonsterId = null!;
    }

    public CharacterContractCompletion(
        Guid characterId,
        string contractId,
        string targetMonsterId,
        Guid combatSessionId,
        DateTimeOffset completedAtUtc)
    {
        if (characterId == Guid.Empty || combatSessionId == Guid.Empty)
            throw new ArgumentException("Contract completion identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetMonsterId);
        if (completedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Contract timestamps must be UTC.", nameof(completedAtUtc));

        CharacterId = characterId;
        ContractId = contractId;
        TargetMonsterId = targetMonsterId;
        CombatSessionId = combatSessionId;
        CompletedAtUtc = completedAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public string ContractId { get; private set; }
    public string TargetMonsterId { get; private set; }
    public Guid CombatSessionId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
}
