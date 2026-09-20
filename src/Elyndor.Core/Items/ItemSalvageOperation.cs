namespace Elyndor.Core.Items;

public sealed class ItemSalvageOperation
{
    private ItemSalvageOperation() { }

    public ItemSalvageOperation(
        Guid operationId,
        Guid characterId,
        Guid itemInstanceId,
        string itemDefinitionId,
        string reforgeStoneItemId,
        int reforgeStoneQuantity,
        string materialItemId,
        int materialQuantity,
        DateTimeOffset createdAtUtc)
        : this(
            operationId,
            characterId,
            itemInstanceId,
            itemDefinitionId,
            reforgeStoneItemId,
            reforgeStoneQuantity,
            materialItemId,
            materialQuantity,
            null,
            0,
            null,
            0,
            createdAtUtc)
    {
    }

    public ItemSalvageOperation(
        Guid operationId,
        Guid characterId,
        Guid itemInstanceId,
        string itemDefinitionId,
        string reforgeStoneItemId,
        int reforgeStoneQuantity,
        string materialItemId,
        int materialQuantity,
        string? enhancementMaterialItemId,
        int enhancementMaterialQuantity,
        string? catalystItemId,
        int catalystQuantity,
        DateTimeOffset createdAtUtc)
    {
        OperationId = operationId;
        CharacterId = characterId;
        ItemInstanceId = itemInstanceId;
        ItemDefinitionId = itemDefinitionId;
        ReforgeStoneItemId = reforgeStoneItemId;
        ReforgeStoneQuantity = reforgeStoneQuantity;
        MaterialItemId = materialItemId;
        MaterialQuantity = materialQuantity;
        EnhancementMaterialItemId = enhancementMaterialItemId;
        EnhancementMaterialQuantity = enhancementMaterialQuantity;
        CatalystItemId = catalystItemId;
        CatalystQuantity = catalystQuantity;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid OperationId { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid ItemInstanceId { get; private set; }
    public string ItemDefinitionId { get; private set; } = null!;
    public string ReforgeStoneItemId { get; private set; } = null!;
    public int ReforgeStoneQuantity { get; private set; }
    public string MaterialItemId { get; private set; } = null!;
    public int MaterialQuantity { get; private set; }
    public string? EnhancementMaterialItemId { get; private set; }
    public int EnhancementMaterialQuantity { get; private set; }
    public string? CatalystItemId { get; private set; }
    public int CatalystQuantity { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
