using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.Quests;

namespace Elyndor.Core.Progression;

public sealed record ProgressionBalanceAuditRow(
    int Level,
    long XpToNext,
    decimal TargetNormalKills,
    int NormalMonsterXp,
    int RewardableNormalMonsterCount,
    int TargetQuestXp,
    decimal TargetQuestSharePercent,
    decimal TargetPureCombatMinutes,
    bool HasRewardableNormalMonster);

public static class ProgressionBalanceAudit
{
    public static IReadOnlyList<ProgressionBalanceAuditRow> Run(
        GameContentPackage content)
    {
        ArgumentNullException.ThrowIfNull(content);
        LevelProgressionDefinition progression = content.LevelProgression
            ?? throw new InvalidOperationException(
                "Level progression content is required.");
        ProgressionBalanceProfile profile = content.ProgressionBalance
            ?? throw new InvalidOperationException(
                "Progression balance profile is required.");

        decimal targetTtkSeconds = content.CombatBalance is null
            ? 10m
            : (content.CombatBalance.NormalTtkSeconds.Minimum
                + content.CombatBalance.NormalTtkSeconds.Maximum) / 2m;
        IReadOnlyList<QuestDefinition> quests = content.Quests ?? [];

        List<ProgressionBalanceAuditRow> rows = [];
        int maximum = Math.Min(
            Math.Min(profile.MaxBenchmarkLevel, progression.MaxLevel - 1),
            60);
        for (int level = 1; level <= maximum; level++)
        {
            long xpToNext = progression.XpToNext(level);
            decimal targetKills = ProgressionBalanceCurve.TargetNormalKills(
                profile,
                level);
            int normalMonsterXp = xpToNext <= 0 || targetKills <= 0
                ? 0
                : checked((int)decimal.Round(
                    xpToNext / targetKills,
                    0,
                    MidpointRounding.AwayFromZero));

            int rewardableNormals = (content.Monsters ?? [])
                .Count(monster =>
                    monster.Level == level
                    && monster.Rank == MonsterRank.Normal
                    && monster.GrantsXp);

            QuestDefinition[] levelQuests = quests
                .Where(quest => quest.RequiredLevel == level)
                .ToArray();
            int targetQuestXp = levelQuests.Sum(quest =>
                ProgressionRewardCalculator.ResolveQuestXp(
                    quest,
                    progression,
                    profile));
            decimal questShare = xpToNext <= 0
                ? 0
                : targetQuestXp * 100m / xpToNext;

            rows.Add(new ProgressionBalanceAuditRow(
                level,
                xpToNext,
                targetKills,
                normalMonsterXp,
                rewardableNormals,
                targetQuestXp,
                decimal.Round(questShare, 1),
                decimal.Round(targetKills * targetTtkSeconds / 60m, 2),
                rewardableNormals > 0));
        }

        return rows;
    }
}
