using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.UnitTests.Combat;

public sealed class OutgoingHealingEffectTests
{
    [Fact]
    public void OutgoingHealingMultiplierAmplifiesSourceHealing()
    {
        DateTimeOffset now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
        CombatActorState source = CombatActorState.CreateDummy(1_000);
        CombatActorState target = CombatActorState.CreateDummy(1_000);
        target.SetCurrentHp(500);

        EffectEngine.Apply(
            source,
            source.ActorId,
            new EffectDefinition(
                "TEST_OUTGOING_HEALING",
                EffectKind.StatModifier,
                TimeSpan.FromSeconds(15),
                1,
                EffectStackPolicy.Replace,
                1.25m,
                ModifiedStat: EffectStat.OutgoingHealingMultiplier,
                ModifierMode: EffectModifierMode.Multiplicative),
            now);

        HealingResult result = HealingPipeline.Resolve(new HealingRequest(
            target,
            100,
            OccurredAtUtc: now,
            Source: source));

        Assert.Equal(125, result.ModifiedAmount);
        Assert.Equal(125, result.EffectiveHealing);
        Assert.Equal(625, result.ResultingHp);
    }
}
