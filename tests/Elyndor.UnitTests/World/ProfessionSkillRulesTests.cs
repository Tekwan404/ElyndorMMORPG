using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Professions;

namespace Elyndor.UnitTests.World;

public sealed class ProfessionSkillRulesTests
{
    [Fact]
    public void ServerRandomControlsSkillIncreaseAndCompletedStepNeverRolls()
    {
        Assert.True(ProfessionSkillRules.ShouldIncrease(new SequenceGameRandom(0.99m), 1, 1, 16));
        Assert.True(ProfessionSkillRules.ShouldIncrease(new SequenceGameRandom(0m), 15, 1, 16));
        Assert.False(ProfessionSkillRules.ShouldIncrease(new SequenceGameRandom(0.99m), 15, 1, 16));
        Assert.False(ProfessionSkillRules.ShouldIncrease(new SequenceGameRandom(), 16, 1, 16));
        Assert.False(ProfessionSkillRules.ShouldIncrease(new SequenceGameRandom(), 1, 16, 31));
    }
}
