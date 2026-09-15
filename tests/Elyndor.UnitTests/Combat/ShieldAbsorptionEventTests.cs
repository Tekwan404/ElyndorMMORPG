using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class ShieldAbsorptionEventTests
{
    [Fact]
    public void AbsorptionEventsPreserveEachShieldDefinitionId()
    {
        DateTimeOffset now = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(100);

        EffectEngine.Apply(
            target,
            target.ActorId,
            new EffectDefinition(
                "OLDER_SHIELD",
                EffectKind.Shield,
                TimeSpan.FromSeconds(10),
                1,
                EffectStackPolicy.Independent,
                15),
            now);
        EffectEngine.Apply(
            target,
            target.ActorId,
            new EffectDefinition(
                "NEWER_SHIELD",
                EffectKind.Shield,
                TimeSpan.FromSeconds(10),
                1,
                EffectStackPolicy.Independent,
                10),
            now.AddMilliseconds(1));

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                30,
                DamageType.Magical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom(),
            now.AddSeconds(1));

        Assert.Equal(25, result.AbsorbedByShields);
        Assert.Equal(5, result.HpDamage);

        CombatEvent[] absorptions = result.Events
            .Where(item => item.Type == CombatEventType.ShieldAbsorbed)
            .ToArray();
        Assert.Equal(2, absorptions.Length);
        Assert.Equal("NEWER_SHIELD", absorptions[0].DefinitionId);
        Assert.Equal(10, absorptions[0].Amount);
        Assert.Equal("OLDER_SHIELD", absorptions[1].DefinitionId);
        Assert.Equal(15, absorptions[1].Amount);
    }
}
