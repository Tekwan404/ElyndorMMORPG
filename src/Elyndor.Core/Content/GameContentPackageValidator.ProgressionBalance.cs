using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Core.Quests;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateProgressionBalance(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        LevelProgressionDefinition? progression = package.LevelProgression;
        if (progression is null)
            return;

        if (string.IsNullOrWhiteSpace(progression.Id)
            || progression.MaxLevel is < 2 or > 100
            || progression.XpAnchors.Count < 2)
        {
            errors.Add(new(
                "INVALID_LEVEL_PROGRESSION",
                "levelProgression",
                "Level progression requires an id, max level, and XP anchors."));
            return;
        }

        LevelXpAnchorDefinition[] xpAnchors = progression.XpAnchors
            .OrderBy(anchor => anchor.Level)
            .ToArray();
        if (xpAnchors[0].Level != 1
            || xpAnchors[^1].Level != progression.MaxLevel - 1
            || xpAnchors.Any(anchor =>
                anchor.Level < 1
                || anchor.Level >= progression.MaxLevel
                || anchor.XpToNext <= 0)
            || xpAnchors.Select(anchor => anchor.Level).Distinct().Count()
                != xpAnchors.Length
            || xpAnchors.Zip(
                xpAnchors.Skip(1),
                (left, right) => right.XpToNext > left.XpToNext)
                .Any(increasing => !increasing))
        {
            errors.Add(new(
                "INVALID_LEVEL_XP_ANCHORS",
                "levelProgression.xpAnchors",
                "XP anchors must cover level 1 through maxLevel-1, be unique, positive, and strictly increasing."));
        }

        ProgressionBalanceProfile? profile = package.ProgressionBalance;
        if (profile is null)
        {
            errors.Add(new(
                "MISSING_PROGRESSION_BALANCE",
                "progressionBalance",
                "Progression balance profile is required."));
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.Id)
            || profile.MaxBenchmarkLevel < 1
            || profile.MaxBenchmarkLevel >= progression.MaxLevel
            || profile.AuditTolerancePercent is <= 0 or > 100
            || profile.HigherLevelBonusPerLevel < 0
            || profile.MaxHigherLevelMultiplier < 1)
        {
            errors.Add(new(
                "INVALID_PROGRESSION_BALANCE",
                "progressionBalance",
                "Progression balance metadata is invalid."));
        }

        ValidateKillAnchors(profile, progression, errors);
        ValidateMonsterRankMultipliers(profile, errors);
        ValidateQuestShares(profile, errors);
        ValidateLevelPenalties(profile, errors);
        ValidatePartyMultipliers(profile, errors);
    }

    private static void ValidateKillAnchors(
        ProgressionBalanceProfile profile,
        LevelProgressionDefinition progression,
        List<ContentValidationError> errors)
    {
        LevelTargetKillsAnchorDefinition[] anchors = profile.TargetNormalKills
            .OrderBy(anchor => anchor.Level)
            .ToArray();
        if (anchors.Length < 2
            || anchors[0].Level != 1
            || anchors[^1].Level != progression.MaxLevel - 1
            || anchors.Any(anchor =>
                anchor.Level < 1
                || anchor.Level >= progression.MaxLevel
                || anchor.TargetKills <= 0)
            || anchors.Select(anchor => anchor.Level).Distinct().Count()
                != anchors.Length)
        {
            errors.Add(new(
                "INVALID_TARGET_KILL_ANCHORS",
                "progressionBalance.targetNormalKills",
                "Target-kill anchors must cover level 1 through maxLevel-1 and remain positive."));
        }
    }

    private static void ValidateMonsterRankMultipliers(
        ProgressionBalanceProfile profile,
        List<ContentValidationError> errors)
    {
        MonsterRank[] required =
        [
            MonsterRank.Normal,
            MonsterRank.Elite,
            MonsterRank.Boss
        ];
        if (profile.MonsterRankMultipliers.Any(item => item.Multiplier <= 0)
            || profile.MonsterRankMultipliers
                .Select(item => item.Rank)
                .Distinct()
                .Count() != profile.MonsterRankMultipliers.Count
            || required.Any(rank =>
                !profile.MonsterRankMultipliers.Any(item => item.Rank == rank)))
        {
            errors.Add(new(
                "INVALID_MONSTER_XP_MULTIPLIERS",
                "progressionBalance.monsterRankMultipliers",
                "Normal, Elite, and Boss XP multipliers must be unique and positive."));
        }
    }

    private static void ValidateQuestShares(
        ProgressionBalanceProfile profile,
        List<ContentValidationError> errors)
    {
        QuestType[] required =
        [
            QuestType.Story,
            QuestType.Side,
            QuestType.Contract
        ];
        if (profile.QuestXpShares.Any(item =>
                item.ShareOfLevel <= 0 || item.ShareOfLevel >= 1)
            || profile.QuestXpShares
                .Select(item => item.Type)
                .Distinct()
                .Count() != profile.QuestXpShares.Count
            || required.Any(type =>
                !profile.QuestXpShares.Any(item => item.Type == type)))
        {
            errors.Add(new(
                "INVALID_QUEST_XP_SHARES",
                "progressionBalance.questXpShares",
                "Story, Side, and Contract XP shares must be unique fractions between zero and one."));
        }
    }

    private static void ValidateLevelPenalties(
        ProgressionBalanceProfile profile,
        List<ContentValidationError> errors)
    {
        LowerLevelXpPenaltyDefinition[] penalties = profile.LowerLevelPenalties
            .OrderBy(item => item.PlayerLevelsAboveMonster)
            .ToArray();
        if (penalties.Length == 0
            || penalties.Any(item =>
                item.PlayerLevelsAboveMonster < 2
                || item.Multiplier is < 0 or > 1)
            || penalties.Select(item => item.PlayerLevelsAboveMonster)
                .Distinct()
                .Count() != penalties.Length
            || penalties.Zip(
                penalties.Skip(1),
                (left, right) => right.Multiplier <= left.Multiplier)
                .Any(nonIncreasing => !nonIncreasing))
        {
            errors.Add(new(
                "INVALID_LEVEL_DIFFERENCE_XP",
                "progressionBalance.lowerLevelPenalties",
                "Lower-level XP penalties must start at a two-level gap and decrease monotonically."));
        }
    }

    private static void ValidatePartyMultipliers(
        ProgressionBalanceProfile profile,
        List<ContentValidationError> errors)
    {
        PartyXpPoolMultiplierDefinition[] parties = profile.PartyPoolMultipliers
            .OrderBy(item => item.PartySize)
            .ToArray();
        if (parties.Length == 0
            || parties[0].PartySize != 1
            || parties[0].PoolMultiplier != 1
            || parties.Any(item =>
                item.PartySize < 1 || item.PoolMultiplier <= 0)
            || parties.Select(item => item.PartySize).Distinct().Count()
                != parties.Length)
        {
            errors.Add(new(
                "INVALID_PARTY_XP_MULTIPLIERS",
                "progressionBalance.partyPoolMultipliers",
                "Party XP pool multipliers must start at 1 player = 1.0 and be unique and positive."));
        }
    }
}
