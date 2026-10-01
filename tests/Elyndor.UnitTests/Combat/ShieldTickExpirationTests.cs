using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class ShieldTickExpirationTests
{
    [Theory]
    [InlineData(1, 10, 20)]
    [InlineData(2, 0, 0)]
    public void PeriodicTickUsesShieldOnlyBeforeExpirationAndPaysOnlyForAbsorbedDamage(
        int shieldDurationSeconds, int expectedHpDamage, int expectedResource)
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var source = new CombatActorState(Guid.NewGuid(), 100, 100, 100, 100, CombatStats.Default);
        var target = new CombatActorState(Guid.NewGuid(), 100, 100, 100, 20, CombatStats.Default);
        var random = new SeededGameRandom(1);
        EffectEngine.Apply(target, target.ActorId, new EffectDefinition(
            "RESOURCE_SHIELD", EffectKind.Shield, TimeSpan.FromSeconds(shieldDurationSeconds),
            1, EffectStackPolicy.Replace, 100, ResourceCostPerAbsorbedDamage: 2), now);
        EffectEngine.Apply(target, source.ActorId, new EffectDefinition(
            "PERIODIC_DAMAGE", EffectKind.DamageOverTime, TimeSpan.FromSeconds(3),
            1, EffectStackPolicy.Replace, 10, TimeSpan.FromSeconds(1),
            PeriodicDamageType: DamageType.True), now);

        DateTimeOffset tickAt = now.AddSeconds(1);
        IReadOnlyList<CombatEvent> events = EffectEngine.Process(target, tickAt,
            (effect, tick) => DamagePipeline.Resolve(new DamageRequest(
                source, target, effect.Definition.Magnitude, effect.Definition.PeriodicDamageType,
                CanMiss: false, CanDodge: false, CanCrit: false, CanBlock: false), random, tick).Events);

        CombatEvent damage = Assert.Single(events, e => e.Type == CombatEventType.DamageDealt);
        Assert.Equal(tickAt, damage.OccurredAtUtc);
        Assert.True(damage.IsPeriodic);
        Assert.Equal(expectedHpDamage, damage.Amount);
        Assert.Equal(100m - expectedHpDamage, target.CurrentHp);
        Assert.Equal(expectedResource, target.CurrentResource);
        Assert.Single(events, e => e.Type == CombatEventType.EffectTicked);

        if (shieldDurationSeconds == 1)
        {
            Assert.DoesNotContain(events, e => e.Type == CombatEventType.ShieldAbsorbed);
            Assert.DoesNotContain(events, e => e.Type == CombatEventType.ResourceChanged);
            Assert.Single(events, e => e.Type == CombatEventType.EffectExpired
                && e.DefinitionId == "RESOURCE_SHIELD" && e.OccurredAtUtc == tickAt);
            Assert.DoesNotContain(target.ActiveEffects, e => e.Definition.Id == "RESOURCE_SHIELD");
        }
        else
        {
            Assert.Equal(10m, Assert.Single(events, e => e.Type == CombatEventType.ShieldAbsorbed).Amount);
            Assert.Equal(-20m, Assert.Single(events, e => e.Type == CombatEventType.ResourceChanged).Amount);
            Assert.Contains(target.ActiveEffects, e => e.Definition.Id == "RESOURCE_SHIELD");
        }

        Assert.Empty(EffectEngine.Process(target, tickAt));
    }
}
