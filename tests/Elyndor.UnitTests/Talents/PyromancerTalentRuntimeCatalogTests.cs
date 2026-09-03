using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class PyromancerTalentRuntimeCatalogTests
{
    [Fact]
    public void FireBranchResolvesEveryRuntimeContractWithoutDeferredHooks()
    {
        List<TalentDefinition> nodes = [];
        foreach (string talentId in PyromancerTalentRuntimeCatalog.SupportedTalentIds)
        {
            Assert.True(PyromancerTalentRuntimeCatalog.TryGetRule(
                talentId,
                out PyromancerTalentRuntimeRule rule));

            List<TalentModifierDefinition> modifiers =
            [
                new(
                    TalentModifierType.EventTriggered,
                    rule.EventKey,
                    Enumerable.Repeat(0m, rule.Values.Count).ToArray(),
                    RuntimeStatus: TalentModifierRuntimeStatus.Deferred,
                    DeferredOwner: TalentRuntimeOwners.CombatSession)
            ];
            if (talentId == "F-5-1")
            {
                modifiers.Insert(0, new TalentModifierDefinition(
                    TalentModifierType.AbilityModifier,
                    TalentModifierKeys.UnlockAbility,
                    [1],
                    "COMBUSTION"));
            }

            nodes.Add(new TalentDefinition(
                talentId,
                "FIRE",
                1,
                0,
                talentId,
                talentId,
                rule.Values.Count,
                [],
                string.Empty,
                Modifiers: modifiers));
        }

        nodes.Add(Unlock("F-3-1", "FLAME_FLASH"));
        nodes.Add(Unlock("F-4-1", "FIRE_WAVE"));
        TalentTreeDefinition tree = new(
            "MAGE_TREE",
            "MAGE",
            59,
            1,
            [new TalentBranchDefinition("FIRE", "Пламя", "Fire", 32)],
            nodes);
        Dictionary<string, int> selected = nodes.ToDictionary(
            node => node.Id,
            _ => 1,
            StringComparer.Ordinal);

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(tree, selected);

        Assert.Equal(32, nodes.Count);
        Assert.Equal(30, result.EventHooks.Count);
        Assert.Empty(result.DeferredHooks);
        Assert.Contains("FLAME_FLASH", result.UnlockedAbilityIds);
        Assert.Contains("FIRE_WAVE", result.UnlockedAbilityIds);
        Assert.Contains("COMBUSTION", result.UnlockedAbilityIds);
        Assert.Contains(result.EventHooks, hook =>
            hook.TalentId == "F-2-2" && hook.Value == 4 && hook.TargetId == "MAGE_FIREBALL");
        Assert.Contains(result.EventHooks, hook =>
            hook.TalentId == "F-6-1" && hook.Value == 3 && hook.TargetId == "MAGE_FIREBALL");
        Assert.Contains(result.EventHooks, hook =>
            hook.TalentId == "F-9-1" && hook.InternalCooldown == TimeSpan.FromSeconds(6));
    }

    private static TalentDefinition Unlock(string talentId, string abilityId) =>
        new(
            talentId,
            "FIRE",
            1,
            0,
            talentId,
            talentId,
            1,
            [],
            string.Empty,
            Modifiers:
            [
                new TalentModifierDefinition(
                    TalentModifierType.AbilityModifier,
                    TalentModifierKeys.UnlockAbility,
                    [1],
                    abilityId)
            ]);
}
