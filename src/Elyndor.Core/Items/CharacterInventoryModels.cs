using Elyndor.Core.Content;

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
        DateTimeOffset acquiredAtUtc,
        int definitionVersion = 1,
        PrimaryStats? rolledPrimaryStats = null)
    {
        if (id == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("Item identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(itemDefinitionId);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(definitionVersion, 1);
        if (acquiredAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Item timestamps must be UTC.", nameof(acquiredAtUtc));

        Id = id;
        CharacterId = characterId;
        ItemDefinitionId = itemDefinitionId;
        DefinitionVersion = definitionVersion;
        Quantity = quantity;
        AcquiredAtUtc = acquiredAtUtc;
        SetRolledPrimaryStats(rolledPrimaryStats);
    }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public string ItemDefinitionId { get; private set; }
    public int DefinitionVersion { get; private set; } = 1;
    public int Quantity { get; private set; }
    public DateTimeOffset AcquiredAtUtc { get; private set; }
    public bool IsLocked { get; private set; }
    public decimal? RolledStrength { get; private set; }
    public decimal? RolledAgility { get; private set; }
    public decimal? RolledIntellect { get; private set; }
    public decimal? RolledStamina { get; private set; }

    public PrimaryStats? RolledPrimaryStats =>
        RolledStrength.HasValue
        && RolledAgility.HasValue
        && RolledIntellect.HasValue
        && RolledStamina.HasValue
            ? new PrimaryStats(
                RolledStrength.Value,
                RolledAgility.Value,
                RolledIntellect.Value,
                RolledStamina.Value)
            : null;

    public void SetLocked(bool isLocked) => IsLocked = isLocked;

    private void SetRolledPrimaryStats(PrimaryStats? stats)
    {
        RolledStrength = stats?.Strength;
        RolledAgility = stats?.Agility;
        RolledIntellect = stats?.Intellect;
        RolledStamina = stats?.Stamina;
    }

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
