namespace Elyndor.Core.Items;

public enum ItemReforgeOperationState
{
    Pending,
    Accepted,
    Kept
}

public sealed class ItemReforgeOperation
{
    private ItemReforgeOperation()
    {
        SlotKey = null!;
        MaterialItemId = null!;
        CatalystItemId = null!;
        CurrentItemJson = null!;
        ProposedItemJson = null!;
    }

    public ItemReforgeOperation(
        Guid operationId,
        Guid characterId,
        Guid itemInstanceId,
        string slotKey,
        int costGold,
        string materialItemId,
        int materialQuantity,
        string catalystItemId,
        int catalystQuantity,
        string currentItemJson,
        string proposedItemJson,
        DateTimeOffset createdAtUtc)
    {
        if (operationId == Guid.Empty || characterId == Guid.Empty || itemInstanceId == Guid.Empty)
            throw new ArgumentException("Reforge identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(slotKey);
        ArgumentOutOfRangeException.ThrowIfNegative(costGold);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialItemId);
        ArgumentOutOfRangeException.ThrowIfNegative(materialQuantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalystItemId);
        ArgumentOutOfRangeException.ThrowIfNegative(catalystQuantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentItemJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedItemJson);
        if (createdAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Reforge timestamps must be UTC.", nameof(createdAtUtc));

        OperationId = operationId;
        CharacterId = characterId;
        ItemInstanceId = itemInstanceId;
        SlotKey = slotKey;
        CostGold = costGold;
        MaterialItemId = materialItemId;
        MaterialQuantity = materialQuantity;
        CatalystItemId = catalystItemId;
        CatalystQuantity = catalystQuantity;
        CurrentItemJson = currentItemJson;
        ProposedItemJson = proposedItemJson;
        CreatedAtUtc = createdAtUtc;
        State = ItemReforgeOperationState.Pending;
    }

    public Guid OperationId { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid ItemInstanceId { get; private set; }
    public string SlotKey { get; private set; }
    public int CostGold { get; private set; }
    public string MaterialItemId { get; private set; }
    public int MaterialQuantity { get; private set; }
    public string CatalystItemId { get; private set; }
    public int CatalystQuantity { get; private set; }
    public string CurrentItemJson { get; private set; }
    public string ProposedItemJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public ItemReforgeOperationState State { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public void Decide(bool acceptProposed, DateTimeOffset decidedAtUtc)
    {
        if (decidedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Reforge timestamps must be UTC.", nameof(decidedAtUtc));
        if (State != ItemReforgeOperationState.Pending)
            return;

        State = acceptProposed
            ? ItemReforgeOperationState.Accepted
            : ItemReforgeOperationState.Kept;
        DecidedAtUtc = decidedAtUtc;
    }
}
