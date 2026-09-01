using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class TalentModifierResolverTests
{
    [Fact]
    public void ResolveUsesSelectedRankAndSkipsDeferredHooks()
    {
        TalentTreeDefinition tree = CreateTree(
            new TalentDefinition(
                "B-1-1", "BERSERKER", 1, 0, "Боевое Безумие", "Battle Frenzy", 2, [], "",
                Modifiers:
                [
                    new(TalentModifierType.StatModifier, TalentModifierKeys.AttackPowerPercent, [2, 4]),
                    new(TalentModifierType.EventTriggered, TalentModifierKeys.OnEnemyKilled, [8, 12],
                        RuntimeStatus: TalentModifierRuntimeStatus.Deferred,
                        DeferredOwner: TalentRuntimeOwners.CombatSession)
                ]),
            new TalentDefinition(
                "B-2-2", "BERSERKER", 2, 0, "Дикий Удар", "Wild Strike", 1, [], "",
                Modifiers:
                [
                    new(TalentModifierType.AbilityModifier, TalentModifierKeys.UnlockAbility, [1], "WILD_STRIKE")
                ]));

        ResolvedTalentModifiers result = TalentModifierResolver.Resolve(
            tree,
            new Dictionary<string, int> { ["B-1-1"] = 2, ["B-2-2"] = 1 });

        Assert.Equal(4, result.Stats.AttackPowerPercent);
        Assert.Contains("WILD_STRIKE", result.UnlockedAbilityIds);
        Assert.Empty(result.DeferredHooks);
    }

    [Fact]
    public void ApplyToAbilityClampsCostAndChangesCooldownAndDamageCoefficient()
    {
        TalentTreeDefinition tree = CreateTree(
            new TalentDefinition(
                "B-4-2", "BERSERKER", 4, 0, "Мастер Вихря", "Whirlwind Mastery", 2, [], "",
                Modifiers:
                [
                    new(TalentModifierType.AbilityModifier, TalentModifierKeys.AbilityCooldownSeconds, [1, 2], "WHIRLWIND"),
                    new(TalentModifierType.AbilityModifier, TalentModifierKeys.AbilityDamagePercent, [10, 20], "WHIRLWIND"),
                    new(TalentModifierType.AbilityModifier, TalentModifierKeys.AbilityResourceCostFlat, [20, 40], "WHIRLWIND"),
                    new(TalentModifierType.AbilityModifier, TalentModifierKeys.AbilityArmorPenetrationPercent, [8, 15], "WHIRLWIND"),
                    new(TalentModifierType.AbilityModifier, TalentModifierKeys.EffectDurationSeconds, [1, 2], "WHIRLWIND")
                ]));
        ResolvedTalentModifiers modifiers = TalentModifierResolver.Resolve(
            tree, new Dictionary<string, int> { ["B-4-2"] = 2 });
        AbilityDefinition ability = new(
            "WHIRLWIND", AbilityType.Instant, AbilityTargetType.AllEnemiesInCombat,
            35, TimeSpan.FromSeconds(10), TimeSpan.Zero, true,
            GlobalCooldownCategory.Standard, false, "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(AbilityActionType.Damage, AttackPowerCoefficient: 0.7m),
                new AbilityActionDefinition(
                    AbilityActionType.ApplyEffect,
                    Effect: new Elyndor.Core.Combat.Effects.EffectDefinition(
                        "TEST_EFFECT", Elyndor.Core.Combat.Effects.EffectKind.Buff,
                        TimeSpan.FromSeconds(4), 1,
                        Elyndor.Core.Combat.Effects.EffectStackPolicy.Refresh, 0))
            ]);

        AbilityDefinition effective = TalentAbilityResolver.Apply(ability, modifiers);

        Assert.Equal(0, effective.ResourceCost);
        Assert.Equal(TimeSpan.FromSeconds(8), effective.Cooldown);
        Assert.Equal(0.84m, effective.Actions![0].AttackPowerCoefficient);
        Assert.Equal(0.15m, effective.Actions[0].ArmorPenetrationBonus);
        Assert.Equal(TimeSpan.FromSeconds(6), effective.Actions[1].Effect!.Duration);
    }

    private static TalentTreeDefinition CreateTree(params TalentDefinition[] nodes) =>
        new("WARRIOR_TREE", "WARRIOR", 59, 1,
            [new TalentBranchDefinition("BERSERKER", "Берсерк", "Урон", nodes.Length)], nodes);
}
