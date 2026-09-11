namespace Elyndor.Core.Economy;

public sealed record PromoItemRewardDefinition(string ItemDefinitionId, int Quantity);

public sealed record PromoCodeDefinition(
    string Code,
    long CrystalAmount = 0,
    IReadOnlyList<PromoItemRewardDefinition>? ItemRewards = null,
    bool Enabled = true,
    DateTimeOffset? StartsAtUtc = null,
    DateTimeOffset? ExpiresAtUtc = null,
    int? GlobalRedemptionLimit = null,
    int? PerAccountRedemptionLimit = 1);

public sealed class PromoCodeRedemption
{
    private PromoCodeRedemption() { Code = null!; RequestFingerprint = null!; }

    public PromoCodeRedemption(Guid operationId, Guid accountId, Guid characterId, string code, string requestFingerprint, DateTimeOffset redeemedAtUtc)
    {
        OperationId = operationId;
        AccountId = accountId;
        CharacterId = characterId;
        Code = code;
        RequestFingerprint = requestFingerprint;
        RedeemedAtUtc = redeemedAtUtc;
    }

    public Guid OperationId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid CharacterId { get; private set; }
    public string Code { get; private set; }
    public string RequestFingerprint { get; private set; }
    public DateTimeOffset RedeemedAtUtc { get; private set; }
}
