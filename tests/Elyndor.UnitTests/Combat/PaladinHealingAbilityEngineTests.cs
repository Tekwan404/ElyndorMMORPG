using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinHealingAbilityEngineTests
{
    [Fact]
    public void HealingActionUsesSourceSpellPowerCriticalRollAndCombatMetadata()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        CombatActorState paladin = CombatActorState.CreateDummy(
            100,
            maxResource: 100,
            stats: CombatStats.Default with
            {
                CriticalChance = 100,
                CriticalDamage = 1,
                SpellPower = 50
            });
        CombatActorState ally = CombatActorState.CreateDummy(100);
        ally.SetCurrentHp(50);
        CombatRuntimeState runtime = new(paladin);
        runtime.AddActor(ally);

        AbilityDefinition ability = new(
            "PALADIN_TEST_HEAL",
            AbilityType.Instant,
            AbilityTargetType.SingleAlly,
            ResourceCost: 10,
            Cooldown: TimeSpan.Zero,
            CastTime: TimeSpan.Zero,
            UsesGlobalCooldown: false,
            GlobalCooldownCategory.None,
            IsSpell: true,
            School: "HOLY",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Healing,
                    Amount: 10,
                    CanCrit: true,
                    SpellPowerCoefficient: 1)
            ]);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            ability,
            new AbilityIntent("heal-1", ability.Id, ally.ActorId),
            now,
            new SequenceGameRandom(0m));

        Assert.True(result.Succeeded);
        Assert.Equal(100, ally.CurrentHp);
        Assert.Equal(90, paladin.CurrentResource);

        CombatEvent healing = Assert.Single(
            result.Events,
            combatEvent => combatEvent.Type == CombatEventType.HealingApplied);
        Assert.Equal(50, healing.Amount);
        Assert.Equal(paladin.ActorId, healing.SourceActorId);
        Assert.Equal(ally.ActorId, healing.TargetActorId);
        Assert.Equal(ability.Id, healing.DefinitionId);
        Assert.Equal(now, healing.OccurredAtUtc);
    }

    [Fact]
    public void CriticalHealingAbilityRequiresInjectedGameRandom()
    {
        CombatActorState paladin = CombatActorState.CreateDummy(100, maxResource: 100);
        CombatActorState ally = CombatActorState.CreateDummy(100);
        ally.SetCurrentHp(50);
        CombatRuntimeState runtime = new(paladin);
        runtime.AddActor(ally);
        AbilityDefinition ability = HealingAbility(canCrit: true);

        Assert.Throws<InvalidOperationException>(() => AbilityEngine.Execute(
            runtime,
            ability,
            new AbilityIntent("heal-crit", ability.Id, ally.ActorId),
            DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void NonCriticalHealingAbilityDoesNotConsumeRandomness()
    {
        CombatActorState paladin = CombatActorState.CreateDummy(
            100,
            maxResource: 100,
            stats: CombatStats.Default with { CriticalChance = 100 });
        CombatActorState ally = CombatActorState.CreateDummy(100);
        ally.SetCurrentHp(50);
        CombatRuntimeState runtime = new(paladin);
        runtime.AddActor(ally);
        AbilityDefinition ability = HealingAbility(canCrit: false);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            ability,
            new AbilityIntent("heal-no-crit", ability.Id, ally.ActorId),
            DateTimeOffset.UnixEpoch);

        Assert.True(result.Succeeded);
        Assert.Equal(60, ally.CurrentHp);
    }

    private static AbilityDefinition HealingAbility(bool canCrit) =>
        new(
            "PALADIN_TEST_HEAL",
            AbilityType.Instant,
            AbilityTargetType.SingleAlly,
            ResourceCost: 0,
            Cooldown: TimeSpan.Zero,
            CastTime: TimeSpan.Zero,
            UsesGlobalCooldown: false,
            GlobalCooldownCategory.None,
            IsSpell: true,
            School: "HOLY",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Healing,
                    Amount: 10,
                    CanCrit: canCrit)
            ]);
}
