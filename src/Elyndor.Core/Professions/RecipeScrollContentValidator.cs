using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.Core.Professions;

public sealed class RecipeScrollContentValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        var package = context.Package;
        var scrollIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in (package.ProfessionRecipes ?? []).Where(recipe => recipe.LearningScrollItemId is not null))
        {
            var scroll = (package.Items ?? []).FirstOrDefault(item => item.Id == recipe.LearningScrollItemId);
            var profession = (package.Professions ?? []).FirstOrDefault(item => item.Id == recipe.ProfessionId);
            if (string.IsNullOrWhiteSpace(recipe.Id) || recipe.Id.Length > 128
                || string.IsNullOrWhiteSpace(recipe.Name)
                || (package.ProfessionRecipes ?? []).Count(item => item.Id == recipe.Id) != 1
                || !scrollIds.Add(recipe.LearningScrollItemId!) || scroll is null || scroll.Type != ItemType.Recipe
                || profession is null || profession.Category != ProfessionCategory.Production
                || recipe.RequiredSkill < 1 || recipe.SkillUpUntil <= recipe.RequiredSkill || recipe.SkillUpUntil > profession.MaxSkill
                || !(package.Items ?? []).Any(item => item.Id == recipe.OutputItemId) || recipe.OutputQuantity < 1
                || !package.Locations.Any(location => location.Id == recipe.RequiredLocationId)
                || recipe.Ingredients.Count == 0 || recipe.Ingredients.Any(ingredient => ingredient.Quantity < 1
                    || !(package.Items ?? []).Any(item => item.Id == ingredient.ItemId)))
                context.Errors.Add(new("RECIPE_SCROLL_INVALID", $"professionRecipes[{recipe.Id}]", "Scroll recipe references, skill thresholds, station or quantities are invalid."));
        }
        foreach (var item in (package.Items ?? []).Where(item => item.Type == ItemType.Recipe && !scrollIds.Contains(item.Id)))
            context.Errors.Add(new("RECIPE_SCROLL_UNMAPPED", $"items[{item.Id}]", "Recipe scroll must reference exactly one authored recipe."));
    }
}
