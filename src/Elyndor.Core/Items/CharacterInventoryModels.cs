namespace Elyndor.Core.Items;

public sealed class CharacterItem
{
    private CharacterItem()
    {
        ItemDefinitionId = null!;
    }

    public CharacterItem(
        Guid id,
        Guid characterId,
        string itemDefinitionId,
        int quantity,
        DateTimeOffset acquiredAtUtc)
    {
        if (id == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("Item identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(itemDefinitionId);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        if (acquiredAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Item timestamps must be UTC.", nameof(acquiredAtUtc));

        Id = id;
        CharacterId = characterId;
        ItemDefinitionId = itemDefinitionId;
        Quantity = quantity;
        AcquiredAtUtc = acquiredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public string ItemDefinitionId { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset AcquiredAtUtc { get; private set; }

    public void AddQuantity(int quantity, int maxStack)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStack, 1);
        int updated = checked(Quantity + quantity);
        if (updated > maxStack)
            throw new InvalidOperationException("Item stack would exceed its maximum size.");
        Quantity = updated;
    }

    public void RemoveQuantity(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        if (quantity > Quantity)
            throw new InvalidOperationException("Item stack does not contain enough quantity.");
        Quantity -= quantity;
    }
}

public sealed class CharacterEquipment
{
    private CharacterEquipment()
    {
    }

    public CharacterEquipment(Guid characterId, EquipmentSlot slot, Guid characterItemId)
    {
        if (characterId == Guid.Empty || characterItemId == Guid.Empty)
            throw new ArgumentException("Equipment identifiers cannot be empty.");
        CharacterId = characterId;
        Slot = slot;
        CharacterItemId = characterItemId;
    }

    public Guid CharacterId { get; private set; }
    public EquipmentSlot Slot { get; private set; }
    public Guid CharacterItemId { get; private set; }

    public void Equip(Guid characterItemId)
    {
        if (characterItemId == Guid.Empty)
            throw new ArgumentException("Item identifier cannot be empty.", nameof(characterItemId));
        CharacterItemId = characterItemId;
    }
}
