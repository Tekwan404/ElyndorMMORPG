using System.Text.Json.Nodes;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class GameContentPackageCodecLegacyBalanceTests
{
    [Fact]
    public async Task DeserializeValidatedAcceptsLegacyDeadArmorFormulaFields()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        string canonical = GameContentPackageCodec.SerializeCanonical(package);
        JsonObject root = JsonNode.Parse(canonical)!.AsObject();
        JsonObject formula = root["statFormula"]!.AsObject();
        formula["armorPerStamina"] = 2;
        formula["armorPerStrength"] = 1;

        GameContentPackage restored = GameContentPackageCodec.DeserializeValidated(
            root.ToJsonString());

        Assert.Equal(package.ContentVersion, restored.ContentVersion);
        Assert.Equal(package.BalanceVersion, restored.BalanceVersion);
        Assert.Equal(package.StatFormula, restored.StatFormula);
    }

    [Fact]
    public async Task DeserializeValidatedMigratesLegacyExponentialLevelProgression()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        string canonical = GameContentPackageCodec.SerializeCanonical(package);
        JsonObject root = JsonNode.Parse(canonical)!.AsObject();
        root["levelProgression"] = new JsonObject
        {
            ["id"] = "DEFAULT_LEVELING",
            ["maxLevel"] = 60,
            ["baseXpToNext"] = 100,
            ["growthFactor"] = 1.5m
        };
        root.Remove("progressionBalance");

        GameContentPackage restored = GameContentPackageCodec.DeserializeValidated(
            root.ToJsonString());

        Assert.NotNull(restored.LevelProgression);
        Assert.Equal(100, restored.LevelProgression!.XpToNext(1));
        Assert.Equal(150, restored.LevelProgression.XpToNext(2));
        Assert.Equal(225, restored.LevelProgression.XpToNext(3));
        Assert.Null(restored.ProgressionBalance);
    }

}
