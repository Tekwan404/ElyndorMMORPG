using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class WarriorTalentRuntimeCatalogTests
{
    [Fact]
    public void GuardianCatalogCoversEveryDeferredGuardianEvent()
    {
        string[] expected =
        [
            "G-1-4", "G-2-1", "G-2-2", "G-2-4", "G-3-1", "G-3-2", "G-3-4",
            "G-4-1", "G-4-2", "G-4-4", "G-5-2", "G-6-1", "G-6-2", "G-6-3",
            "G-6-4", "G-7-2", "G-7-4", "G-8-1", "G-8-2", "G-8-3", "G-9-1"
        ];

        Assert.Equal(
            expected.OrderBy(id => id, StringComparer.Ordinal),
            GuardianTalentRuntimeCatalog.SupportedTalentIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void WarlordCatalogCoversEveryDeferredWarlordEvent()
    {
        string[] expected =
        [
            "W-1-1", "W-1-2", "W-1-4", "W-2-1", "W-2-2", "W-2-4", "W-3-1", "W-3-2",
            "W-3-3", "W-3-4", "W-4-1", "W-4-2", "W-4-3", "W-4-4", "W-5-1", "W-5-2",
            "W-5-3", "W-5-4", "W-6-1", "W-6-2", "W-6-3", "W-6-4", "W-7-1", "W-7-2",
            "W-7-3", "W-7-4", "W-8-1", "W-8-2", "W-8-3", "W-8-4", "W-9-1"
        ];

        Assert.Equal(
            expected.OrderBy(id => id, StringComparer.Ordinal),
            WarlordTalentRuntimeCatalog.SupportedTalentIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void GuardianDeferredHookResolvesThroughItsCatalog()
    {
        TalentDefinition node = new(
            "G-4-2", "GUARDIAN", 4, 10, "Провокация", "Taunt", 2, [], "Описание",
            Modifiers:
            [
                new(
                    TalentModifierType.EventTriggered,
                    TalentModifierKeys.OnAbilityUsed,
                    [0, 0],
                    RuntimeStatus: TalentModifierRuntimeStatus.Deferred,
                    DeferredOwner: TalentRuntimeOwners.CombatSession)
            ]);
        TalentTreeDefinition tree = new(
            "WARRIOR_TREE", "WARRIOR", 59, 1,
            [new TalentBranchDefinition("GUARDIAN", "Страж", "Защита", 1)],
            [node]);

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(
            tree,
            new Dictionary<string, int> { [node.Id] = 2 });

        Assert.Single(result.EventHooks);
        Assert.Empty(result.DeferredHooks);
    }

    [Fact]
    public void WarlordDeferredUnlockResolvesAsAnAbility()
    {
        TalentDefinition node = new(
            "W-2-1", "WARLORD", 2, 5, "Боевой клич", "Battle Cry", 1, [], "Описание",
            Modifiers:
            [
                new(
                    TalentModifierType.AbilityModifier,
                    TalentModifierKeys.UnlockAbility,
                    [1],
                    "BATTLE_CRY",
                    TalentModifierRuntimeStatus.Deferred,
                    TalentRuntimeOwners.Party)
            ]);
        TalentTreeDefinition tree = new(
            "WARRIOR_TREE", "WARRIOR", 59, 1,
            [new TalentBranchDefinition("WARLORD", "Полководец", "Команда", 1)],
            [node]);

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(
            tree,
            new Dictionary<string, int> { [node.Id] = 1 });

        Assert.Contains("BATTLE_CRY", result.UnlockedAbilityIds);
        Assert.Empty(result.DeferredHooks);
    }
}
