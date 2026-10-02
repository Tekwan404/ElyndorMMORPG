using Elyndor.Core.Monsters;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateCombatBalance(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        CombatBalanceProfile? profile = package.CombatBalance;
        if (profile is null)
            return;

        if (string.IsNullOrWhiteSpace(profile.Id)
            || profile.MaxBenchmarkLevel is < 1 or > 60
            || profile.AuditTolerancePercent is <= 0 or > 100)
        {
            errors.Add(new(
                "INVALID_COMBAT_BALANCE_PROFILE",
                "combatBalance",
                "Combat balance id, max benchmark level, or audit tolerance is invalid."));
        }

        ValidateRange(profile.NormalTtkSeconds, "combatBalance.normalTtkSeconds", errors);
        ValidateRange(profile.NormalTtdSeconds, "combatBalance.normalTtdSeconds", errors);
        ValidateRange(profile.EliteTtkSeconds, "combatBalance.eliteTtkSeconds", errors);
        ValidateRange(profile.EliteTtdSeconds, "combatBalance.eliteTtdSeconds", errors);

        ValidateCurve(profile.NormalMonsterCurve.MaxHp, "combatBalance.normalMonsterCurve.maxHp", errors);
        ValidateCurve(profile.NormalMonsterCurve.AttackPower, "combatBalance.normalMonsterCurve.attackPower", errors);
        ValidateCurve(profile.NormalMonsterCurve.Armor, "combatBalance.normalMonsterCurve.armor", errors);
        ValidateCurve(profile.NormalMonsterCurve.MagicResistance, "combatBalance.normalMonsterCurve.magicResistance", errors);
        ValidateCurve(
            profile.NormalMonsterCurve.AutoAttackBaseDamage,
            "combatBalance.normalMonsterCurve.autoAttackBaseDamage",
            errors);

        HashSet<string> gearStateIds = new(StringComparer.Ordinal);
        HashSet<string> qualityProfileIds = (package.Itemization?.QualityProfiles ?? [])
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < profile.GearStates.Count; index++)
        {
            CombatGearBenchmarkDefinition state = profile.GearStates[index];
            string path = $"combatBalance.gearStates[{index}]";
            if (string.IsNullOrWhiteSpace(state.Id)
                || !gearStateIds.Add(state.Id)
                || state.ItemLevelLag < 0
                || string.IsNullOrWhiteSpace(state.QualityProfileId)
                || qualityProfileIds.Count > 0 && !qualityProfileIds.Contains(state.QualityProfileId))
            {
                errors.Add(new(
                    "INVALID_COMBAT_GEAR_BENCHMARK",
                    path,
                    $"Combat gear benchmark '{state.Id}' is invalid."));
            }
        }

        foreach (string requiredState in new[] { "WEAK", "NORMAL", "GOOD" })
        {
            if (!gearStateIds.Contains(requiredState))
            {
                errors.Add(new(
                    "MISSING_COMBAT_GEAR_BENCHMARK",
                    "combatBalance.gearStates",
                    $"Combat balance profile requires '{requiredState}' gear benchmark."));
            }
        }

        HashSet<MonsterRank> ranks = [];
        for (var index = 0; index < profile.RankMultipliers.Count; index++)
        {
            MonsterRankBalanceMultiplier multiplier = profile.RankMultipliers[index];
            string path = $"combatBalance.rankMultipliers[{index}]";
            if (!ranks.Add(multiplier.Rank) || !ValidMultipliers(
                    multiplier.HpMultiplier,
                    multiplier.AttackPowerMultiplier,
                    multiplier.ArmorMultiplier,
                    multiplier.MagicResistanceMultiplier,
                    multiplier.AutoAttackDamageMultiplier))
            {
                errors.Add(new(
                    "INVALID_MONSTER_RANK_BALANCE_MULTIPLIER",
                    path,
                    $"Monster rank multiplier '{multiplier.Rank}' is invalid."));
            }
        }

        foreach (MonsterRank requiredRank in new[] { MonsterRank.Normal, MonsterRank.Elite })
        {
            if (!ranks.Contains(requiredRank))
            {
                errors.Add(new(
                    "MISSING_MONSTER_RANK_BALANCE_MULTIPLIER",
                    "combatBalance.rankMultipliers",
                    $"Combat balance profile requires '{requiredRank}' rank multiplier."));
            }
        }

        HashSet<string> archetypeIds = new(StringComparer.Ordinal);
        for (var index = 0; index < profile.MonsterArchetypes.Count; index++)
        {
            MonsterArchetypeBalanceMultiplier multiplier = profile.MonsterArchetypes[index];
            string path = $"combatBalance.monsterArchetypes[{index}]";
            if (string.IsNullOrWhiteSpace(multiplier.Id)
                || !archetypeIds.Add(multiplier.Id)
                || !ValidMultipliers(
                    multiplier.HpMultiplier,
                    multiplier.AttackPowerMultiplier,
                    multiplier.ArmorMultiplier,
                    multiplier.MagicResistanceMultiplier,
                    multiplier.AutoAttackDamageMultiplier))
            {
                errors.Add(new(
                    "INVALID_MONSTER_ARCHETYPE_BALANCE_MULTIPLIER",
                    path,
                    $"Monster archetype multiplier '{multiplier.Id}' is invalid."));
            }
        }

        if (!archetypeIds.Contains("STANDARD"))
        {
            errors.Add(new(
                "MISSING_STANDARD_MONSTER_ARCHETYPE",
                "combatBalance.monsterArchetypes",
                "Combat balance profile requires STANDARD monster archetype."));
        }

        for (var index = 0; index < (package.Monsters?.Count ?? 0); index++)
        {
            MonsterDefinition monster = package.Monsters![index];
            if (!archetypeIds.Contains(monster.BalanceArchetypeId))
            {
                errors.Add(new(
                    "UNKNOWN_MONSTER_BALANCE_ARCHETYPE",
                    $"monsters[{index}].balanceArchetypeId",
                    $"Monster '{monster.Id}' uses unknown balance archetype '{monster.BalanceArchetypeId}'."));
            }
        }
    }

    private static void ValidateRange(
        CombatSecondsRange range,
        string path,
        List<ContentValidationError> errors)
    {
        if (range.Minimum <= 0 || range.Maximum < range.Minimum)
        {
            errors.Add(new(
                "INVALID_COMBAT_TIME_RANGE",
                path,
                "Combat time range must be positive and ordered."));
        }
    }

    private static void ValidateCurve(
        CombatCurveCoefficients curve,
        string path,
        List<ContentValidationError> errors)
    {
        if (curve.BaseValue < 0 || curve.LinearPerLevel < 0 || curve.QuadraticPerLevel < 0)
        {
            errors.Add(new(
                "INVALID_COMBAT_BALANCE_CURVE",
                path,
                "Combat balance curve coefficients cannot be negative."));
        }
    }

    private static bool ValidMultipliers(params decimal[] values) =>
        values.All(value => value > 0);
}
