using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public sealed class PendingLootItem
{
    private PendingLootItem()
    {
        ItemDefinitionId = null!;
    }

    public PendingLootItem(
        Guid id,
        Guid characterId,
        Guid rewardResolutionId,
        string itemDefinitionId,
        int quantity,
        int definitionVersion,
        DateTimeOffset createdAtUtc,
        PrimaryStats? rolledPrimaryStats = null,
        string? generatedItemJson = null,
        string? generationSeedHash = null,
        string? sourceType = null,
        Guid? sourceOperationId = null,
        string? sourceEntryId = null)
    {
        if (id == Guid.Empty
            || characterId == Guid.Empty
            || rewardResolutionId == Guid.Empty)
        {
            throw new ArgumentException("Pending loot identifiers cannot be empty.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(itemDefinitionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(definitionVersion);
        if (createdAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Pending loot timestamps must be UTC.", nameof(createdAtUtc));

        Id = id;
        CharacterId = characterId;
        RewardResolutionId = rewardResolutionId;
        ItemDefinitionId = itemDefinitionId;
        Quantity = quantity;
        DefinitionVersion = definitionVersion;
        CreatedAtUtc = createdAtUtc;
        GeneratedItemJson = generatedItemJson;
        GenerationSeedHash = generationSeedHash;
        SourceType = sourceType;
        SourceOperationId = sourceOperationId;
        SourceEntryId = sourceEntryId;
        SetRolledPrimaryStats(rolledPrimaryStats);
    }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid RewardResolutionId { get; private set; }
    public string ItemDefinitionId { get; private set; }
    public int Quantity { get; private set; }
    public int DefinitionVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public decimal? RolledStrength { get; private set; }
    public decimal? RolledAgility { get; private set; }
    public decimal? RolledIntellect { get; private set; }
    public decimal? RolledStamina { get; private set; }
    public string? GeneratedItemJson { get; private set; }
    public string? GenerationSeedHash { get; private set; }
    public string? SourceType { get; private set; }
    public Guid? SourceOperationId { get; private set; }
    public string? SourceEntryId { get; private set; }

    public PrimaryStats? RolledPrimaryStats =>
        RolledStrength.HasValue
        || RolledAgility.HasValue
        || RolledIntellect.HasValue
        || RolledStamina.HasValue
            ? new PrimaryStats(
                RolledStrength ?? 0,
                RolledAgility ?? 0,
                RolledIntellect ?? 0,
                RolledStamina ?? 0)
            : null;

    public void SetQuantity(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Quantity = quantity;
    }

    private void SetRolledPrimaryStats(PrimaryStats? stats)
    {
        RolledStrength = stats?.Strength;
        RolledAgility = stats?.Agility;
        RolledIntellect = stats?.Intellect;
        RolledStamina = stats?.Stamina;
    }
}
