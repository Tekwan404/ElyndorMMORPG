namespace Elyndor.Core.Characters;

public sealed record CharacterSkinDefinition(
    string Id,
    string Name,
    string ClassId,
    string GenderId,
    string ImageId,
    long CrystalPrice,
    bool Enabled = true,
    bool Purchasable = true);

public sealed class CharacterSkinOwnership
{
    private CharacterSkinOwnership() { SkinId = null!; }

    public CharacterSkinOwnership(Guid characterId, string skinId, Guid operationId, long crystalPrice, DateTimeOffset purchasedAtUtc)
    {
        CharacterId = characterId;
        SkinId = skinId;
        OperationId = operationId;
        CrystalPrice = crystalPrice;
        PurchasedAtUtc = purchasedAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public string SkinId { get; private set; }
    public Guid OperationId { get; private set; }
    public long CrystalPrice { get; private set; }
    public DateTimeOffset PurchasedAtUtc { get; private set; }
}
