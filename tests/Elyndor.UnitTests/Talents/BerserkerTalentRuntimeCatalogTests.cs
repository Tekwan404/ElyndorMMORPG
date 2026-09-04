using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class BerserkerTalentRuntimeCatalogTests
{
    [Fact]
    public void SupportedDeferredHookUsesModifierPayloadInsteadOfCatalogNumbers()
    {
        Assert.True(BerserkerTalentRuntimeCatalog.TryGetEventKey(
            "B-4-1",
            out string eventKey));
        TalentModifierDefinition modifier = new(
            TalentModifierType.EventTriggered,
            eventKey,
            [91],
            RuntimeStatus: TalentModifierRuntimeStatus.Deferred,
            DeferredOwner: TalentRuntimeOwners.CombatSession,
            InternalCooldownSeconds: 7,
            ChancePercent: 37);
        TalentDefinition node = Node("B-4-1", modifier);

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(
            Tree(node),
            new Dictionary<string, int> { [node.Id] = 1 });

        ResolvedTalentEventHook hook = Assert.Single(result.EventHooks);
        Assert.Equal(91, hook.Value);
        Assert.Equal(37, hook.ChancePercent);
        Assert.Equal(TimeSpan.FromSeconds(7), hook.InternalCooldown);
        Assert.Empty(result.DeferredHooks);
    }

    [Fact]
    public void CatalogOwnsAllExpectedBerserkerRuntimeContracts()
    {
        string[] expected =
        [
            "B-2-1", "B-2-4", "B-3-4", "B-4-1", "B-4-4", "B-5-4",
            "B-6-2", "B-6-3", "B-6-4", "B-7-1", "B-7-2", "B-7-3",
            "B-7-4", "B-8-1", "B-8-2", "B-8-3", "B-9-1"
        ];

        Assert.Equal(
            expected.OrderBy(id => id, StringComparer.Ordinal),
            BerserkerTalentRuntimeCatalog.SupportedTalentIds
                .OrderBy(id => id, StringComparer.Ordinal));
    }

    private static TalentDefinition Node(
        string id,
        TalentModifierDefinition modifier) =>
        new(id, "BERSERKER", 1, 0, id, id, 1, [], "", Modifiers: [modifier]);

    private static TalentTreeDefinition Tree(TalentDefinition node) =>
        new(
            "WARRIOR_TREE",
            "WARRIOR",
            59,
            1,
            [new TalentBranchDefinition("BERSERKER", "Берсерк", "Урон", 1)],
            [node]);
}
