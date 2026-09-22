using Elyndor.Core.World;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public sealed class ContentValidationPipeline
{
    private readonly IReadOnlyList<IContentValidationStage> stages;

    public ContentValidationPipeline(IReadOnlyList<IContentValidationStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        this.stages = stages;
    }

    public static ContentValidationPipeline Default { get; } = new(
        [
            new MetadataValidator(),
            new DefinitionValidator(),
            new CharacterValidator(),
            new AbilityValidator(),
            new TalentValidator(),
            new ItemValidator(),
            new EnhancementCatalystSourceValidator(),
            new MerchantValidator(),
            new MonsterValidator(),
            new EncounterContentValidator(),
            new WorldValidator(),
            new DungeonValidator()
        ]);

    public IReadOnlyList<ContentValidationError> Validate(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        ContentValidationContext context = new(package);
        foreach (IContentValidationStage stage in stages)
            stage.Validate(context);

        return context.Errors;
    }
}

public interface IContentValidationStage
{
    void Validate(ContentValidationContext context);
}

public sealed class ContentValidationContext
{
    internal ContentValidationContext(GameContentPackage package)
    {
        Package = package;
    }

    public GameContentPackage Package { get; }

    public List<ContentValidationError> Errors { get; } = [];

    internal IReadOnlySet<GameContentPackageValidator.ContentKey>? Definitions { get; set; }
}

public sealed class MetadataValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateMetadata(context.Package, context.Errors);
}

public sealed class DefinitionValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        HashSet<GameContentPackageValidator.ContentKey> definitions =
            GameContentPackageValidator.ValidateDefinitions(context.Package, context.Errors);
        context.Definitions = definitions;
        GameContentPackageValidator.ValidateReferences(context.Package, definitions, context.Errors);
    }
}

public sealed class CharacterValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateCharacterProfiles(
            context.Package,
            context.Definitions ?? new HashSet<GameContentPackageValidator.ContentKey>(),
            context.Errors);
}

public sealed class AbilityValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateCombatDefinitions(context.Package, context.Errors);
}

public sealed class TalentValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateTalentDefinitions(
            context.Package.TalentTrees ?? [],
            context.Package.Abilities ?? [],
            context.Package.ClassProfiles ?? [],
            context.Errors);
}

public sealed class ItemValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        GameContentPackage original = context.Package;
        GameContentPackage compatibilityProjection = original with
        {
            Items = original.Items is null
                ? null
                : original.Items
                    .Select(item => item.Type == ItemType.SpatialArtifact
                        ? item with
                        {
                            Type = ItemType.Material,
                            Stackable = true,
                            MaxStack = Math.Max(2, item.MaxStack),
                            Slot = null
                        }
                        : item)
                    .ToArray()
        };

        GameContentPackageValidator.ValidateProgressionItemsAndLoot(
            compatibilityProjection,
            context.Errors);
        GameContentPackageValidator.ValidateItemization(
            compatibilityProjection,
            context.Errors);
        GameContentPackageValidator.ValidatePremiumStore(
            compatibilityProjection,
            context.Errors);
        GameContentPackageValidator.ValidatePromoCodes(
            compatibilityProjection,
            context.Errors);

        ValidateIconIds(original, context.Errors);

        ValidateSpatialArtifacts(original, context.Errors);
    }

    private static void ValidateIconIds(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        IReadOnlyList<ItemDefinition> items = package.Items ?? [];
        for (var index = 0; index < items.Count; index++)
        {
            ItemDefinition item = items[index];
            if (item.IconId is null)
                continue;

            if (!ItemIconId.IsCanonical(item.IconId))
            {
                errors.Add(new(
                    "ITEM_ICON_INVALID_PATH",
                    $"items[{index}].iconId",
                    $"Item '{item.Id}' has invalid icon id '{item.IconId}'. "
                    + "Item icon ids must be lowercase, extensionless paths relative to the item asset root."));
            }
        }
    }

    private static void ValidateSpatialArtifacts(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        IReadOnlyList<ItemDefinition> items = package.Items ?? [];
        for (var index = 0; index < items.Count; index++)
        {
            ItemDefinition item = items[index];
            if (item.Type != ItemType.SpatialArtifact)
            {
                if (item.InventoryCapacityBonus != 0)
                {
                    errors.Add(new(
                        "INVALID_SPATIAL_ARTIFACT_BONUS",
                        $"items[{index}].inventoryCapacityBonus",
                        $"Non-spatial item '{item.Id}' cannot grant inventory capacity."));
                }
                continue;
            }

            bool hasCombatStats = item.Stats != new PrimaryStats(0, 0, 0, 0)
                || item.SetId is not null
                || item.WeaponBaseAttackIntervalSeconds is not null
                || item.AttackSpeedPercent != 0
                || item.DodgePercent != 0
                || item.MaxHpFlat != 0
                || item.AttackPowerFlat != 0
                || item.SpellPowerFlat != 0
                || item.CriticalChancePercent != 0
                || item.CriticalDamagePercent != 0
                || item.AccuracyPercent != 0
                || item.ArmorFlat != 0
                || item.MagicResistanceFlat != 0
                || item.ArmorPenetrationPercent != 0
                || item.MagicPenetrationPercent != 0
                || item.MaxResourceFlat != 0
                || item.BlockChancePercent != 0
                || item.BlockValueMin != 0
                || item.BlockValueMax != 0
                || item.PrimaryStatRanges is not null
                || item.WeaponDamageMin is not null
                || item.WeaponDamageMax is not null;

            if (item.Stackable
                || item.MaxStack != 1
                || item.Slot is not null
                || item.InventoryCapacityBonus <= 0
                || hasCombatStats
                || item.ConsumableActions is { Count: > 0 }
                || item.ConsumableCooldownSeconds != 0
                || item.WeaponCategory is not null
                || item.ArmorCategory is not null
                || item.OffHandCategory is not null)
            {
                errors.Add(new(
                    "INVALID_SPATIAL_ARTIFACT",
                    $"items[{index}]",
                    $"Spatial artifact '{item.Id}' must be a non-stackable, non-combat capacity item."));
            }
        }
    }
}

public sealed class MerchantValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateMerchantDefinitions(context.Package, context.Errors);
}

public sealed class MonsterValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateMonsterDefinitions(context.Package, context.Errors);
}

public sealed class WorldValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        GameContentPackageValidator.ValidateLocations(context.Package.Locations, context.Errors);
        GameContentPackageValidator.ValidateWorldContracts(context.Package, context.Errors);
        GameContentPackageValidator.ValidateQuests(context.Package, context.Errors);
        context.Errors.AddRange(WorldEncounterContentValidator.Validate(context.Package));
    }
}

public sealed class DungeonValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateDungeons(context.Package, context.Errors);
}
