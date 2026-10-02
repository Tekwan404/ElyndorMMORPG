using System.Text.Json.Serialization;
using Elyndor.Core.Monsters;
using Elyndor.Core.Quests;

namespace Elyndor.Core.Progression;

public sealed record LevelXpAnchorDefinition(
    int Level,
    long XpToNext);

public sealed record LevelProgressionDefinition
{
    [JsonConstructor]
    public LevelProgressionDefinition(
        string id,
        int maxLevel,
        IReadOnlyList<LevelXpAnchorDefinition> xpAnchors)
    {
        Id = id;
        MaxLevel = maxLevel;
        XpAnchors = xpAnchors;
    }

    public LevelProgressionDefinition(
        string id,
        int maxLevel,
        int baseXpToNext,
        decimal growthFactor)
        : this(
            id,
            maxLevel,
            BuildLegacyAnchors(maxLevel, baseXpToNext, growthFactor))
    {
    }

    public string Id { get; init; }
    public int MaxLevel { get; init; }
    public IReadOnlyList<LevelXpAnchorDefinition> XpAnchors { get; init; }

    public long XpToNext(int level)
    {
        if (level < 1 || level >= MaxLevel)
            return 0;

        LevelXpAnchorDefinition[] anchors = XpAnchors
            .OrderBy(anchor => anchor.Level)
            .ToArray();
        if (anchors.Length == 0)
            return 0;

        LevelXpAnchorDefinition? exact = anchors
            .SingleOrDefault(anchor => anchor.Level == level);
        if (exact is not null)
            return exact.XpToNext;

        LevelXpAnchorDefinition? lower = anchors
            .LastOrDefault(anchor => anchor.Level < level);
        LevelXpAnchorDefinition? upper = anchors
            .FirstOrDefault(anchor => anchor.Level > level);
        if (lower is null)
            return anchors[0].XpToNext;
        if (upper is null)
            return anchors[^1].XpToNext;

        decimal ratio = (decimal)(level - lower.Level)
            / (upper.Level - lower.Level);
        decimal value = lower.XpToNext
            + (upper.XpToNext - lower.XpToNext) * ratio;
        return decimal.ToInt64(decimal.Round(
            value,
            0,
            MidpointRounding.AwayFromZero));
    }

    private static List<LevelXpAnchorDefinition> BuildLegacyAnchors(
        int maxLevel,
        int baseXpToNext,
        decimal growthFactor)
    {
        List<LevelXpAnchorDefinition> anchors = [];
        decimal value = baseXpToNext;
        for (int level = 1; level < maxLevel; level++)
        {
            anchors.Add(new(
                level,
                decimal.ToInt64(decimal.Ceiling(value))));
            value *= growthFactor;
        }

        return anchors;
    }
}

public sealed record LevelTargetKillsAnchorDefinition(
    int Level,
    decimal TargetKills);

public sealed record MonsterRankXpMultiplierDefinition(
    MonsterRank Rank,
    decimal Multiplier);

public sealed record QuestXpShareDefinition(
    QuestType Type,
    decimal ShareOfLevel);

public sealed record LowerLevelXpPenaltyDefinition(
    int PlayerLevelsAboveMonster,
    decimal Multiplier);

public sealed record PartyXpPoolMultiplierDefinition(
    int PartySize,
    decimal PoolMultiplier);

public sealed record ProgressionBalanceProfile(
    string Id,
    int MaxBenchmarkLevel,
    IReadOnlyList<LevelTargetKillsAnchorDefinition> TargetNormalKills,
    IReadOnlyList<MonsterRankXpMultiplierDefinition> MonsterRankMultipliers,
    IReadOnlyList<QuestXpShareDefinition> QuestXpShares,
    IReadOnlyList<LowerLevelXpPenaltyDefinition> LowerLevelPenalties,
    IReadOnlyList<PartyXpPoolMultiplierDefinition> PartyPoolMultipliers,
    decimal HigherLevelBonusPerLevel,
    decimal MaxHigherLevelMultiplier,
    decimal AuditTolerancePercent = 20m);

public static class ProgressionBalanceCurve
{
    public static decimal TargetNormalKills(
        ProgressionBalanceProfile profile,
        int level)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        LevelTargetKillsAnchorDefinition[] anchors = profile.TargetNormalKills
            .OrderBy(anchor => anchor.Level)
            .ToArray();
        if (anchors.Length == 0)
            throw new InvalidOperationException(
                $"Progression balance profile '{profile.Id}' has no target-kill anchors.");

        LevelTargetKillsAnchorDefinition? exact = anchors
            .SingleOrDefault(anchor => anchor.Level == level);
        if (exact is not null)
            return exact.TargetKills;

        LevelTargetKillsAnchorDefinition? lower = anchors
            .LastOrDefault(anchor => anchor.Level < level);
        LevelTargetKillsAnchorDefinition? upper = anchors
            .FirstOrDefault(anchor => anchor.Level > level);
        if (lower is null)
            return anchors[0].TargetKills;
        if (upper is null)
            return anchors[^1].TargetKills;

        decimal ratio = (decimal)(level - lower.Level)
            / (upper.Level - lower.Level);
        return lower.TargetKills
            + (upper.TargetKills - lower.TargetKills) * ratio;
    }
}

public static class ProgressionRewardCalculator
{
    public static int ResolveBaseMonsterXp(
        MonsterDefinition monster,
        LevelProgressionDefinition progression,
        ProgressionBalanceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(profile);

        if (monster.XpReward <= 0)
            return 0;

        long xpToNext = progression.XpToNext(monster.Level);
        if (xpToNext <= 0)
            return 0;

        decimal targetKills = ProgressionBalanceCurve.TargetNormalKills(
            profile,
            monster.Level);
        if (targetKills <= 0)
            throw new InvalidOperationException(
                $"Target normal kills must be positive for level {monster.Level}.");

        MonsterRankXpMultiplierDefinition rank = profile.MonsterRankMultipliers
            .SingleOrDefault(item => item.Rank == monster.Rank)
            ?? throw new InvalidOperationException(
                $"Progression balance profile '{profile.Id}' has no XP multiplier for rank '{monster.Rank}'.");

        decimal value = xpToNext / targetKills * rank.Multiplier;
        return RoundPositive(value);
    }

    public static int ResolveMonsterXp(
        MonsterDefinition monster,
        int playerLevel,
        int eligiblePartySize,
        LevelProgressionDefinition progression,
        ProgressionBalanceProfile profile)
    {
        int baseXp = ResolveBaseMonsterXp(monster, progression, profile);
        if (baseXp == 0)
            return 0;

        decimal levelMultiplier = ResolveLevelDifferenceMultiplier(
            playerLevel,
            monster.Level,
            profile);
        decimal partyMultiplier = ResolvePersonalPartyMultiplier(
            eligiblePartySize,
            profile);
        return RoundNonNegative(baseXp * levelMultiplier * partyMultiplier);
    }

    public static int ResolveQuestXp(
        QuestDefinition quest,
        LevelProgressionDefinition progression,
        ProgressionBalanceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(profile);

        if (quest.RewardXp <= 0)
            return 0;

        QuestXpShareDefinition share = profile.QuestXpShares
            .SingleOrDefault(item => item.Type == quest.Type)
            ?? throw new InvalidOperationException(
                $"Progression balance profile '{profile.Id}' has no XP share for quest type '{quest.Type}'.");

        long xpToNext = progression.XpToNext(quest.RequiredLevel);
        return xpToNext <= 0
            ? 0
            : RoundPositive(xpToNext * share.ShareOfLevel);
    }

    public static decimal ResolveLevelDifferenceMultiplier(
        int playerLevel,
        int monsterLevel,
        ProgressionBalanceProfile profile)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(playerLevel, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(monsterLevel, 1);
        ArgumentNullException.ThrowIfNull(profile);

        int difference = monsterLevel - playerLevel;
        if (difference >= 0)
        {
            return decimal.Min(
                profile.MaxHigherLevelMultiplier,
                1m + difference * profile.HigherLevelBonusPerLevel);
        }

        int playerLevelsAbove = -difference;
        if (playerLevelsAbove <= 1)
            return 1m;

        LowerLevelXpPenaltyDefinition[] penalties = profile.LowerLevelPenalties
            .OrderBy(item => item.PlayerLevelsAboveMonster)
            .ToArray();
        LowerLevelXpPenaltyDefinition? exact = penalties
            .SingleOrDefault(item =>
                item.PlayerLevelsAboveMonster == playerLevelsAbove);
        if (exact is not null)
            return exact.Multiplier;

        LowerLevelXpPenaltyDefinition? next = penalties
            .FirstOrDefault(item =>
                item.PlayerLevelsAboveMonster > playerLevelsAbove);
        if (next is not null)
            return next.Multiplier;

        return penalties.Length == 0 ? 1m : penalties[^1].Multiplier;
    }

    public static decimal ResolvePersonalPartyMultiplier(
        int eligiblePartySize,
        ProgressionBalanceProfile profile)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(eligiblePartySize, 1);
        ArgumentNullException.ThrowIfNull(profile);

        PartyXpPoolMultiplierDefinition[] multipliers = profile.PartyPoolMultipliers
            .OrderBy(item => item.PartySize)
            .ToArray();
        if (multipliers.Length == 0)
            return 1m / eligiblePartySize;

        PartyXpPoolMultiplierDefinition selected = multipliers
            .LastOrDefault(item => item.PartySize <= eligiblePartySize)
            ?? multipliers[0];
        return selected.PoolMultiplier / eligiblePartySize;
    }

    private static int RoundPositive(decimal value) =>
        Math.Max(
            1,
            checked((int)decimal.Round(
                value,
                0,
                MidpointRounding.AwayFromZero)));

    private static int RoundNonNegative(decimal value) =>
        Math.Max(
            0,
            checked((int)decimal.Round(
                value,
                0,
                MidpointRounding.AwayFromZero)));
}
