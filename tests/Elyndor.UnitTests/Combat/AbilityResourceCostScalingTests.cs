using Elyndor.Core.Combat.Abilities;

namespace Elyndor.UnitTests.Combat;

public sealed class AbilityResourceCostScalingTests
{
    [Theory]
    [InlineData(1, 20)]
    [InlineData(6, 30)]
    [InlineData(11, 40)]
    [InlineData(60, 80)]
    public void InterpolatesAuthoredCostsAndClampsAtCurveEndpoints(int level, int expected)
    {
        var ability = new AbilityDefinition("TEST", AbilityType.Instant, AbilityTargetType.Self,
            20, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "ARCANE",
            ResourceCostByLevel: [new(1, 20), new(11, 40), new(60, 80)]);
        Assert.Equal(expected, AbilityResourceCostScaling.Apply(ability, level).ResourceCost);
        Assert.Equal(20, ability.ResourceCost);
    }

    [Fact]
    public void RejectsUnsortedDuplicateOrNegativeCostPoints()
    {
        Assert.False(AbilityResourceCostScaling.IsValid([new(10, 20), new(1, 10)]));
        Assert.False(AbilityResourceCostScaling.IsValid([new(1, 20), new(1, 30)]));
        Assert.False(AbilityResourceCostScaling.IsValid([new(1, -1)]));
        Assert.False(AbilityResourceCostScaling.IsValid([]));
    }
}
