using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class MageTalentRuntimeCatalogTests
{
    [Theory]
    [InlineData("A-2-1", TalentModifierKeys.OnAbilityUsed)]
    [InlineData("A-8-2", TalentModifierKeys.OnCriticalHit)]
    [InlineData("I-2-3", TalentModifierKeys.OnDamageTaken)]
    [InlineData("I-9-1", TalentModifierKeys.OnAbilityUsed)]
    public void ArcaneAndFrostRuntimeContractsAreOwned(
        string talentId,
        string expectedKey)
    {
        Assert.True(MageTalentRuntimeCatalog.TryGetEventKey(talentId, out string key));
        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void MageDeferredCombatHookResolvesAsExecutableEventHook()
    {
        TalentModifierDefinition modifier = new(
            TalentModifierType.EventTriggered,
            TalentModifierKeys.OnAbilityUsed,
            [10, 20],
            "ARCANE_TEST",
            TalentModifierRuntimeStatus.Deferred,
            TalentRuntimeOwners.CombatSession,
            SecondaryValues: [3, 6],
            DurationSeconds: 5);
        TalentDefinition node = new(
            "A-7-3",
            "ARCANE",
            7,
            30,
            "Резонанс Школ",
            "School Resonance",
            2,
            [],
            "test",
            Modifiers: [modifier]);
        TalentTreeDefinition tree = new(
            "MAGE_TREE",
            "MAGE",
            59,
            1,
            [new TalentBranchDefinition("ARCANE", "Тайная магия", "Mana", 1)],
            [node]);

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(
            tree,
            new Dictionary<string, int> { [node.Id] = 2 });

        ResolvedTalentEventHook hook = Assert.Single(result.EventHooks);
        Assert.Equal(20, hook.Value);
        Assert.Equal(6, hook.SecondaryValue);
        Assert.Equal(TimeSpan.FromSeconds(5), hook.Duration);
        Assert.Empty(result.DeferredHooks);
    }

    [Fact]
    public void GenericMageStatsResolveIntoDerivedTalentModifiers()
    {
        TalentDefinition node = new(
            "A-STATS",
            "ARCANE",
            1,
            0,
            "Магические основы",
            "Mage Basics",
            1,
            [],
            "test",
            Modifiers:
            [
                new(TalentModifierType.StatModifier, TalentModifierKeys.IntellectPercent, [6]),
                new(TalentModifierType.StatModifier, TalentModifierKeys.SpellPowerPercent, [12]),
                new(TalentModifierType.StatModifier, TalentModifierKeys.MagicPenetrationPercent, [9]),
                new(TalentModifierType.ResourceModifier, TalentModifierKeys.MaxResourcePercent, [15])
            ]);
        TalentTreeDefinition tree = new(
            "MAGE_TREE",
            "MAGE",
            59,
            1,
            [new TalentBranchDefinition("ARCANE", "Тайная магия", "Mana", 1)],
            [node]);

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(
            tree,
            new Dictionary<string, int> { [node.Id] = 1 });

        Assert.Equal(6, result.Stats.IntellectPercent);
        Assert.Equal(12, result.Stats.SpellPowerPercent);
        Assert.Equal(9, result.Stats.MagicPenetrationPercent);
        Assert.Equal(15, result.Stats.MaxResourcePercent);
    }
}
