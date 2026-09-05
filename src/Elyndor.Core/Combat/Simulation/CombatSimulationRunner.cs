using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Simulation;

public sealed record CombatSimulationScenario(
    string ClassId,
    int PlayerLevel,
    string MonsterId,
    int Iterations = 100,
    int Seed = 1337,
    int MaxDurationSeconds = 90,
    IReadOnlyList<string>? AbilityPriority = null);

public sealed record CombatSimulationDamageSource(
    string DefinitionId,
    decimal AverageDamage,
    decimal DamageSharePercent);

public sealed record CombatSimulationResult(
    string ContentVersion,
    string BalanceVersion,
    string ClassId,
    int PlayerLevel,
    string MonsterId,
    int Iterations,
    int Victories,
    int Defeats,
    int Timeouts,
    decimal WinRatePercent,
    decimal AverageDurationSeconds,
    decimal P50DurationSeconds,
    decimal P95DurationSeconds,
    decimal AveragePlayerDps,
    decimal AverageEnemyDps,
    decimal AveragePlayerRemainingHp,
    IReadOnlyList<CombatSimulationDamageSource> DamageSources);

public sealed class CombatSimulationException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class CombatSimulationRunner(GameContentPackage content)
{
    private static readonly DateTimeOffset SimulationEpoch =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ActionStep = TimeSpan.FromMilliseconds(100);

    public CombatSimulationResult Run(
        CombatSimulationScenario scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ValidateScenario(scenario);

        IReadOnlyList<ClassProfile> classProfiles = content.ClassProfiles
            ?? throw Invalid("simulation_class_profiles_missing", "Class profiles are required.");
        ClassProfile classProfile = classProfiles.SingleOrDefault(profile =>
            string.Equals(profile.Id, scenario.ClassId, StringComparison.Ordinal))
            ?? throw Invalid("simulation_class_missing", $"Class '{scenario.ClassId}' does not exist.");
        if (classProfile.CombatAutoAttack is null)
        {
            throw Invalid(
                "simulation_class_not_ready",
                $"Class '{scenario.ClassId}' has no combat auto attack profile.");
        }

        MonsterDefinition monster = (content.Monsters ?? []).SingleOrDefault(candidate =>
            string.Equals(candidate.Id, scenario.MonsterId, StringComparison.Ordinal))
            ?? throw Invalid("simulation_monster_missing", $"Monster '{scenario.MonsterId}' does not exist.");
        if (monster.Rank != MonsterRank.Normal)
        {
            throw Invalid(
                "simulation_monster_rank_unsupported",
                "Combat Simulator MVP supports Normal monsters only.");
        }

        MonsterAiProfile enemyAi = (content.MonsterAiProfiles ?? []).SingleOrDefault(profile =>
            string.Equals(profile.Id, monster.AiProfileId, StringComparison.Ordinal))
            ?? throw Invalid(
                "simulation_monster_ai_missing",
                $"Monster AI '{monster.AiProfileId}' does not exist.");

        StatFormulaProfile formula = content.StatFormula
            ?? throw Invalid("simulation_stat_formula_missing", "Stat formula is required.");
        ResourceProfile baseResource = (content.ResourceProfiles ?? []).SingleOrDefault(profile =>
            string.Equals(profile.Id, classProfile.ResourceProfileId, StringComparison.Ordinal))
            ?? throw Invalid(
                "simulation_resource_missing",
                $"Resource '{classProfile.ResourceProfileId}' does not exist.");

        CharacterStats playerStats =
            new CharacterStatCalculator(formula, classProfiles)
                .Calculate(scenario.ClassId, scenario.PlayerLevel);
        ResourceProfile resource = CharacterResourceProfileResolver.Resolve(
            baseResource,
            content.ResourceScaling,
            playerStats);

        Dictionary<string, AbilityDefinition> abilities = (content.Abilities ?? [])
            .ToDictionary(ability => ability.Id, StringComparer.Ordinal);
        string[] knownAbilityIds = ResolveKnownAbilityIds(
            classProfile,
            scenario.PlayerLevel,
            abilities);
        AbilityDefinition[] abilityPriority = ResolveAbilityPriority(
            scenario.AbilityPriority,
            knownAbilityIds,
            abilities);

        int victories = 0;
        int defeats = 0;
        int timeouts = 0;
        decimal totalPlayerDamage = 0;
        decimal totalEnemyDamage = 0;
        decimal totalDuration = 0;
        decimal totalRemainingHp = 0;
        List<decimal> durations = new(scenario.Iterations);
        Dictionary<string, decimal> damageByDefinition = new(StringComparer.Ordinal);

        for (var iteration = 0; iteration < scenario.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SimulationRun run = RunSingle(
                scenario,
                iteration,
                classProfile,
                playerStats,
                resource,
                monster,
                enemyAi,
                abilities,
                knownAbilityIds,
                abilityPriority,
                cancellationToken);

            switch (run.Status)
            {
                case CombatSessionStatus.Victory:
                    victories++;
                    break;
                case CombatSessionStatus.Defeat:
                    defeats++;
                    break;
                default:
                    timeouts++;
                    break;
            }

            durations.Add(run.DurationSeconds);
            totalDuration += run.DurationSeconds;
            totalPlayerDamage += run.PlayerDamage;
            totalEnemyDamage += run.EnemyDamage;
            totalRemainingHp += run.PlayerRemainingHp;
            foreach ((string definitionId, decimal amount) in run.DamageByDefinition)
            {
                damageByDefinition[definitionId] =
                    damageByDefinition.GetValueOrDefault(definitionId) + amount;
            }
        }

        durations.Sort();
        decimal averageDuration = totalDuration / scenario.Iterations;
        decimal averagePlayerDps = totalDuration > 0 ? totalPlayerDamage / totalDuration : 0;
        decimal averageEnemyDps = totalDuration > 0 ? totalEnemyDamage / totalDuration : 0;

        CombatSimulationDamageSource[] sources = damageByDefinition
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CombatSimulationDamageSource(
                pair.Key,
                pair.Value / scenario.Iterations,
                totalPlayerDamage > 0 ? pair.Value / totalPlayerDamage * 100m : 0))
            .ToArray();

        return new CombatSimulationResult(
            content.ContentVersion,
            content.BalanceVersion,
            scenario.ClassId,
            scenario.PlayerLevel,
            scenario.MonsterId,
            scenario.Iterations,
            victories,
            defeats,
            timeouts,
            victories * 100m / scenario.Iterations,
            averageDuration,
            Percentile(durations, 0.50m),
            Percentile(durations, 0.95m),
            averagePlayerDps,
            averageEnemyDps,
            totalRemainingHp / scenario.Iterations,
            sources);
    }

    private SimulationRun RunSingle(
        CombatSimulationScenario scenario,
        int iteration,
        ClassProfile classProfile,
        CharacterStats playerStats,
        ResourceProfile resource,
        MonsterDefinition monster,
        MonsterAiProfile enemyAi,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IReadOnlyList<string> knownAbilityIds,
        IReadOnlyList<AbilityDefinition> abilityPriority,
        CancellationToken cancellationToken)
    {
        Guid playerId = Guid.NewGuid();
        Guid enemyId = Guid.NewGuid();
        CombatActorState playerActor = new(
            playerId,
            playerStats.MaxHp,
            playerStats.MaxHp,
            resource.MaxValue,
            resource.StartValue,
            ToCombatStats(scenario.PlayerLevel, playerStats));
        CombatActorState enemyActor = new(
            enemyId,
            monster.MaxHp,
            monster.MaxHp,
            0,
            0,
            monster.Stats);

        decimal attackSpeedMultiplier = Math.Max(0.1m, playerStats.AttackSpeed);
        AutoAttackProfile classAutoAttack = classProfile.CombatAutoAttack!;
        AutoAttackProfile playerAutoAttack = classAutoAttack with
        {
            Interval = TimeSpan.FromSeconds(
                classAutoAttack.Interval.TotalSeconds / (double)attackSpeedMultiplier)
        };

        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            scenario.ClassId,
            $"SIM_{scenario.ClassId}",
            resource.Id,
            playerAutoAttack,
            new HashSet<string>(knownAbilityIds, StringComparer.Ordinal),
            resource.CombatRegenPerSecond);
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            monster.Id,
            monster.DisplayName ?? monster.Name,
            "NONE",
            new AutoAttackProfile(
                monster.AutoAttackInterval,
                monster.AutoAttackBaseDamage,
                monster.AutoAttackAttackPowerCoefficient,
                0),
            new HashSet<string>(monster.AbilityIds, StringComparer.Ordinal));

        DateTimeOffset startedAt = SimulationEpoch.AddDays(iteration);
        CombatSession session = new(
            Guid.NewGuid(),
            player,
            enemy,
            abilities,
            enemyAi,
            ResolvedTalentModifiers.Empty,
            new SeededSimulationRandom(unchecked(scenario.Seed + iteration * 7919)),
            startedAt,
            content.ContentVersion,
            content.BalanceVersion);

        session.Handle(
            new StartAutoAttackCommand($"sim:{iteration}:auto"),
            startedAt);

        DateTimeOffset now = startedAt;
        DateTimeOffset deadline = startedAt.AddSeconds(scenario.MaxDurationSeconds);
        var command = 0;

        while (session.Status == CombatSessionStatus.Active && now < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CombatSessionSnapshot snapshot = session.Snapshot();
            if (snapshot.Player.Hp <= 0 || snapshot.Enemy.Hp <= 0) break;

            foreach (AbilityDefinition ability in abilityPriority)
            {
                Guid targetId = ability.TargetType is AbilityTargetType.Self
                    or AbilityTargetType.SingleAlly
                    or AbilityTargetType.Owner
                    ? playerId
                    : enemyId;
                CombatCommandResult result = session.Handle(
                    new UseAbilityCommand(
                        $"sim:{iteration}:ability:{command++}",
                        ability.Id,
                        targetId),
                    now);
                if (result.Succeeded || session.Status != CombatSessionStatus.Active)
                    break;
            }

            now = now.Add(ActionStep);
            session.AdvanceTo(now);
        }

        bool timedOut = session.Status == CombatSessionStatus.Active;
        if (timedOut)
        {
            now = deadline;
            session.AdvanceTo(now);
        }

        CombatSessionSnapshot final = session.Snapshot();
        IReadOnlyList<CombatEvent> events = session.GetEventsAfter(0);
        decimal playerDamage = 0;
        decimal enemyDamage = 0;
        Dictionary<string, decimal> damageByDefinition = new(StringComparer.Ordinal);

        foreach (CombatEvent combatEvent in events.Where(item =>
                     item.Type == CombatEventType.DamageDealt && item.Amount > 0))
        {
            if (combatEvent.SourceActorId == playerId)
            {
                playerDamage += combatEvent.Amount;
                string id = combatEvent.DefinitionId ?? "UNKNOWN";
                damageByDefinition[id] =
                    damageByDefinition.GetValueOrDefault(id) + combatEvent.Amount;
            }
            else if (combatEvent.SourceActorId == enemyId)
            {
                enemyDamage += combatEvent.Amount;
            }
        }

        decimal duration = Math.Max(
            0.001m,
            (decimal)(final.ServerTimeUtc - startedAt).TotalSeconds);
        CombatSessionStatus status = timedOut
            ? CombatSessionStatus.Cancelled
            : final.Status;
        return new SimulationRun(
            status,
            duration,
            playerDamage,
            enemyDamage,
            final.Player.Hp,
            damageByDefinition);
    }

    private static string[] ResolveKnownAbilityIds(
        ClassProfile classProfile,
        int level,
        IReadOnlyDictionary<string, AbilityDefinition> abilities) =>
        (classProfile.StartingAbilityIds ?? [])
            .Concat((classProfile.AbilityUnlocks ?? [])
                .Where(unlock => unlock.UnlockLevel <= level)
                .Select(unlock => unlock.AbilityId))
            .Distinct(StringComparer.Ordinal)
            .Where(abilities.ContainsKey)
            .ToArray();

    private static AbilityDefinition[] ResolveAbilityPriority(
        IReadOnlyList<string>? requested,
        IReadOnlyList<string> knownAbilityIds,
        IReadOnlyDictionary<string, AbilityDefinition> abilities)
    {
        HashSet<string> known = new(knownAbilityIds, StringComparer.Ordinal);
        if (requested is { Count: > 0 })
        {
            if (requested.Count != requested.Distinct(StringComparer.Ordinal).Count()
                || requested.Any(id => !known.Contains(id)))
            {
                throw Invalid(
                    "simulation_ability_priority_invalid",
                    "Ability priority contains a duplicate or unknown ability.");
            }

            return requested.Select(id => abilities[id]).ToArray();
        }

        return knownAbilityIds
            .Select(id => abilities[id])
            .Where(ability => ability.Type != AbilityType.Taunt)
            .OrderByDescending(ability =>
                ability.TargetType == AbilityTargetType.Self
                && ability.Cooldown > TimeSpan.Zero
                && ability.Actions?.Any(action => action.Type != AbilityActionType.Damage) == true)
            .ThenByDescending(ability =>
                ability.Actions?.Any(action => action.Type == AbilityActionType.Damage) == true
                && ability.ResourceCost > 0)
            .ThenByDescending(ability => ability.Cooldown)
            .ThenByDescending(ability => ability.ResourceCost)
            .ThenBy(ability => ability.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static CombatStats ToCombatStats(int level, CharacterStats stats) => new(
        level,
        stats.Accuracy,
        stats.Dodge,
        stats.CriticalChance,
        stats.CriticalDamage / 100m,
        stats.Armor,
        stats.MagicResistance,
        stats.ArmorPenetration / 100m,
        stats.MagicPenetration / 100m,
        stats.AttackPower,
        stats.SpellPower);

    private static decimal Percentile(IReadOnlyList<decimal> sorted, decimal percentile)
    {
        if (sorted.Count == 0) return 0;
        int index = (int)Math.Ceiling((double)(percentile * sorted.Count)) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static void ValidateScenario(CombatSimulationScenario scenario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.ClassId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.MonsterId);
        if (scenario.PlayerLevel is < 1 or > 60)
            throw new ArgumentOutOfRangeException(nameof(scenario.PlayerLevel));
        if (scenario.Iterations is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(scenario.Iterations));
        if (scenario.MaxDurationSeconds is < 1 or > 180)
            throw new ArgumentOutOfRangeException(nameof(scenario.MaxDurationSeconds));
    }

    private static CombatSimulationException Invalid(string code, string message) =>
        new(code, message);

    private sealed record SimulationRun(
        CombatSessionStatus Status,
        decimal DurationSeconds,
        decimal PlayerDamage,
        decimal EnemyDamage,
        decimal PlayerRemainingHp,
        IReadOnlyDictionary<string, decimal> DamageByDefinition);

    private sealed class SeededSimulationRandom(int seed) : IGameRandom
    {
        private uint state = seed == 0 ? 0xA341316Cu : unchecked((uint)seed);

        public decimal NextUnit()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return (value & 0x00FFFFFFu) / 16777216m;
        }
    }
}
