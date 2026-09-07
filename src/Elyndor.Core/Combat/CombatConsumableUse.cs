namespace Elyndor.Core.Combat;

public sealed class CombatConsumableUse
{
    private CombatConsumableUse()
    {
        CommandId = null!;
        ItemDefinitionId = null!;
    }

    public CombatConsumableUse(
        Guid sessionId,
        string commandId,
        Guid characterId,
        string itemDefinitionId,
        int definitionVersion,
        int maxStack,
        DateTimeOffset usedAtUtc)
    {
        if (sessionId == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("Consumable use identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemDefinitionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(definitionVersion);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStack, 2);
        if (usedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Consumable timestamps must be UTC.", nameof(usedAtUtc));

        SessionId = sessionId;
        CommandId = commandId;
        CharacterId = characterId;
        ItemDefinitionId = itemDefinitionId;
        DefinitionVersion = definitionVersion;
        MaxStack = maxStack;
        UsedAtUtc = usedAtUtc;
    }

    public Guid SessionId { get; private set; }
    public string CommandId { get; private set; }
    public Guid CharacterId { get; private set; }
    public string ItemDefinitionId { get; private set; }
    public int DefinitionVersion { get; private set; }
    public int MaxStack { get; private set; }
    public DateTimeOffset UsedAtUtc { get; private set; }
}
