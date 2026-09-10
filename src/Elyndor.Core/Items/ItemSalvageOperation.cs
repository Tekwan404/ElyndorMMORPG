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
    {
        OperationId = operationId;
        CharacterId = characterId;
        ItemInstanceId = itemInstanceId;
        ItemDefinitionId = itemDefinitionId;
        ReforgeStoneItemId = reforgeStoneItemId;
        ReforgeStoneQuantity = reforgeStoneQuantity;
        MaterialItemId = materialItemId;
        MaterialQuantity = materialQuantity;
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
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
