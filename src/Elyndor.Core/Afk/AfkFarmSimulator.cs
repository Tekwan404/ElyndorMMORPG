using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.World;

namespace Elyndor.Core.Afk;

public sealed record AfkFarmSimulationSettings(TimeSpan EncounterRecoveryDelay)
{
    public static AfkFarmSimulationSettings SafeDefault { get; } =
        new(TimeSpan.FromSeconds(1));
}

public sealed record AfkFarmLootCandidate(string MonsterId, string LootTableId);

public sealed record AfkFarmSimulationRequest(
    Guid SessionId,
    long IntervalIndex,
    AfkCharacterSnapshot Character,
    LocationDefinition Location,
    IReadOnlyDictionary<string, MonsterDefinition> MonstersById,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc,
    string ContentVersion,
    AfkFarmSimulationSettings? Settings = null);

public sealed record AfkFarmSimulationResult(
    TimeSpan SimulatedDuration,
    int EncounteredEnemies,
    int Kills,
    int FailedKills,
    decimal EstimatedIncomingDamage,
    decimal ResultingHpEstimate,
    int XpCandidate,
    int GoldCandidate,
    IReadOnlyList<AfkFarmLootCandidate> LootCandidates);

/// <summary>
/// Calculates Safe AFK intervals without creating online combat sessions or mutating durable state.
/// The Safe mode contract intentionally reports risk without applying damage, resource costs or death.
/// </summary>
public static class AfkFarmSimulator
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid SimulationPlayerId =
        new("f72f7ca5-e743-416b-b2d5-03d4b20c508b");
    private static readonly Guid SimulationMonsterId =
        new("87942036-4f6d-41a5-956b-6e3466c84c79");

    public static AfkFarmSimulationResult Simulate(AfkFarmSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Character);
        ArgumentNullException.ThrowIfNull(request.Location);
        ArgumentNullException.ThrowIfNull(request.MonstersById);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ContentVersion);
        if (request.SessionId == Guid.Empty)
            throw new ArgumentException("AFK session identifier cannot be empty.", nameof(request));
        if (request.IntervalIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        EnsureUtc(request.StartedAtUtc, nameof(request.StartedAtUtc));
        EnsureUtc(request.EndsAtUtc, nameof(request.EndsAtUtc));
        if (request.EndsAtUtc <= request.StartedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(request), "AFK interval must be positive.");

        AfkFarmSimulationSettings settings = request.Settings ?? AfkFarmSimulationSettings.SafeDefault;
        if (settings.EncounterRecoveryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(request), "Encounter recovery delay cannot be negative.");

        ClassProfile classProfile = DeserializeClassProfile(request.Character.ClassProfileJson);
        AutoAttackProfile autoAttack = classProfile.CombatAutoAttack
            ?? throw new InvalidOperationException("AFK character snapshot has no auto attack profile.");
        List<(MonsterDefinition Monster, decimal Weight)> encounters = ResolveEligibleEncounters(request);
        if (encounters.Count == 0)
            return Empty(request);

        IGameRandom random = new SeededGameRandom(CreateSeed(request));
        TimeSpan remaining = request.EndsAtUtc - request.StartedAtUtc;
        int encountered = 0;
        int kills = 0;
        int failedKills = 0;
        int xp = 0;
        int gold = 0;
        decimal estimatedIncomingDamage = 0;
        List<AfkFarmLootCandidate> loot = [];

        while (remaining > TimeSpan.Zero)
        {
            MonsterDefinition monster = SelectEncounter(encounters, random);
            FightResult fight = SimulateFight(request.Character, autoAttack, monster, remaining, random);
            encountered++;
            estimatedIncomingDamage += fight.EstimatedIncomingDamage;
            remaining -= fight.Elapsed;

            if (!fight.Killed)
            {
                failedKills++;
                break;
            }

            kills++;
            xp += monster.XpReward;
            gold += RollGold(monster, random);
            if (!string.IsNullOrWhiteSpace(monster.LootTableId))
                loot.Add(new AfkFarmLootCandidate(monster.Id, monster.LootTableId));

            TimeSpan recovery = remaining < settings.EncounterRecoveryDelay
                ? remaining
                : settings.EncounterRecoveryDelay;
            remaining -= recovery;
        }

        // Safe AFK intentionally does not consume HP or cause death; this is only a risk estimate.
        return new AfkFarmSimulationResult(
            request.EndsAtUtc - request.StartedAtUtc - remaining,
            encountered,
            kills,
            failedKills,
            decimal.Round(estimatedIncomingDamage, 0, MidpointRounding.AwayFromZero),
            request.Character.CurrentHp,
            xp,
            gold,
            loot);
    }

    private static FightResult SimulateFight(
        AfkCharacterSnapshot snapshot,
        AutoAttackProfile baseAutoAttack,
        MonsterDefinition monster,
        TimeSpan available,
        IGameRandom random)
    {
        CombatActorState player = CreatePlayer(snapshot);
        CombatActorState enemy = new(
            SimulationMonsterId, monster.MaxHp, monster.MaxHp, 0, 0, monster.Stats);
        decimal attackSpeed = Math.Max(0.1m, snapshot.Stats.AttackSpeed);
        TimeSpan playerInterval = TimeSpan.FromSeconds(
            baseAutoAttack.Interval.TotalSeconds / (double)attackSpeed);
        if (playerInterval <= TimeSpan.Zero || monster.AutoAttackInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("AFK combat attack intervals must be positive.");

        int playerAttacks = 0;
        TimeSpan elapsed = TimeSpan.Zero;
        while (!enemy.IsDead && elapsed + playerInterval <= available)
        {
            decimal baseDamage = AutoAttackDamageRoller.RollPlayerDamage(
                baseAutoAttack, snapshot.Stats.AttackPower, random);
            DamagePipeline.Resolve(
                new DamageRequest(player, enemy, baseDamage, baseAutoAttack.DamageType), random);
            playerAttacks++;
            elapsed += playerInterval;
        }

        int monsterAttacks = (int)Math.Floor(elapsed.TotalSeconds / monster.AutoAttackInterval.TotalSeconds);
        decimal incoming = 0;
        AutoAttackProfile monsterAttack = new(
            monster.AutoAttackInterval,
            monster.AutoAttackBaseDamage,
            monster.AutoAttackAttackPowerCoefficient,
            0,
            monster.AutoAttackBaseDamageMin,
            monster.AutoAttackBaseDamageMax);
        for (var index = 0; index < monsterAttacks; index++)
        {
            decimal baseDamage = AutoAttackDamageRoller.RollBaseDamage(monsterAttack, random)
                + monster.Stats.AttackPower * monsterAttack.AttackPowerCoefficient;
            DamageResult result = DamagePipeline.Resolve(
                new DamageRequest(enemy, player, baseDamage, DamageType.Physical), random);
            incoming += result.HpDamage;
        }

        return new FightResult(enemy.IsDead, elapsed == TimeSpan.Zero ? available : elapsed, incoming);
    }

    private static CombatActorState CreatePlayer(AfkCharacterSnapshot snapshot) => new(
        SimulationPlayerId,
        snapshot.Stats.MaxHp,
        snapshot.Stats.MaxHp,
        0,
        0,
        new CombatStats(
            snapshot.Level,
            snapshot.Stats.Accuracy,
            snapshot.Stats.Dodge,
            snapshot.Stats.CriticalChance,
            snapshot.Stats.CriticalDamage / 100m,
            snapshot.Stats.Armor,
            snapshot.Stats.MagicResistance,
            snapshot.Stats.ArmorPenetration / 100m,
            snapshot.Stats.MagicPenetration / 100m,
            snapshot.Stats.AttackPower,
            snapshot.Stats.SpellPower,
            snapshot.Stats.BlockChance,
            snapshot.Stats.BlockValueMin,
            snapshot.Stats.BlockValueMax),
        ResolveTalentCombatModifiers(snapshot.TalentModifiersJson),
        canDie: false);

    private static List<(MonsterDefinition Monster, decimal Weight)> ResolveEligibleEncounters(
        AfkFarmSimulationRequest request) => (request.Location.Encounters ?? [])
        .Where(encounter => encounter.Weight > 0
            && request.MonstersById.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster)
            && monster.Rank == MonsterRank.Normal)
        .Select(encounter => (request.MonstersById[encounter.MonsterId], encounter.Weight))
        .OrderBy(pair => pair.Item1.Id, StringComparer.Ordinal)
        .ToList();

    private static MonsterDefinition SelectEncounter(
        IReadOnlyList<(MonsterDefinition Monster, decimal Weight)> encounters,
        IGameRandom random)
    {
        decimal totalWeight = encounters.Sum(pair => pair.Weight);
        decimal roll = random.NextUnit() * totalWeight;
        decimal cumulative = 0;
        foreach ((MonsterDefinition monster, decimal weight) in encounters)
        {
            cumulative += weight;
            if (roll < cumulative)
                return monster;
        }

        return encounters[^1].Monster;
    }

    private static int RollGold(MonsterDefinition monster, IGameRandom random)
    {
        if (monster.GoldRewardMax < monster.GoldRewardMin)
            throw new InvalidOperationException($"Monster '{monster.Id}' has an invalid gold range.");
        if (monster.GoldRewardMin == monster.GoldRewardMax)
            return monster.GoldRewardMin;
        return monster.GoldRewardMin
            + (int)Math.Floor((monster.GoldRewardMax - monster.GoldRewardMin + 1) * random.NextUnit());
    }

    private static ClassProfile DeserializeClassProfile(string json) =>
        JsonSerializer.Deserialize<ClassProfile>(json, SnapshotJsonOptions)
        ?? throw new InvalidOperationException("AFK character snapshot has an invalid class profile.");

    private static TalentCombatModifiers ResolveTalentCombatModifiers(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("combat", out JsonElement combat))
                return new TalentCombatModifiers();
            return JsonSerializer.Deserialize<TalentCombatModifiers>(combat.GetRawText(), SnapshotJsonOptions)
                   ?? new TalentCombatModifiers();
        }
        catch (JsonException)
        {
            return new TalentCombatModifiers();
        }
    }

    private static int CreateSeed(AfkFarmSimulationRequest request)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"{request.SessionId:N}:{request.IntervalIndex}:{request.ContentVersion}");
        byte[] hash = SHA256.HashData(data);
        return BitConverter.ToInt32(hash, 0);
    }

    private static AfkFarmSimulationResult Empty(AfkFarmSimulationRequest request) => new(
        request.EndsAtUtc - request.StartedAtUtc,
        0, 0, 0, 0, request.Character.CurrentHp, 0, 0, []);

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("AFK timestamps must be UTC.", parameterName);
    }

    private sealed record FightResult(bool Killed, TimeSpan Elapsed, decimal EstimatedIncomingDamage);
}
