using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.UnitTests.Combat;

public sealed class PveDispelActionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DispelActionRemovesOnlyRequestedCategoryFromSelectedTarget()
    {
        CombatActorState caster = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(100);
        CombatRuntimeState runtime = new(caster);
        runtime.AddActor(target);

        EffectEngine.Apply(
            target,
            target.ActorId,
            new EffectDefinition(
                "TEST_POISON",
                EffectKind.Debuff,
                TimeSpan.FromSeconds(20),
                1,
                EffectStackPolicy.Replace,
                1,
                DispelCategory: "POISON"),
            Now);
        EffectEngine.Apply(
            target,
            target.ActorId,
            new EffectDefinition(
                "TEST_CURSE",
                EffectKind.Debuff,
                TimeSpan.FromSeconds(20),
                1,
                EffectStackPolicy.Replace,
                1,
                DispelCategory: "CURSE"),
            Now);

        AbilityDefinition cleanse = new(
            "TEST_CLEANSE",
            AbilityType.Instant,
            AbilityTargetType.SingleAlly,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "HOLY",
            AllowSelfTarget: true,
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Dispel,
                    DispelCategory: "POISON")
            ]);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            cleanse,
            new AbilityIntent("cleanse-poison", cleanse.Id, target.ActorId),
            Now);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(target.ActiveEffects, effect => effect.Definition.Id == "TEST_POISON");
        Assert.Contains(target.ActiveEffects, effect => effect.Definition.Id == "TEST_CURSE");
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.EffectRemoved
            && item.DefinitionId == "TEST_POISON"
            && item.TargetActorId == target.ActorId);
    }

    [Fact]
    public void DispelActionRequiresCategory()
    {
        CombatRuntimeState runtime = new(CombatActorState.CreateDummy(100));
        AbilityDefinition invalid = new(
            "INVALID_DISPEL",
            AbilityType.Instant,
            AbilityTargetType.Self,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "HOLY",
            Actions: [new AbilityActionDefinition(AbilityActionType.Dispel)]);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            AbilityEngine.Execute(
                runtime,
                invalid,
                new AbilityIntent("invalid-dispel", invalid.Id, runtime.Actor.ActorId),
                Now));

        Assert.Contains("dispel category", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}