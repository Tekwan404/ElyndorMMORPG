using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class RecipeScrollContentTests
{
    [Fact]
    public async Task EveryScrollDropsFromAnActiveEncounterWithoutReplacingOrdinaryLoot()
    {
        var package = await Load();
        var indexes = GameContentIndexes.For(package);
        var recipes = package.ProfessionRecipes!.Where(recipe => recipe.LearningScrollItemId is not null).ToArray();
        Assert.Equal(13, recipes.Length);
        Assert.Equal(9, recipes.Count(recipe => recipe.ProfessionId == "LEATHERWORKING"));
        Assert.Equal(4, recipes.Count(recipe => recipe.ProfessionId == "ALCHEMY"));
        var reachableTables = package.Locations.SelectMany(location => location.Encounters ?? [])
            .Select(encounter => indexes.MonstersById[encounter.MonsterId].LootTableId)
            .Where(id => id is not null).Distinct().Select(id => indexes.LootTablesById[id!]).ToArray();
        foreach (var recipe in recipes)
        {
            Assert.Equal(ItemType.Recipe, indexes.ItemsById[recipe.LearningScrollItemId!].Type);
            var table = reachableTables.First(table => table.Entries.Any(entry => entry.ItemId == recipe.LearningScrollItemId && entry.DropChance > 0));
            Assert.Contains(table.Entries, entry => indexes.ItemsById[entry.ItemId].Type != ItemType.Recipe);
            Assert.Contains(LootRoller.Roll(table, new SequenceGameRandom(Enumerable.Repeat(0m, 1024).ToArray())),
                roll => roll.ItemId == recipe.LearningScrollItemId && roll.Quantity == 1);
        }
    }

    [Fact]
    public async Task ValidatorRejectsAnUnmappedScrollAndInvalidRecipeOutput()
    {
        var package = await Load();
        var recipe = package.ProfessionRecipes!.First(recipe => recipe.LearningScrollItemId is not null);
        var invalid = package with { ProfessionRecipes = package.ProfessionRecipes!.Select(item => item == recipe
            ? item with { LearningScrollItemId = "MISSING_SCROLL", OutputQuantity = 0 } : item).ToArray() };
        var errors = new ContentValidationPipeline([new RecipeScrollContentValidator()]).Validate(invalid);
        Assert.Contains(errors, error => error.Code == "RECIPE_SCROLL_INVALID");
        Assert.Contains(errors, error => error.Code == "RECIPE_SCROLL_UNMAPPED");
    }

    private static Task<GameContentPackage> Load()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(path)) return GameContentPackageLoader.LoadAsync(path);
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
