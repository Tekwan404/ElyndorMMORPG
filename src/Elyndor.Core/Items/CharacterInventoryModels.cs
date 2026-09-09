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

    public int? ItemLevel { get; private set; }
    public decimal? MinimumTemplateItemPower { get; private set; }
    public decimal? ActualItemPower { get; private set; }
    public decimal? MaxTemplateItemPower { get; private set; }
    public decimal? RollQuality { get; private set; }
    public int? Stars { get; private set; }
    public bool IsPerfect { get; private set; }
    public string? PerfectOrigin { get; private set; }
    public int GenerationVersion { get; private set; }
    public string? GenerationSeedHash { get; private set; }
    public string? GeneratedPrefixId { get; private set; }
    public string? GeneratedSuffixId { get; private set; }
    public string? GeneratedDisplayName { get; private set; }
    public string? ReforgeSlotKey { get; private set; }
    public int ReforgeCount { get; private set; }
    public int EnhancementLevel { get; private set; }
    public string? SourceType { get; private set; }
    public Guid? SourceOperationId { get; private set; }
    public string? SourceEntryId { get; private set; }
    public List<ItemRolledAffix> Affixes { get; private set; } = [];

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

    public bool IsProcedurallyGenerated => ItemLevel.HasValue && GenerationVersion > 0;

    public void ApplyGeneratedInstance(
        GeneratedItemInstance generated,
        string generationSeedHash,
        string sourceType,
        Guid sourceOperationId,
        string sourceEntryId)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationSeedHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        if (sourceOperationId == Guid.Empty)
            throw new ArgumentException("Source operation identifier cannot be empty.", nameof(sourceOperationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntryId);

        if (IsProcedurallyGenerated)
        {
            if (string.Equals(GenerationSeedHash, generationSeedHash, StringComparison.Ordinal))
                return;
            throw new InvalidOperationException("Generated item metadata is already assigned.");
        }

        ItemLevel = generated.ItemLevel;
        MinimumTemplateItemPower = generated.MinimumTemplateItemPower;
        ActualItemPower = generated.ActualItemPower;
        MaxTemplateItemPower = generated.MaxTemplateItemPower;
        RollQuality = generated.RollQuality;
        Stars = generated.Stars;
        IsPerfect = generated.IsPerfect;
        PerfectOrigin = generated.PerfectOrigin;
        GenerationVersion = generated.GenerationVersion;
        GenerationSeedHash = generationSeedHash;
        GeneratedPrefixId = generated.GeneratedPrefixId;
        GeneratedSuffixId = generated.GeneratedSuffixId;
        GeneratedDisplayName = generated.DisplayName;
        EnhancementLevel = 0;
        SourceType = sourceType;
        SourceOperationId = sourceOperationId;
        SourceEntryId = sourceEntryId;
        Affixes.Clear();
        Affixes.AddRange(generated.Affixes.Select(affix => new ItemRolledAffix(Id, affix)));
        SetRolledPrimaryStats(null);
    }

    public void SetLocked(bool isLocked) => IsLocked = isLocked;

    public void SelectReforgeSlot(string slotKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotKey);
        if (ReforgeSlotKey is not null
            && !string.Equals(ReforgeSlotKey, slotKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only one affix slot may be selected for Reforge.");
        }
        if (!Affixes.Any(affix => string.Equals(affix.SlotKey, slotKey, StringComparison.Ordinal)))
            throw new InvalidOperationException("Selected Reforge affix slot does not exist.");
        ReforgeSlotKey = slotKey;
        foreach (ItemRolledAffix affix in Affixes)
            affix.SetReforgeSlot(string.Equals(affix.SlotKey, slotKey, StringComparison.Ordinal));
    }

    public void ApplyReforge(
        GeneratedItemAffix updatedAffix,
        decimal minimumTemplateItemPower,
        decimal actualItemPower,
        decimal maxTemplateItemPower,
        decimal rollQuality,
        int stars,
        bool isPerfect,
        string? perfectOrigin,
        string? generatedPrefixId,
        string? generatedSuffixId,
        string generatedDisplayName)
    {
        ArgumentNullException.ThrowIfNull(updatedAffix);
        if (ReforgeSlotKey is null
            || !string.Equals(ReforgeSlotKey, updatedAffix.SlotKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Reforge may only mutate the selected affix slot.");
        }

        ItemRolledAffix affix = Affixes.Single(candidate =>
            string.Equals(candidate.SlotKey, ReforgeSlotKey, StringComparison.Ordinal));
        affix.ReplaceFrom(updatedAffix);
        MinimumTemplateItemPower = minimumTemplateItemPower;
        ActualItemPower = actualItemPower;
        MaxTemplateItemPower = maxTemplateItemPower;
        RollQuality = rollQuality;
        Stars = stars;
        IsPerfect = isPerfect;
        PerfectOrigin = perfectOrigin;
        GeneratedPrefixId = generatedPrefixId;
        GeneratedSuffixId = generatedSuffixId;
        GeneratedDisplayName = generatedDisplayName;
        ReforgeCount++;
    }

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

public sealed class ItemRolledAffix
{
    private ItemRolledAffix()
    {
        SlotKey = null!;
        AffixDefinitionId = null!;
        StatId = null!;
    }

    public ItemRolledAffix(Guid itemInstanceId, GeneratedItemAffix affix)
    {
        if (itemInstanceId == Guid.Empty)
            throw new ArgumentException("Item instance identifier cannot be empty.", nameof(itemInstanceId));
        ArgumentNullException.ThrowIfNull(affix);

        ItemInstanceId = itemInstanceId;
        SlotKey = affix.SlotKey;
        AffixDefinitionId = affix.AffixDefinitionId;
        StatId = affix.StatId;
        Value = affix.Value;
        MinAtGeneration = affix.MinAtGeneration;
        MaxAtGeneration = affix.MaxAtGeneration;
        StepAtGeneration = affix.StepAtGeneration;
        AffixTier = affix.AffixTier;
        IsGuaranteed = affix.IsGuaranteed;
        IsReforgeSlot = affix.IsReforgeSlot;
        GenerationOrdinal = affix.GenerationOrdinal;
    }

    public Guid ItemInstanceId { get; private set; }
    public string SlotKey { get; private set; }
    public string AffixDefinitionId { get; private set; }
    public string StatId { get; private set; }
    public decimal Value { get; private set; }
    public decimal MinAtGeneration { get; private set; }
    public decimal MaxAtGeneration { get; private set; }
    public decimal StepAtGeneration { get; private set; }
    public int AffixTier { get; private set; }
    public bool IsGuaranteed { get; private set; }
    public bool IsReforgeSlot { get; private set; }
    public int GenerationOrdinal { get; private set; }

    public GeneratedItemAffix ToGeneratedAffix() =>
        new(
            SlotKey,
            AffixDefinitionId,
            StatId,
            Value,
            MinAtGeneration,
            MaxAtGeneration,
            StepAtGeneration,
            AffixTier,
            IsGuaranteed,
            IsReforgeSlot,
            GenerationOrdinal);

    public void SetReforgeSlot(bool selected) => IsReforgeSlot = selected;

    public void ReplaceFrom(GeneratedItemAffix affix)
    {
        ArgumentNullException.ThrowIfNull(affix);
        if (!string.Equals(SlotKey, affix.SlotKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Cannot replace a different affix slot.");
        AffixDefinitionId = affix.AffixDefinitionId;
        StatId = affix.StatId;
        Value = affix.Value;
        MinAtGeneration = affix.MinAtGeneration;
        MaxAtGeneration = affix.MaxAtGeneration;
        StepAtGeneration = affix.StepAtGeneration;
        AffixTier = affix.AffixTier;
        IsGuaranteed = affix.IsGuaranteed;
        IsReforgeSlot = true;
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
