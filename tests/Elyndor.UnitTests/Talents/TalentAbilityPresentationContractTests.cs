using Elyndor.Contracts.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class TalentAbilityPresentationContractTests
{
    [Fact]
    public void TalentNodeResponse_carries_unlocked_ability_name()
    {
        TalentNodeResponse response = new(
            "TALENT_ID",
            "BRANCH_ID",
            1,
            0,
            "Талант",
            "Talent",
            1,
            [],
            "Описание",
            null,
            null,
            "SUPPORTED",
            "ABILITY_ID",
            "Название способности");

        Assert.Equal("ABILITY_ID", response.UnlockedAbilityId);
        Assert.Equal("Название способности", response.UnlockedAbilityName);
    }
}
