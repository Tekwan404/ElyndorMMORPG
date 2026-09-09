using System.Text.Json;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.Infrastructure.Items;

public static class ItemInstancePersistenceFactory
{
    public static CharacterItem CreateCharacterItem(
        Guid characterId,
        ItemDefinition definition,
        Guid sourceOperationId,
        string sourceType,
        string sourceEntryId,
        int ordinal,
        DateTimeOffset acquiredAtUtc,
        GameContentPackage content,
        string qualityProfileId = "NORMAL",
        Guid? itemId = null,
        Elyndor.Core.Combat.Randomness.IGameRandom? legacyRandom = null)
    {
        ItemGenerationKey key = ItemGenerationKey.Create(
            sourceOperationId,
            $"{sourceEntryId}|{definition.Id}",
            ordinal);
        GeneratedItemInstance? generated = ProceduralItemPolicy.Generate(
            definition,
            content.Itemization,
            qualityProfileId,
            key);
        PrimaryStats? legacyRoll = generated is null
            && definition.Type == ItemType.Equipment
            ? ItemInstanceStatRoller.Resolve(
                definition,
                legacyRandom ?? new Elyndor.Core.Combat.Randomness.SeededGameRandom(key.Seed))
            : null;

        CharacterItem item = new(
            itemId ?? Guid.CreateVersion7(),
            characterId,
            definition.Id,
            1,
            acquiredAtUtc,
            definition.Version,
            legacyRoll);
        if (generated is not null)
        {
            item.ApplyGeneratedInstance(
                generated,
                key.AuditHash,
                sourceType,
                sourceOperationId,
                sourceEntryId);
        }
        return item;
    }

    public static PendingLootItem CreatePendingLootItem(
        Guid characterId,
        ItemDefinition definition,
        Guid sourceOperationId,
        string sourceType,
        string sourceEntryId,
        int ordinal,
        DateTimeOffset acquiredAtUtc,
        GameContentPackage content,
        string qualityProfileId = "NORMAL")
    {
        ItemGenerationKey key = ItemGenerationKey.Create(
            sourceOperationId,
            $"{sourceEntryId}|{definition.Id}",
            ordinal);
        GeneratedItemInstance? generated = ProceduralItemPolicy.Generate(
            definition,
            content.Itemization,
            qualityProfileId,
            key);
        PrimaryStats? legacyRoll = generated is null
            && definition.Type == ItemType.Equipment
            ? ItemInstanceStatRoller.Resolve(
                definition,
                new Elyndor.Core.Combat.Randomness.SeededGameRandom(key.Seed))
            : null;

        return new PendingLootItem(
            Guid.CreateVersion7(),
            characterId,
            sourceOperationId,
            definition.Id,
            1,
            definition.Version,
            acquiredAtUtc,
            legacyRoll,
            generated is null ? null : JsonSerializer.Serialize(generated),
            generated is null ? null : key.AuditHash,
            sourceType,
            sourceOperationId,
            sourceEntryId);
    }

    public static GeneratedItemInstance? ToGeneratedInstance(
        CharacterItem item,
        ItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(definition);
        if (!item.IsProcedurallyGenerated
            || !item.ItemLevel.HasValue
            || !item.MinimumTemplateItemPower.HasValue
            || !item.ActualItemPower.HasValue
            || !item.MaxTemplateItemPower.HasValue
            || !item.RollQuality.HasValue
            || !item.Stars.HasValue)
        {
            return null;
        }

        return new GeneratedItemInstance(
            item.ItemLevel.Value,
            item.Affixes
                .OrderBy(affix => affix.GenerationOrdinal)
                .Select(affix => affix.ToGeneratedAffix())
                .ToArray(),
            item.MinimumTemplateItemPower.Value,
            item.ActualItemPower.Value,
            item.MaxTemplateItemPower.Value,
            item.RollQuality.Value,
            item.Stars.Value,
            item.IsPerfect,
            item.PerfectOrigin,
            item.GeneratedPrefixId,
            item.GeneratedSuffixId,
            item.GeneratedDisplayName ?? definition.Name,
            item.GenerationVersion);
    }

    public static CharacterItem MaterializePending(
        PendingLootItem pending,
        ItemDefinition definition,
        DateTimeOffset acquiredAtUtc)
    {
        CharacterItem item = new(
            pending.Id,
            pending.CharacterId,
            definition.Id,
            1,
            acquiredAtUtc,
            definition.Version,
            pending.RolledPrimaryStats);
        if (!string.IsNullOrWhiteSpace(pending.GeneratedItemJson))
        {
            GeneratedItemInstance generated =
                JsonSerializer.Deserialize<GeneratedItemInstance>(pending.GeneratedItemJson)
                ?? throw new InvalidDataException("Pending generated item payload is invalid.");
            item.ApplyGeneratedInstance(
                generated,
                pending.GenerationSeedHash
                    ?? throw new InvalidDataException("Pending generated item seed is missing."),
                pending.SourceType ?? "PENDING_LOOT",
                pending.SourceOperationId ?? pending.RewardResolutionId,
                pending.SourceEntryId ?? definition.Id);
        }
        return item;
    }
}
