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
    public async Task DeserializeValidatedMigratesLegacyMonsterXpWithoutLosingRollbackValue()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        string canonical = GameContentPackageCodec.SerializeCanonical(package);
        JsonObject root = JsonNode.Parse(canonical)!.AsObject();
        JsonArray monsters = root["monsters"]!.AsArray();
        JsonObject rewarded = monsters
            .Select(node => node!.AsObject())
            .First(monster =>
                monster["id"]!.GetValue<string>()
                    == "ASHEN_BORDER_OBUGLENNYI_DREVEN_L21");
        JsonObject noReward = monsters
            .Select(node => node!.AsObject())
            .First(monster =>
                monster["id"]!.GetValue<string>()
                    == "ANCIENT_MINE_SPIDERLING_L16");

        rewarded["xpReward"] = 17_600;
        rewarded.Remove("legacyXpReward");
        noReward["xpReward"] = 0;
        noReward.Remove("grantsXp");

        GameContentPackage restored = GameContentPackageCodec.DeserializeValidated(
            root.ToJsonString());

        var restoredRewarded = restored.Monsters!.Single(monster =>
            monster.Id == "ASHEN_BORDER_OBUGLENNYI_DREVEN_L21");
        var restoredNoReward = restored.Monsters!.Single(monster =>
            monster.Id == "ANCIENT_MINE_SPIDERLING_L16");

        Assert.Equal(17_600, restoredRewarded.LegacyXpReward);
        Assert.True(restoredRewarded.GrantsXp);
        Assert.Equal(0, restoredNoReward.LegacyXpReward);
        Assert.False(restoredNoReward.GrantsXp);
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
