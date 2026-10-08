using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class PaladinTalentRuntimeCatalogTests
{
    [Fact]
    public void RegisteredPaladinTalentRequiresItsExactTargetIdentifier()
    {
        TalentDefinition node = Node("P-8-1");
        TalentModifierDefinition valid = Hook("PALADIN_P_8_1");

        Assert.True(PaladinTalentRuntimeCatalog.SupportsRuntime(node, valid));
        Assert.True(PaladinTalentRuntimeCatalog.SupportsLegacyDeferred(node, valid));
        Assert.False(PaladinTalentRuntimeCatalog.SupportsRuntime(
            node, valid with { TargetId = "PALADIN_P_4_4" }));
        Assert.False(PaladinTalentRuntimeCatalog.SupportsLegacyDeferred(
            node, valid with { TargetId = "PALADIN_P_4_4" }));
    }

    [Fact]
    public void NewPaladinPrefixDoesNotImplicitlyAuthorizeUnimplementedEvents()
    {
        TalentDefinition unregistered = Node("P-8-99");
        TalentModifierDefinition forged = Hook("PALADIN_P_8_99");

        Assert.False(PaladinTalentRuntimeCatalog.SupportsRuntime(unregistered, forged));
        Assert.False(PaladinTalentRuntimeCatalog.SupportsLegacyDeferred(unregistered, forged));
        Assert.False(TalentRuntimeAvailability.IsModifierSupported(unregistered, forged));
    }

    private static TalentDefinition Node(string id) =>
        new(id, "PROTECTION", 8, 35, "Талант", "Talent", 1,
            [], "Описание");

    private static TalentModifierDefinition Hook(string id) =>
        new(TalentModifierType.EventTriggered, "ON_DAMAGE_TAKEN", [1],
            TargetId: id, RuntimeStatus: TalentModifierRuntimeStatus.Deferred);
}
