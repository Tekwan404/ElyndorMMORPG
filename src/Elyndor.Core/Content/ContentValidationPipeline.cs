using Elyndor.Core.World;
using Elyndor.Core.Items;
using Elyndor.Core.Combat.ItemEffects;

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
            new CharacterSkinValidator(),
            new AbilityValidator(),
            new TalentValidator(),
            new ItemValidator(),
            new EnhancementCatalystSourceValidator(),
            new MerchantValidator(),
            new MonsterValidator(),
            new WorldBossValidator(),
            new CombatBalanceValidator(),
            new ProgressionBalanceValidator(),
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

public sealed class CharacterSkinValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < (context.Package.CharacterSkins?.Count ?? 0); index++)
        {
            var skin = context.Package.CharacterSkins![index];
            string path = $"characterSkins[{index}]";
            if (string.IsNullOrWhiteSpace(skin.Id) || !seen.Add(skin.Id))
                context.Errors.Add(new("CHARACTER_SKIN_DUPLICATE_ID", path, "Skin id is empty or duplicated."));
            if (string.IsNullOrWhiteSpace(skin.Name) || !(context.Package.ClassProfiles ?? []).Any(item => item.Id == skin.ClassId)
                || skin.GenderId is not ("FEMALE" or "MALE") || skin.CrystalPrice < 0
                || (skin.Purchasable && skin.CrystalPrice == 0))
                context.Errors.Add(new("CHARACTER_SKIN_INVALID", path, "Skin name, class, gender or price is invalid."));
            if (string.IsNullOrWhiteSpace(skin.ImageId)
                || skin.ImageId.Contains('/') || skin.ImageId.Contains('\\') || skin.ImageId.Contains("..")
                || !skin.ImageId.All(ch => char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch == '-')
                || string.IsNullOrWhiteSpace(skin.Id)
                || !string.Equals(skin.ImageId, skin.Id.ToLowerInvariant().Replace('_', '-'), StringComparison.Ordinal))
                context.Errors.Add(new("CHARACTER_SKIN_INVALID_IMAGE", path, "Skin image id must be a lowercase asset basename."));
        }
    }
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
        ValidateItemSpecialEffects(original, context.Errors);

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

    private static void ValidateItemSpecialEffects(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        IReadOnlyList<ItemSpecialEffectDefinition> definitions =
            package.ItemSpecialEffects ?? [];
        Dictionary<string, ItemSpecialEffectDefinition> byId =
            new(StringComparer.Ordinal);

        for (var index = 0; index < definitions.Count; index++)
        {
            ItemSpecialEffectDefinition definition = definitions[index];
            string path = $"itemSpecialEffects[{index}]";

            if (string.IsNullOrWhiteSpace(definition.Id)
                || !byId.TryAdd(definition.Id, definition))
            {
                errors.Add(new(
                    "INVALID_ITEM_SPECIAL_EFFECT_ID",
                    $"{path}.id",
                    "Item special effect id is empty or duplicated."));
                continue;
            }

            try
            {
                _ = new ItemSpecialEffectEvaluator([definition]);
            }
            catch (ArgumentException exception)
            {
                errors.Add(new(
                    "INVALID_ITEM_SPECIAL_EFFECT",
                    path,
                    $"Item special effect '{definition.Id}' is invalid: {exception.Message}"));
                continue;
            }

            foreach (ItemSpecialEffectActionDefinition action in definition.Actions)
            {
                if (action.Kind == ItemSpecialEffectActionKind.ModifyCooldown)
                {
                    if (string.IsNullOrWhiteSpace(action.AbilityId)
                        || !(package.Abilities ?? []).Any(ability =>
                            string.Equals(
                                ability.Id,
                                action.AbilityId,
                                StringComparison.Ordinal)))
                    {
                        errors.Add(new(
                            "MISSING_ITEM_SPECIAL_EFFECT_ABILITY",
                            path,
                            $"Item special effect '{definition.Id}' references missing ability '{action.AbilityId}'."));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(action.AbilityId))
                {
                    errors.Add(new(
                        "INVALID_ITEM_SPECIAL_EFFECT_ABILITY",
                        path,
                        $"Item special effect '{definition.Id}' sets abilityId for a non-cooldown action."));
                }
            }
        }

        HashSet<string> referenced = new(StringComparer.Ordinal);
        IReadOnlyList<ItemDefinition> items = package.Items ?? [];
        for (var index = 0; index < items.Count; index++)
        {
            ItemDefinition item = items[index];
            if (item.SpecialEffectIds is null)
                continue;

            string path = $"items[{index}].specialEffectIds";
            bool invalidShape = item.Type != ItemType.Equipment
                || item.SpecialEffectIds.Count == 0
                || item.SpecialEffectIds.Any(string.IsNullOrWhiteSpace)
                || item.SpecialEffectIds.Distinct(StringComparer.Ordinal).Count()
                    != item.SpecialEffectIds.Count;
            if (invalidShape)
            {
                errors.Add(new(
                    "INVALID_ITEM_SPECIAL_EFFECT_REFERENCE",
                    path,
                    $"Item '{item.Id}' has invalid special-effect references."));
                continue;
            }

            foreach (string effectId in item.SpecialEffectIds)
            {
                referenced.Add(effectId);
                if (!byId.ContainsKey(effectId))
                {
                    errors.Add(new(
                        "MISSING_ITEM_SPECIAL_EFFECT_REFERENCE",
                        path,
                        $"Item '{item.Id}' references missing special effect '{effectId}'."));
                }
            }
        }

        for (var index = 0; index < definitions.Count; index++)
        {
            ItemSpecialEffectDefinition definition = definitions[index];
            if (!referenced.Contains(definition.Id))
            {
                errors.Add(new(
                    "UNREFERENCED_ITEM_SPECIAL_EFFECT",
                    $"itemSpecialEffects[{index}]",
                    $"Item special effect '{definition.Id}' is not referenced by equipment."));
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
                || item.WeaponDamageMax is not null
                || item.SpecialEffectIds is { Count: > 0 };

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

public sealed class WorldBossValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateWorldBossDefinitions(
            context.Package,
            context.Errors);
}

public sealed class CombatBalanceValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateCombatBalance(context.Package, context.Errors);
}

public sealed class ProgressionBalanceValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateProgressionBalance(
            context.Package,
            context.Errors);
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
