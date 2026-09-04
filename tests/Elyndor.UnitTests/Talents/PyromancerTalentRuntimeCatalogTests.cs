using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class PyromancerTalentRuntimeCatalogTests
{
    [Fact]
    public void SupportedDeferredHookResolvesTypedRuntimeParametersFromContent()
    {
        Assert.True(PyromancerTalentRuntimeCatalog.TryGetEventKey(
            "F-9-1",
            out string eventKey));
        TalentModifierDefinition modifier = new(
            TalentModifierType.EventTriggered,
            eventKey,
            [13],
            "FIRE",
            TalentModifierRuntimeStatus.Deferred,
            TalentRuntimeOwners.CombatSession,
            InternalCooldownSeconds: 9,
            SecondaryValues: [4],
            DurationSeconds: 7,
            CastTimeSeconds: 1.2m,
            ResourceCostReductionPercent: 42);
        TalentDefinition node = new(
            "F-9-1",
            "FIRE",
            1,
            0,
            "Avatar",
            "Avatar",
            1,
            [],
            "",
            Modifiers: [modifier]);
        TalentTreeDefinition tree = new(
            "MAGE_TREE",
            "MAGE",
            59,
            1,
            [new TalentBranchDefinition("FIRE", "Пламя", "Fire", 1)],
            [node]);

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(
            tree,
            new Dictionary<string, int> { [node.Id] = 1 });

        ResolvedTalentEventHook hook = Assert.Single(result.EventHooks);
        Assert.Equal(13, hook.Value);
        Assert.Equal(4, hook.SecondaryValue);
        Assert.Equal(TimeSpan.FromSeconds(7), hook.Duration);
        Assert.Equal(TimeSpan.FromSeconds(9), hook.InternalCooldown);
        Assert.Equal(1.2m, hook.CastTimeSeconds);
        Assert.Equal(42, hook.ResourceCostReductionPercent);
        Assert.Empty(result.DeferredHooks);
    }

    [Fact]
    public void FireCatalogOwnsThirtyRuntimeContracts()
    {
        Assert.Equal(30, PyromancerTalentRuntimeCatalog.SupportedTalentIds.Count);
    }
}
