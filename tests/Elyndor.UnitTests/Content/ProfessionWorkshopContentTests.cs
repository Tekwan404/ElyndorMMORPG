using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Professions;

namespace Elyndor.UnitTests.Content;

public sealed class ProfessionWorkshopContentTests
{
    [Fact]
    public async Task MaterialGuideUsesActualEncountersAndCanonicalNames()
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var sources = ProfessionWorkshopCatalog.MaterialSources(content);
        Assert.Equal("Снятие шкур", content.Professions!.Single(profession => profession.Id == "SKINNING").Name);
        Assert.Equal("Кожевничество", content.Professions!.Single(profession => profession.Id == "LEATHERWORKING").Name);
        Assert.NotEmpty(sources);
        Assert.Contains(sources, source => source.ItemId == "ROUGH_HIDE" && source.LocationId == "WHISPERING_FOREST");
        Assert.Contains(sources, source => source.ItemId == "THICK_HIDE" && source.LocationId == "DEEP_FOREST");
        Assert.DoesNotContain(sources, source => source.LocationId == "STARTER_TOWN");
        Assert.All(sources, source => Assert.Equal(content.Items!.Single(item => item.Id == source.ItemId).Name, source.ItemName));
    }

    [Fact]
    public async Task LiveSkinningSourcesCanBeReachedStartingAtSkillOne()
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var monsters = content.Locations.SelectMany(location => location.Encounters ?? []).Select(encounter => encounter.MonsterId).ToHashSet();
        var sources = content.SkinningSources!.Where(source => monsters.Contains(source.MonsterId)).ToArray();
        int reachable = 1;
        while (true)
        {
            int next = sources.Where(source => source.RequiredSkill <= reachable).Select(source => source.SkillUpUntil).DefaultIfEmpty(reachable).Max();
            if (next <= reachable) break;
            reachable = next;
        }
        Assert.True(reachable >= 61, $"Skinning stalls at {reachable} before completing the current location ladder.");
    }

    [Theory]
    [InlineData("WHISPERING_FOREST", 1, 16)]
    [InlineData("FLOWER_MEADOW", 16, 31)]
    [InlineData("DEEP_FOREST", 31, 61)]
    public async Task LocationHasOneGatheringStep(string locationId, int start, int next)
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var monsters = content.Locations.Single(location => location.Id == locationId).Encounters!.Select(encounter => encounter.MonsterId).ToHashSet();
        var sources = content.SkinningSources!.Where(source => monsters.Contains(source.MonsterId)).ToArray();
        Assert.NotEmpty(sources);
        Assert.All(sources, source => { Assert.Equal(start, source.RequiredSkill); Assert.Equal(next, source.SkillUpUntil); });
    }
    [Theory]
    [InlineData("LIGHT_LEATHER")]
    [InlineData("THICK_LEATHER")]
    public async Task EachProcessedTierProducesOrdinaryEquipment(string materialId)
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var recipes = content.ProfessionRecipes!.Where(recipe => recipe.Ingredients.Any(i => i.ItemId == materialId))
            .ToArray();
        Assert.True(recipes.Length >= 3, $"{materialId} needs useful equipment recipes.");
        foreach (var recipe in recipes)
        {
            var output = content.Items!.Single(item => item.Id == recipe.OutputItemId);
            Assert.Equal(ItemType.Equipment, output.Type);
            Assert.True(output.Rarity is ItemRarity.Common or ItemRarity.Uncommon);
            Assert.Null(output.SetId);
            Assert.Equal("STARTER_TOWN", recipe.RequiredLocationId);
        }
    }

    private static string ContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository content not found.");
    }
}
