namespace Elyndor.Contracts.Economy;

public sealed record PremiumStoreOfferResponse(string Sku, string ItemDefinitionId, string Name, string Description, string Rarity, string? IconId, int Quantity, long CrystalPrice, bool CanPurchase, int MaxPackCount = 10000, int InventoryCapacityBonus = 0);
public sealed record PremiumStoreResponse(long CrystalBalance, IReadOnlyList<PremiumStoreOfferResponse> Offers);
public sealed record PremiumStorePurchaseRequest(string Sku, Guid MutationId, int PackCount = 1);
public sealed record PremiumStorePurchaseResponse(long CrystalBalance);
