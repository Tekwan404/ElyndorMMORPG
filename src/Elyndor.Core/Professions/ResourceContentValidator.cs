using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.Core.Professions;

public sealed class ResourceContentValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        var package = context.Package;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in package.ResourceSources ?? [])
        {
            var profession = (package.Professions ?? []).FirstOrDefault(profession => profession.Id == source.ProfessionId);
            var item = (package.Items ?? []).FirstOrDefault(item => item.Id == source.ItemId);
            var location = package.Locations.FirstOrDefault(location => location.Id == source.LocationId);
            if (string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 128
                || source.Id.Any(character => character != '_' && !char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character))
                || !ids.Add(source.Id) || string.IsNullOrWhiteSpace(source.DisplayName) || string.IsNullOrWhiteSpace(source.Description)
                || profession is null || profession.Category != ProfessionCategory.Gathering
                || item is null || item.Type != ItemType.Material || !item.Stackable
                || source.MinQuantity < 1 || source.MaxQuantity < source.MinQuantity || source.MaxQuantity > item.MaxStack
                || source.RequiredSkill < 1 || source.SkillUpUntil <= source.RequiredSkill || source.SkillUpUntil > profession.MaxSkill
                || source.RespawnSeconds < 1 || location is null || location.DangerLevel == "SAFE")
                context.Errors.Add(new ContentValidationError("RESOURCE_SOURCE_INVALID", $"resourceSources[{source.Id}]",
                    "Resource source identifiers, references, gathering thresholds, quantities or respawn are invalid."));
        }
    }
}
