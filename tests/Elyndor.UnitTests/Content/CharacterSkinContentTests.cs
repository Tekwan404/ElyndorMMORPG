using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class CharacterSkinContentTests
{
    [Fact]
    public async Task ExistingHumanCatalogKeepsItsDefinedFemaleSaleSkinsWithCanonicalImages()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        CharacterSkinDefinition[] skins = (package.CharacterSkins ?? []).Where(skin => skin.RaceId == "HUMAN").ToArray();

        Assert.Equal(27, skins.Length);
        Assert.All(skins, skin => Assert.Equal("FEMALE", skin.GenderId));
        Assert.Equal(4, skins.Count(skin => !skin.Purchasable));
        Assert.Equal(23, skins.Count(skin => skin.Purchasable && skin.CrystalPrice > 0));
        Assert.Equal("mage-female-fire", package.CharacterSkins!.Single(skin => skin.Id == "MAGE_FEMALE_FIRE").ImageId);
    }

    [Fact]
    public async Task UndeadCatalogHasTenAuthoredSkinsForEveryClassAndGender()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        var skins = package.CharacterSkins!.Where(skin => skin.RaceId == "UNDEAD").ToArray();
        Assert.Equal(80, skins.Length);
        foreach (string classId in new[] { "WARRIOR", "ARCHER", "MAGE", "PALADIN" })
        foreach (string genderId in new[] { "MALE", "FEMALE" })
        {
            var variants = skins.Where(skin => skin.ClassId == classId && skin.GenderId == genderId).ToArray();
            Assert.Equal(10, variants.Length);
            Assert.Single(variants, skin => !skin.Purchasable && skin.CrystalPrice == 0);
            Assert.Equal(9, variants.Count(skin => skin.Purchasable && skin.CrystalPrice > 0));
            Assert.Equal(4, variants.Count(skin => skin.Id.Contains("_COLLECTOR_", StringComparison.Ordinal) && skin.Purchasable));
        }
    }

    [Theory]
    [InlineData("../mage", "CHARACTER_SKIN_INVALID_IMAGE")]
    [InlineData("mage-female-other", "CHARACTER_SKIN_INVALID_IMAGE")]
    public async Task InvalidSkinImageIsRejected(string imageId, string expectedCode)
    {
        GameContentPackage original = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        GameContentPackage broken = original with
        {
            CharacterSkins = [original.CharacterSkins![0] with { ImageId = imageId }]
        };

        Assert.Contains(ContentValidationPipeline.Default.Validate(broken), error => error.Code == expectedCode);
    }

    [Fact]
    public void ChangingClassRemovesIncompatibleEquippedSkin()
    {
        var character = new Character(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "Mage", "MAGE", "HUMAN", "FEMALE", "MAGE", new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero));
        character.SelectSkin("MAGE_FEMALE_FIRE");

        character.ChangeClass("ARCHER");

        Assert.Null(character.ActiveSkinId);
    }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
