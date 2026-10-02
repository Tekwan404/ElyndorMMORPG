using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.Core.Combat.Simulation;

public sealed record CombatBalanceBenchmarkRequest(
    int MinimumLevel = 1,
    int MaximumLevel = 25,
    int Iterations = 10,
    int Seed = 1337,
    int MaxDurationSeconds = 90,
    IReadOnlyList<string>? ClassIds = null,
    IReadOnlyList<CombatSimulationGearState>? GearStates = null);

public sealed record CombatBalanceBenchmarkRow(
    string ClassId,
    int Level,
    CombatSimulationGearState GearState,
    string MonsterId,
    decimal PlayerMaxHp,
    decimal PlayerArmor,
    decimal PlayerMagicResistance,
    decimal PlayerPhysicalEhp,
    decimal PlayerDps,
    decimal EnemyDps,
    decimal P50TtkSeconds,
    decimal EstimatedTtdSeconds,
    decimal WinRatePercent,
    bool TtkWithinTarget,
    bool TtdWithinTarget);

public sealed class CombatBalanceBenchmarkRunner(GameContentPackage content)
{
    public IReadOnlyList<CombatBalanceBenchmarkRow> Run(
        CombatBalanceBenchmarkRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new CombatBalanceBenchmarkRequest();
        Validate(request);

        CombatBalanceProfile profile = content.CombatBalance
            ?? throw new InvalidOperationException("Combat balance profile is required.");
        int maximumLevel = Math.Min(request.MaximumLevel, profile.MaxBenchmarkLevel);
        string[] classIds = request.ClassIds?.ToArray()
            ?? (content.ClassProfiles ?? [])
                .Select(item => item.Id)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
        CombatSimulationGearState[] gearStates = request.GearStates?.ToArray()
            ??
            [
                CombatSimulationGearState.Weak,
                CombatSimulationGearState.Normal,
                CombatSimulationGearState.Good
            ];

        CombatSimulationRunner simulator = new(content);
        List<CombatBalanceBenchmarkRow> rows = [];
        for (int level = request.MinimumLevel; level <= maximumLevel; level++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MonsterDefinition? monster = SelectRepresentativeNormalMonster(level, profile);
            if (monster is null)
                continue;

            foreach (string classId in classIds)
            {
                foreach (CombatSimulationGearState gearState in gearStates)
                {
                    CombatSimulationResult result = simulator.Run(
                        new CombatSimulationScenario(
                            classId,
                            level,
                            monster.Id,
                            request.Iterations,
                            request.Seed + level,
                            request.MaxDurationSeconds,
                            GearState: gearState),
                        cancellationToken);

                    rows.Add(new CombatBalanceBenchmarkRow(
                        classId,
                        level,
                        gearState,
                        monster.Id,
                        result.PlayerMaxHp,
                        result.PlayerArmor,
                        result.PlayerMagicResistance,
                        result.PlayerPhysicalEhp,
                        result.AveragePlayerDps,
                        result.AverageEnemyDps,
                        result.P50DurationSeconds,
                        result.EstimatedPlayerTtdSeconds,
                        result.WinRatePercent,
                        result.Victories > 0
                        && profile.NormalTtkSeconds.Contains(result.P50DurationSeconds),
                        result.EstimatedPlayerTtdSeconds > 0
                        && profile.NormalTtdSeconds.Contains(result.EstimatedPlayerTtdSeconds)));
                }
            }
        }

        return rows;
    }

    private MonsterDefinition? SelectRepresentativeNormalMonster(
        int level,
        CombatBalanceProfile profile)
    {
        MonsterStatTarget target = MonsterStatCurve.Resolve(
            profile,
            level,
            MonsterRank.Normal,
            "STANDARD");

        return (content.Monsters ?? [])
            .Where(monster =>
                monster.Level == level
                && monster.Rank == MonsterRank.Normal
                && (content.MonsterAiProfiles ?? []).Any(ai =>
                    string.Equals(ai.Id, monster.AiProfileId, StringComparison.Ordinal)))
            .OrderBy(monster => monster.AbilityIds.Count)
            .ThenBy(monster => Distance(monster, target))
            .ThenBy(monster => monster.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static decimal Distance(MonsterDefinition monster, MonsterStatTarget target) =>
        RelativeDistance(monster.MaxHp, target.MaxHp)
        + RelativeDistance(monster.Stats.AttackPower, target.AttackPower)
        + RelativeDistance(monster.Stats.Armor, target.Armor)
        + RelativeDistance(monster.Stats.MagicResistance, target.MagicResistance)
        + RelativeDistance(ResolveAutoAttackBaseDamage(monster), target.AutoAttackBaseDamage);

    private static decimal RelativeDistance(decimal actual, decimal expected) =>
        expected <= 0
            ? Math.Abs(actual)
            : Math.Abs(actual - expected) / expected;

    private static decimal ResolveAutoAttackBaseDamage(MonsterDefinition monster)
    {
        decimal minimum = monster.AutoAttackBaseDamageMin ?? monster.AutoAttackBaseDamage;
        decimal maximum = monster.AutoAttackBaseDamageMax ?? minimum;
        return (minimum + maximum) / 2m;
    }

    private static void Validate(CombatBalanceBenchmarkRequest request)
    {
        if (request.MinimumLevel < 1
            || request.MaximumLevel < request.MinimumLevel
            || request.MaximumLevel > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Benchmark level range must be between 1 and 60.");
        }

        if (request.Iterations is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Benchmark iterations must be between 1 and 1000.");
        }

        if (request.MaxDurationSeconds is < 1 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Benchmark maximum duration must be between 1 and 180 seconds.");
        }

        if (request.GearStates?.Any(state => state == CombatSimulationGearState.None) == true)
        {
            throw new ArgumentException(
                "Balance benchmark requires Weak, Normal, or Good gear state.",
                nameof(request));
        }
    }
}
