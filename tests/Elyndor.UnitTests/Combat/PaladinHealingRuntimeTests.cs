using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinHealingRuntimeTests
{
    [Fact]
    public void DirectHealingDoesNotCritUnlessTheAbilityExplicitlyAllowsIt()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                CriticalChance = 100,
                CriticalDamage = 1,
                SpellPower = 40
            });
        CombatActorState target = CombatActorState.CreateDummy(100);
        target.SetCurrentHp(20);

        HealingResult result = HealingPipeline.Resolve(new HealingRequest(
            target,
            BaseAmount: 20,
            Source: source,
            SpellPowerCoefficient: 0.5m,
            DefinitionId: "TEST_HEAL"));

        Assert.False(result.IsCritical);
        Assert.Equal(40, result.RawAmount);
        Assert.Equal(40, result.EffectiveHealing);
        Assert.Equal(60, target.CurrentHp);
    }

    [Fact]
    public void CriticalHealingUsesServerRngSpellPowerAndTracksOverheal()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                CriticalChance = 100,
                CriticalDamage = 1,
                SpellPower = 50
            });
        CombatActorState target = CombatActorState.CreateDummy(100);
        target.SetCurrentHp(70);

        HealingResult result = HealingPipeline.Resolve(
            new HealingRequest(
                target,
                BaseAmount: 10,
                OccurredAtUtc: now,
                Source: source,
                CanCrit: true,
                SpellPowerCoefficient: 1,
                DefinitionId: "HOLY_LIGHT"),
            new SequenceGameRandom(0m));

        Assert.True(result.IsCritical);
        Assert.Equal(120, result.RawAmount);
        Assert.Equal(120, result.ModifiedAmount);
        Assert.Equal(30, result.EffectiveHealing);
        Assert.Equal(90, result.Overheal);
        Assert.Equal(100, target.CurrentHp);

        CombatEvent healingEvent = Assert.Single(result.Events);
        Assert.Equal(CombatEventType.HealingApplied, healingEvent.Type);
        Assert.Equal(source.ActorId, healingEvent.SourceActorId);
        Assert.Equal(target.ActorId, healingEvent.TargetActorId);
        Assert.Equal("HOLY_LIGHT", healingEvent.DefinitionId);
        Assert.Equal(now, healingEvent.OccurredAtUtc);
    }

    [Fact]
    public void CriticalHealingRequiresSourceAndInjectedRng()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { CriticalChance = 100 });
        CombatActorState target = CombatActorState.CreateDummy(100);
        target.SetCurrentHp(50);

        Assert.Throws<InvalidOperationException>(() => HealingPipeline.Resolve(
            new HealingRequest(target, 10, Source: source, CanCrit: true)));
        Assert.Throws<InvalidOperationException>(() => HealingPipeline.Resolve(
            new HealingRequest(target, 10, CanCrit: true),
            new SequenceGameRandom(0m)));
    }

    [Fact]
    public void ForcedCriticalHealingConsumesNoRandomRoll()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                CriticalChance = 0,
                CriticalDamage = 1
            });
        CombatActorState target = CombatActorState.CreateDummy(200);
        target.SetCurrentHp(100);

        HealingResult result = HealingPipeline.Resolve(new HealingRequest(
            target,
            BaseAmount: 25,
            Source: source,
            ForceCritical: true,
            DefinitionId: "DIVINE_FAVOR_HEAL"));

        Assert.True(result.IsCritical);
        Assert.Equal(50, result.EffectiveHealing);
    }

    [Fact]
    public void PeriodicAndCopiedHealingKeepTheirOriginMetadata()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState periodicTarget = CombatActorState.CreateDummy(100);
        CombatActorState copiedTarget = CombatActorState.CreateDummy(100);
        periodicTarget.SetCurrentHp(50);
        copiedTarget.SetCurrentHp(50);

        HealingResult periodic = HealingPipeline.Resolve(new HealingRequest(
            periodicTarget,
            BaseAmount: 10,
            OccurredAtUtc: now,
            Source: source,
            Origin: HealingOrigin.Periodic,
            DefinitionId: "AFTERGLOW"));
        HealingResult copied = HealingPipeline.Resolve(new HealingRequest(
            copiedTarget,
            BaseAmount: 10,
            OccurredAtUtc: now,
            Source: source,
            Origin: HealingOrigin.Copied,
            DefinitionId: "BEACON_OF_LIGHT"));

        Assert.Equal(HealingOrigin.Periodic, periodic.Origin);
        Assert.True(Assert.Single(periodic.Events).IsPeriodic);
        Assert.Equal(HealingOrigin.Copied, copied.Origin);
        Assert.False(Assert.Single(copied.Events).IsPeriodic);
    }
}
