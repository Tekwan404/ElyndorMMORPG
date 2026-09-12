namespace Elyndor.Contracts.Economy;

public sealed record PromoCodeRedemptionRequest(string Code, Guid MutationId);
public sealed record PromoCodeRedemptionResponse(long CrystalBalance);
