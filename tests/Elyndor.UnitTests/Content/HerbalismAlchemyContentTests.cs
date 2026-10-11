using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class HerbalismAlchemyContentTests
{
    [Theory]
    [InlineData(0, "FOREST_MINT")]
    [InlineData(30, "MISSING_ITEM")]
    public async Task InvalidResourceBalanceAndReferencesAreRejected(int respawn, string itemId)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "content", "package.json")))
            directory = directory.Parent;
        var package = await GameContentPackageLoader.LoadAsync(Path.Combine(directory!.FullName, "content", "package.json"));
        var source = package.ResourceSources![0] with { RespawnSeconds = respawn, ItemId = itemId };
        var errors = Elyndor.Core.Content.ContentValidationPipeline.Default.Validate(package with { ResourceSources = [source] });
        Assert.Contains(errors, error => error.Code == "RESOURCE_SOURCE_INVALID");
    }
    [Fact]
    public async Task NewPairHasLearnableDefinitionsAndRecipesForEveryActiveStep()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "content", "package.json")))
            directory = directory.Parent;
        var content = await GameContentPackageLoader.LoadAsync(Path.Combine(directory!.FullName, "content", "package.json"));
        Assert.Contains(content.Professions!, definition => definition.Id == "HERBALISM" && definition.MaxSkill == 300);
        Assert.Contains(content.Professions!, definition => definition.Id == "ALCHEMY" && definition.MaxSkill == 300);
        foreach (int skill in new[] { 1, 16, 31 })
        {
            var recipes = content.ProfessionRecipes!.Where(recipe => recipe.ProfessionId == "ALCHEMY" && recipe.RequiredSkill == skill).ToArray();
            Assert.True(recipes.Length >= 2);
            Assert.All(recipes, recipe =>
            {
                Assert.Equal("STARTER_TOWN", recipe.RequiredLocationId);
                Assert.True(recipe.SkillUpUntil > skill);
                Assert.Contains(content.Items!, item => item.Id == recipe.OutputItemId && item.ConsumableActions?.Count > 0);
                Assert.All(recipe.Ingredients, ingredient => Assert.Contains(content.Items!, item => item.Id == ingredient.ItemId));
            });
        }
    }
}
