namespace Elyndor.Core.Professions;

public sealed record ResourceSourceDefinition(
    string Id, string LocationId, string ProfessionId, string DisplayName, string Description,
    string ItemId, int MinQuantity, int MaxQuantity, int RequiredSkill, int SkillUpUntil,
    int RespawnSeconds);

public sealed class ResourceNodeState
{
    private ResourceNodeState() { NodeId = null!; }
    public ResourceNodeState(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        NodeId = nodeId;
        AvailableAtUtc = DateTimeOffset.UnixEpoch;
    }
    public string NodeId { get; private set; }
    public DateTimeOffset AvailableAtUtc { get; private set; }
    public bool TryHarvest(DateTimeOffset now, int respawnSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(respawnSeconds, 1);
        if (now.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(now));
        if (now < AvailableAtUtc) return false;
        AvailableAtUtc = now.AddSeconds(respawnSeconds);
        return true;
    }
}

public sealed class ResourceGatheringReceipt
{
    private ResourceGatheringReceipt() { NodeId = null!; ItemId = null!; }
    public ResourceGatheringReceipt(Guid characterId, Guid mutationId, string nodeId,
        string itemId, int quantity, bool skillIncreased, DateTimeOffset gatheredAtUtc)
    {
        CharacterId = characterId;
        MutationId = mutationId;
        NodeId = nodeId;
        ItemId = itemId;
        Quantity = quantity;
        SkillIncreased = skillIncreased;
        GatheredAtUtc = gatheredAtUtc;
    }
    public Guid CharacterId { get; private set; }
    public Guid MutationId { get; private set; }
    public string NodeId { get; private set; }
    public string ItemId { get; private set; }
    public int Quantity { get; private set; }
    public bool SkillIncreased { get; private set; }
    public DateTimeOffset GatheredAtUtc { get; private set; }
}
