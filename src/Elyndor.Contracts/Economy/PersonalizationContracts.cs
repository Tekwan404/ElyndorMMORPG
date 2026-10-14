namespace Elyndor.Contracts.Economy;

public sealed record ProfileCosmeticResponse(string Id, string Name, string Description, string Kind,
    string StyleId, long CrystalPrice, bool Owned, bool Enabled);
public sealed record PersonalizationStoreResponse(long CrystalBalance, string CharacterName, string? ActiveFrameId,
    string? ActiveTitleId, long? RenamePrice, IReadOnlyList<ProfileCosmeticResponse> Cosmetics);
public sealed record CosmeticPurchaseRequest(string CosmeticId, Guid MutationId);
public sealed record CosmeticEquipRequest(string Kind, string? CosmeticId);
public sealed record RenameCharacterRequest(string Name, Guid MutationId);
public sealed record PersonalizationMutationResponse(long CrystalBalance, string? CharacterName,
    string? ActiveFrameId, string? ActiveTitleId);
