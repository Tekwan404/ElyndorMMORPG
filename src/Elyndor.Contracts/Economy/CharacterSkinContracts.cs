namespace Elyndor.Contracts.Economy;

public sealed record CharacterSkinOfferResponse(string Id, string Name, string ClassId, string GenderId,
    string ImageId, long CrystalPrice, bool Owned, bool Eligible, bool Purchasable);
public sealed record CharacterSkinStoreResponse(long CrystalBalance, string? ActiveSkinId,
    IReadOnlyList<CharacterSkinOfferResponse> Skins);
public sealed record CharacterSkinPurchaseRequest(string SkinId, Guid MutationId);
public sealed record CharacterSkinEquipRequest(string? SkinId);
public sealed record CharacterSkinMutationResponse(long CrystalBalance, string? ActiveSkinId);
