namespace Elyndor.Contracts.Items;

public sealed record OpenLootContainerRequest(
    Guid CharacterItemId,
    Guid MutationId);

public sealed record LootContainerRewardItemResponse(
    string DefinitionId,
    string Name,
    string Rarity,
    int Quantity,
    string? IconId,
    bool Pending);

public sealed record OpenLootContainerResponse(
    bool WasReplay,
    int Gold,
    IReadOnlyList<LootContainerRewardItemResponse> Items);
