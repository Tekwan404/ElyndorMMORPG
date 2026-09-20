namespace Elyndor.Contracts.Items;

public sealed record SpatialArtifactResponse(
    Guid CharacterItemId,
    string DefinitionId,
    string Name,
    string Rarity,
    int CapacityBonus,
    string? IconId);

public sealed record InventoryCapacityResponse(
    int BaseCapacity,
    int ArtifactCapacityBonus,
    int Capacity,
    int UsedSlots,
    int FreeSlots,
    bool IsOverflow);

public sealed record SpatialInventoryResponse(
    SpatialArtifactResponse? EquippedArtifact,
    InventoryCapacityResponse Capacity);

public sealed record EquipSpatialArtifactRequest(Guid CharacterItemId);
