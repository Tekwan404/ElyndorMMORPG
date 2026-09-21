using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class PaladinCharacterCreationContentTests
{
    [Fact]
    public async Task PaladinIsPublishedInPublicClassRoster()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        GameContentIndexes indexes = GameContentIndexes.For(package);

        Assert.Contains(
            package.Definitions,
            definition => definition.Type == "CLASS" && definition.Id == "PALADIN");

        ClassProfile paladin = indexes.ClassesById["PALADIN"];
        Assert.Equal("PALADIN", paladin.Id);
        Assert.Equal("MANA", paladin.ResourceProfileId);
        Assert.Contains("RECRUIT_IRON_SWORD", paladin.StartingEquipmentItemIds ?? []);
    }
}
