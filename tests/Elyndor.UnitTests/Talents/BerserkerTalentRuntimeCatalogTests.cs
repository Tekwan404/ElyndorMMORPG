using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class BerserkerTalentRuntimeCatalogTests
{
    private static readonly string[] RuntimeTalentIds =
    [
        "B-2-1", "B-2-4", "B-3-4", "B-4-1", "B-4-4", "B-5-4",
        "B-6-2", "B-6-3", "B-6-4", "B-7-1", "B-7-2", "B-7-3",
        "B-7-4", "B-8-1", "B-8-2", "B-8-3", "B-9-1"
    ];

    [Fact]
    public void CatalogOwnsEveryLegacyBerserkerCombatSessionTalent()
    {
        TalentDefinition[] nodes = RuntimeTalentIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(CreateLegacyNode)
            .ToArray();
        TalentTreeDefinition tree = new(
            "WARRIOR_TREE",
            "WARRIOR",
            59,
            1,
            [new TalentBranchDefinition("BERSERKER", "Берсерк", "Урон", nodes.Length)],
            nodes);
        Dictionary<string, int> ranks = nodes.ToDictionary(
            node => node.Id,
            _ => 1,
            StringComparer.Ordinal);

        ResolvedTalentModifiers resolved =
            TalentModifierResolver.Resolve(tree, ranks);

        Assert.Equal(17, resolved.EventHooks.Count);
        Assert.Empty(resolved.DeferredHooks);
        Assert.All(resolved.EventHooks, hook =>
            Assert.Contains(
                hook.TalentId,
                RuntimeTalentIds));
    }

    private static TalentDefinition CreateLegacyNode(string talentId)
    {
        Assert.True(
            BerserkerTalentRuntimeCatalog.TryGetRule(
                talentId,
                out BerserkerTalentRuntimeRule rule));
        TalentModifierDefinition modifier = new(
            TalentModifierType.EventTriggered,
            rule.EventKey,
            [0],
            rule.TargetId,
            TalentModifierRuntimeStatus.Deferred,
            TalentRuntimeOwners.CombatSession);
        return new TalentDefinition(
            talentId,
            "BERSERKER",
            1,
            0,
            talentId,
            talentId,
            1,
            [],
            "",
            Modifiers: [modifier]);
    }
}
