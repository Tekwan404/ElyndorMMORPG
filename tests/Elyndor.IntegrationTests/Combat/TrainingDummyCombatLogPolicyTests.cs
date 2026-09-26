using Elyndor.Infrastructure.Combat;
using Elyndor.Server.Combat;

namespace Elyndor.IntegrationTests.Combat;

public sealed class TrainingDummyCombatLogPolicyTests
{
    [Fact]
    public void IsEligibleAcceptsTrainingDummy()
    {
        bool eligible = TrainingDummyCombatLogPolicy.IsEligible(
            [CombatSessionFactory.TrainingDummyId]);

        Assert.True(eligible);
    }

    [Theory]
    [InlineData("WHISPERING_FOREST_WOLF")]
    [InlineData("ARCHON_OF_THE_DEAD_STAR")]
    [InlineData("")]
    public void IsEligibleRejectsEveryNonTrainingEnemy(string definitionId)
    {
        bool eligible = TrainingDummyCombatLogPolicy.IsEligible([definitionId]);

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligibleAcceptsMixedEncounterContainingTrainingDummy()
    {
        bool eligible = TrainingDummyCombatLogPolicy.IsEligible(
            ["WHISPERING_FOREST_WOLF", CombatSessionFactory.TrainingDummyId]);

        Assert.True(eligible);
    }
}
