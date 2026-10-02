using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.Quests;

namespace Elyndor.Core.Progression;

public sealed record ProgressionBalanceAuditRow(
    int Level,
    long XpToNext,
    decimal TargetNormalKills,
    int TargetNormalMonsterXp,
    decimal AverageAuthoredNormalMonsterXp,
    decimal AuthoredKillsToLevel,
    int AuthoredQuestXp,
    int TargetQuestXp,
    decimal TargetQuestSharePercent,
    decimal TargetPureCombatMinutes,
    bool WithinMonsterXpTolerance);

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
            int targetNormalXp = xpToNext <= 0 || targetKills <= 0
                ? 0
                : checked((int)decimal.Round(
                    xpToNext / targetKills,
                    0,
                    MidpointRounding.AwayFromZero));

            MonsterDefinition[] normalMonsters = (content.Monsters ?? [])
                .Where(monster =>
                    monster.Level == level
                    && monster.Rank == MonsterRank.Normal
                    && monster.XpReward > 0)
                .ToArray();
            decimal authoredAverage = normalMonsters.Length == 0
                ? 0
                : normalMonsters.Average(monster => (decimal)monster.XpReward);
            decimal authoredKills = authoredAverage <= 0
                ? 0
                : xpToNext / authoredAverage;

            QuestDefinition[] levelQuests = quests
                .Where(quest => quest.RequiredLevel == level)
                .ToArray();
            int authoredQuestXp = levelQuests.Sum(quest => quest.RewardXp);
            int targetQuestXp = levelQuests.Sum(quest =>
                ProgressionRewardCalculator.ResolveQuestXp(
                    quest,
                    progression,
                    profile));
            decimal questShare = xpToNext <= 0
                ? 0
                : targetQuestXp * 100m / xpToNext;
            decimal monsterDeltaPercent = targetNormalXp <= 0
                ? 0
                : (authoredAverage - targetNormalXp)
                    / targetNormalXp * 100m;

            rows.Add(new ProgressionBalanceAuditRow(
                level,
                xpToNext,
                targetKills,
                targetNormalXp,
                decimal.Round(authoredAverage, 1),
                decimal.Round(authoredKills, 1),
                authoredQuestXp,
                targetQuestXp,
                decimal.Round(questShare, 1),
                decimal.Round(targetKills * targetTtkSeconds / 60m, 2),
                normalMonsters.Length == 0
                || Math.Abs(monsterDeltaPercent)
                    <= profile.AuditTolerancePercent));
        }

        return rows;
    }
}
