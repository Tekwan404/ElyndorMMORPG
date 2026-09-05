using Elyndor.Core.World;

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
            new MerchantValidator(),
            new MonsterValidator(),
            new WorldValidator()
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
            context.Errors);
}

public sealed class ItemValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context) =>
        GameContentPackageValidator.ValidateProgressionItemsAndLoot(context.Package, context.Errors);
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
        context.Errors.AddRange(WorldEncounterContentValidator.Validate(context.Package));
    }
}
