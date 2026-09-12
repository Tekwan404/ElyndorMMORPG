namespace Elyndor.Core.Economy;

public sealed class PremiumStorePurchase
{
    private PremiumStorePurchase() { Sku = null!; ItemDefinitionId = null!; }

    public PremiumStorePurchase(Guid operationId, Guid accountId, Guid characterId, string sku, string itemDefinitionId, int quantity, long crystalPrice, DateTimeOffset purchasedAtUtc)
    {
        OperationId = operationId; AccountId = accountId; CharacterId = characterId; Sku = sku; ItemDefinitionId = itemDefinitionId;
        Quantity = quantity; CrystalPrice = crystalPrice; PurchasedAtUtc = purchasedAtUtc;
    }

    public Guid OperationId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid CharacterId { get; private set; }
    public string Sku { get; private set; }
    public string ItemDefinitionId { get; private set; }
    public int Quantity { get; private set; }
    public long CrystalPrice { get; private set; }
    public DateTimeOffset PurchasedAtUtc { get; private set; }
}
