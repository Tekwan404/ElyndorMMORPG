using Elyndor.Core.Progression;
using Elyndor.Core.Quests;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Progression;

public sealed class ProgressionBalanceContentTests
{
    [Fact]
    public async Task AnchoredCurveMatchesLevelingTargets()
    {
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        LevelProgressionDefinition progression = Assert.IsType<LevelProgressionDefinition>(
            content.LevelProgression);

        Assert.Equal(400, progression.XpToNext(1));
        Assert.Equal(2300, progression.XpToNext(5));
        Assert.Equal(390_000, progression.XpToNext(20));
        Assert.Equal(720_000, progression.XpToNext(25));
        Assert.Equal(1_250_000, progression.XpToNext(30));
        Assert.Equal(3_200_000, progression.XpToNext(40));
    }

    [Fact]
    public async Task FieldXpComesFromDerivedNormalMobCurve()
    {
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        LevelProgressionDefinition progression = content.LevelProgression!;
        ProgressionBalanceProfile balance = content.ProgressionBalance!;

        var level21 = content.Monsters!.Single(monster =>
            monster.Id == "ASHEN_BORDER_OBUGLENNYI_DREVEN_L21");
        var level30 = content.Monsters!.Single(monster =>
            monster.Id == "BLACKSTONE_HIGHLANDS_CHERNOKAMENNYI_VOLK_L30");

        int target21 = ProgressionRewardCalculator.ResolveBaseMonsterXp(
            level21,
            progression,
            balance);
        int target30 = ProgressionRewardCalculator.ResolveBaseMonsterXp(
            level30,
            progression,
            balance);

        Assert.InRange(target21, 18_000, 18_200);
        Assert.True(level30.GrantsXp);
        Assert.Equal(0, level30.LegacyXpReward);
        Assert.InRange(target30, 34_600, 34_900);
    }

    [Fact]
    public async Task QuestLevelDifferenceAndPartyPoliciesAreDataDriven()
    {
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        LevelProgressionDefinition progression = content.LevelProgression!;
        ProgressionBalanceProfile balance = content.ProgressionBalance!;

        QuestDefinition firstHunt = content.Quests!.Single(quest =>
            quest.Id == "QUEST_01_FIRST_HUNT");
        QuestDefinition levelTwenty = content.Quests!.Single(quest =>
            quest.Id == "QUEST_20_BLIGHTED_ALPHA");
        var level21 = content.Monsters!.Single(monster =>
            monster.Id == "ASHEN_BORDER_OBUGLENNYI_DREVEN_L21");

        Assert.Equal(
            90,
            ProgressionRewardCalculator.ResolveQuestXp(
                firstHunt,
                progression,
                balance));
        Assert.Equal(
            87_750,
            ProgressionRewardCalculator.ResolveQuestXp(
                levelTwenty,
                progression,
                balance));

        int solo = ProgressionRewardCalculator.ResolveMonsterXp(
            level21,
            playerLevel: 21,
            eligiblePartySize: 1,
            progression,
            balance);
        int duo = ProgressionRewardCalculator.ResolveMonsterXp(
            level21,
            playerLevel: 21,
            eligiblePartySize: 2,
            progression,
            balance);
        int overleveled = ProgressionRewardCalculator.ResolveMonsterXp(
            level21,
            playerLevel: 27,
            eligiblePartySize: 1,
            progression,
            balance);

        Assert.InRange(solo, 18_000, 18_200);
        Assert.InRange(duo, 11_700, 11_850);
        Assert.Equal(0, overleveled);
    }
}
