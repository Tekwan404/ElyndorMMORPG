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
}
