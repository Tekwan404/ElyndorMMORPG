using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Talents;

public sealed class MageCoreContractTests
{
    private static readonly string[] CoreAbilityIds = ["MAGE_FIREBALL", "MAGE_ARCANE_SPARK", "MAGE_ICE_SHARD", "MAGE_COUNTERSPELL"];

    [Fact]
    public async Task MageCoreGrantsSchoolFillersAndCounterspellWithoutTalentPoints()
    {
        var package = await Load();
        var mage = package.ClassProfiles!.Single(p => p.Id == "MAGE");
        var known = CharacterKnownAbilityResolver.Resolve(mage, 1, new HashSet<string>(StringComparer.Ordinal));
        Assert.All(CoreAbilityIds, id => Assert.Contains(id, known));
        var nodes = package.TalentTrees!.Single(t => t.ClassId == "MAGE").Nodes;
        foreach (string id in new[] { "F-1-1", "A-1-1", "I-1-1", "A-3-4" })
        {
            var node = nodes.Single(n => n.Id == id);
            Assert.DoesNotContain(node.Modifiers!, m => m.Key == TalentModifierKeys.UnlockAbility);
            Assert.NotEmpty(node.Modifiers!);
        }
        Assert.Equal(TimeSpan.Zero, package.Abilities!.Single(a => a.Id == "MAGE_ARCANE_SPARK").Cooldown);
    }

    [Fact]
    public async Task MissilesBlizzardAndEvocationUseFourRealChannelTicks()
    {
        var package = await Load();
        foreach (string id in new[] { "MAGE_ARCANE_MISSILES", "MAGE_BLIZZARD", "MAGE_EVOCATION" })
        {
            var ability = package.Abilities!.Single(a => a.Id == id);
            Assert.Equal(AbilityType.Channelled, ability.Type);
            Assert.Equal(TimeSpan.FromSeconds(1), ability.ChannelTickInterval);
            Assert.Equal(TimeSpan.FromSeconds(4), ability.CastTime);
        }
        Assert.Equal(10, package.Abilities!.Single(a => a.Id == "MAGE_EVOCATION").Actions![0].CasterMaxResourcePercent);
    }

    private static Task<GameContentPackage> Load()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json"))) root = root.Parent;
        return GameContentPackageLoader.LoadAsync(Path.Combine(root!.FullName, "content", "package.json"));
    }
}
