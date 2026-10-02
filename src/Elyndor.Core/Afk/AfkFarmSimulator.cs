using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
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
    public static AfkFarmSimulationSettings Default { get; } =
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
    string? TargetMonsterId = null,
    AfkFarmSimulationSettings? Settings = null,
    IReadOnlyDictionary<string, AbilityDefinition>? AbilitiesById = null);

public sealed record AfkFarmSimulationResult(
    TimeSpan SimulatedDuration,
    int EncounteredEnemies,
    int Kills,
    int FailedKills,
    decimal EstimatedIncomingDamage,
    decimal ResultingHpEstimate,
    int EfficiencyPercent,
    int XpCandidate,
    int GoldCandidate,
    IReadOnlyList<AfkFarmLootCandidate> LootCandidates);

/// <summary>
/// Calculates AFK intervals without creating online combat sessions or mutating durable state.
/// Incoming damage informs combat timing only; AFK never spends HP or causes death.
/// Direct instant single-target abilities are resolved through the shared ability/damage pipeline;
/// casted abilities and stateful class proc runtimes remain intentionally outside the lightweight AFK model.
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

        AfkFarmSimulationSettings settings = request.Settings ?? AfkFarmSimulationSettings.Default;
        if (settings.EncounterRecoveryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(request), "AFK encounter recovery delay cannot be negative.");

        ClassProfile classProfile = DeserializeClassProfile(request.Character.ClassProfileJson);
        AutoAttackProfile autoAttack = classProfile.CombatAutoAttack
            ?? throw new InvalidOperationException("AFK character snapshot has no auto attack profile.");
        ResolvedTalentModifiers talents = ResolveTalentModifiers(request.Character.TalentModifiersJson);
        AbilityDefinition[] farmAbilities = ResolveFarmAbilities(
            request.Character,
            request.AbilitiesById,
            talents);
        ResourceProfile? resourceProfile = farmAbilities.Length == 0
            ? null
            : DeserializeResourceProfile(request.Character.ResourceProfileJson);
        List<(MonsterDefinition Monster, decimal Weight)> encounters = ResolveEligibleEncounters(request);
        if (encounters.Count == 0)
            return Empty(request);

        IGameRandom random = new SeededGameRandom(CreateSeed(request));
        CombatActorState player = CreatePlayer(request.Character, talents.Combat, resourceProfile);
        CombatRuntimeState? runtime = farmAbilities.Length == 0
            ? null
            : new CombatRuntimeState(player);
        TimeSpan remaining = request.EndsAtUtc - request.StartedAtUtc;
        DateTimeOffset simulatedNow = request.StartedAtUtc;
        int encountered = 0;
        int kills = 0;
        int failedKills = 0;
        int xp = 0;
        int gold = 0;
        int abilityCommandSequence = 0;
        decimal estimatedIncomingDamage = 0;
        TimeSpan successfulKillTime = TimeSpan.Zero;
        List<AfkFarmLootCandidate> loot = [];

        while (remaining > TimeSpan.Zero)
        {
            MonsterDefinition monster = SelectEncounter(encounters, random);
            FightResult fight = SimulateFight(
                request.Character,
                player,
                runtime,
                resourceProfile,
                autoAttack,
                farmAbilities,
                monster,
                remaining,
                simulatedNow,
                random,
                ref abilityCommandSequence);
            encountered++;
            estimatedIncomingDamage += fight.EstimatedIncomingDamage;
            remaining -= fight.Elapsed;
            simulatedNow += fight.Elapsed;

            if (!fight.Killed)
            {
                failedKills++;
                continue;
            }

            kills++;
            successfulKillTime += fight.Elapsed;
            xp += monster.XpReward;
            gold += RollGold(monster, random);
            if (!string.IsNullOrWhiteSpace(monster.LootTableId))
                loot.Add(new AfkFarmLootCandidate(monster.Id, monster.LootTableId));

            TimeSpan recovery = remaining < settings.EncounterRecoveryDelay
                ? remaining
                : settings.EncounterRecoveryDelay;
            if (resourceProfile is not null && recovery > TimeSpan.Zero)
            {
                decimal recovered = CharacterResourceRules.ApplyElapsed(
                    resourceProfile,
                    player.CurrentResource,
                    recovery,
                    isInCombat: false,
                    recovery);
                player.ConfigureResource(resourceProfile.MaxValue, recovered);
            }
            remaining -= recovery;
            simulatedNow += recovery;
        }

        TimeSpan baselineKillTime = TimeSpan.FromSeconds(
            autoAttack.Interval.TotalSeconds / (double)Math.Max(0.1m, request.Character.Stats.AttackSpeed));
        int efficiency = kills == 0 || successfulKillTime <= TimeSpan.Zero
            ? 0
            : Math.Clamp(
                (int)Math.Floor(kills * baselineKillTime.TotalMilliseconds / successfulKillTime.TotalMilliseconds * 100d),
                0,
                100);

        // AFK intentionally does not consume HP or cause death; this is a combat-efficiency estimate only.
        return new AfkFarmSimulationResult(
            request.EndsAtUtc - request.StartedAtUtc - remaining,
            encountered,
            kills,
            failedKills,
            decimal.Round(estimatedIncomingDamage, 0, MidpointRounding.AwayFromZero),
            request.Character.CurrentHp,
            efficiency,
            xp,
            gold,
            loot);
    }

    private static FightResult SimulateFight(
        AfkCharacterSnapshot snapshot,
        CombatActorState player,
        CombatRuntimeState? runtime,
        ResourceProfile? resourceProfile,
        AutoAttackProfile baseAutoAttack,
        AbilityDefinition[] farmAbilities,
        MonsterDefinition monster,
        TimeSpan available,
        DateTimeOffset fightStartedAtUtc,
        IGameRandom random,
        ref int abilityCommandSequence)
    {
        CombatActorState enemy = new(
            SimulationMonsterId, monster.MaxHp, monster.MaxHp, 0, 0, monster.Stats);
        if (runtime is not null)
            runtime.Actors[SimulationMonsterId] = enemy;

        decimal attackSpeed = Math.Max(0.1m, snapshot.Stats.AttackSpeed);
        TimeSpan playerInterval = TimeSpan.FromSeconds(
            baseAutoAttack.Interval.TotalSeconds / (double)attackSpeed);
        if (playerInterval <= TimeSpan.Zero || monster.AutoAttackInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("AFK combat attack intervals must be positive.");

        TimeSpan elapsed = TimeSpan.Zero;
        while (!enemy.IsDead && elapsed + playerInterval <= available)
        {
            elapsed += playerInterval;
            DateTimeOffset actionAtUtc = fightStartedAtUtc + elapsed;
            if (resourceProfile is not null && resourceProfile.CombatRegenPerSecond > 0)
            {
                player.AddResource(
                    resourceProfile.CombatRegenPerSecond * (decimal)playerInterval.TotalSeconds);
            }

            if (runtime is not null && farmAbilities.Length > 0)
            {
                TryUseFarmAbility(
                    runtime,
                    farmAbilities,
                    enemy.ActorId,
                    actionAtUtc,
                    random,
                    ref abilityCommandSequence);
            }
            if (enemy.IsDead)
                break;

            decimal baseDamage = AutoAttackDamageRoller.RollPlayerDamage(
                baseAutoAttack, snapshot.Stats.AttackPower, random);
            DamageResult autoAttack = DamagePipeline.Resolve(
                new DamageRequest(player, enemy, baseDamage, baseAutoAttack.DamageType),
                random,
                actionAtUtc);
            if (autoAttack.Avoidance == DamageAvoidance.None && baseAutoAttack.ResourceOnHit > 0)
                player.AddResource(baseAutoAttack.ResourceOnHit);
        }

        TimeSpan resolvedElapsed = elapsed == TimeSpan.Zero ? available : elapsed;
        int monsterAttacks = (int)Math.Floor(
            resolvedElapsed.TotalSeconds / monster.AutoAttackInterval.TotalSeconds);
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
                new DamageRequest(enemy, player, baseDamage, DamageType.Physical),
                random,
                fightStartedAtUtc + TimeSpan.FromTicks(
                    monster.AutoAttackInterval.Ticks * (index + 1)));
            incoming += result.HpDamage;
        }

        // Incoming damage is informational in AFK mode. Never carry simulated HP loss into the next encounter.
        player.SetCurrentHp(player.MaxHp);
        return new FightResult(enemy.IsDead, resolvedElapsed, incoming);
    }

    private static void TryUseFarmAbility(
        CombatRuntimeState runtime,
        AbilityDefinition[] abilities,
        Guid targetActorId,
        DateTimeOffset now,
        IGameRandom random,
        ref int commandSequence)
    {
        foreach (AbilityDefinition ability in abilities)
        {
            string commandId = $"afk:{++commandSequence}:{ability.Id}";
            AbilityExecutionResult result = AbilityEngine.Execute(
                runtime,
                ability,
                new AbilityIntent(commandId, ability.Id, targetActorId),
                now,
                random);
            runtime.ProcessedCommandIds.Remove(commandId);
            if (result.Succeeded)
                return;
        }
    }

    private static AbilityDefinition[] ResolveFarmAbilities(
        AfkCharacterSnapshot snapshot,
        IReadOnlyDictionary<string, AbilityDefinition>? abilitiesById,
        ResolvedTalentModifiers talents)
    {
        if (abilitiesById is null || abilitiesById.Count == 0 || snapshot.KnownAbilityIds.Count == 0)
            return [];

        List<AbilityDefinition> resolved = [];
        foreach (string abilityId in snapshot.KnownAbilityIds.Distinct(StringComparer.Ordinal))
        {
            if (!abilitiesById.TryGetValue(abilityId, out AbilityDefinition? baseAbility))
                continue;

            AbilityDefinition ability = ArcherStaticAbilityHookResolver.Apply(
                MageStaticAbilityHookResolver.Apply(
                    PyromancerStaticAbilityHookResolver.Apply(
                        WarlordStaticAbilityHookResolver.Apply(
                            TalentAbilityResolver.Apply(baseAbility, talents),
                            talents),
                        talents),
                    talents),
                talents);
            if (IsSupportedFarmAbility(ability))
                resolved.Add(ability);
        }
        return resolved
            .OrderByDescending(ability => ability.Cooldown > TimeSpan.Zero)
            .ToArray();
    }

    private static bool IsSupportedFarmAbility(AbilityDefinition ability)
    {
        if (ability.Type != AbilityType.Instant
            || ability.TargetType != AbilityTargetType.SingleEnemy
            || ability.Actions is not { Count: > 0 }
            || ability.Actions.Any(action => action.Delay is { } delay && delay > TimeSpan.Zero))
        {
            return false;
        }

        bool hasDirectDamage = false;
        foreach (AbilityActionDefinition action in ability.Actions)
        {
            if (action.Type == AbilityActionType.Damage)
            {
                hasDirectDamage = true;
                continue;
            }
            if (action.Type != AbilityActionType.ResourceChange)
                return false;
        }
        return hasDirectDamage;
    }

    private static CombatActorState CreatePlayer(
        AfkCharacterSnapshot snapshot,
        TalentCombatModifiers talentCombatModifiers,
        ResourceProfile? resourceProfile) => new(
        SimulationPlayerId,
        snapshot.Stats.MaxHp,
        snapshot.Stats.MaxHp,
        resourceProfile?.MaxValue ?? 0,
        resourceProfile is null ? 0 : snapshot.CurrentResource,
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
        talentCombatModifiers,
        canDie: false);

    private static List<(MonsterDefinition Monster, decimal Weight)> ResolveEligibleEncounters(
        AfkFarmSimulationRequest request) => (request.Location.Encounters ?? [])
        .Where(encounter => encounter.Weight > 0
            && request.MonstersById.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster)
            && (request.TargetMonsterId is null
                || string.Equals(encounter.MonsterId, request.TargetMonsterId, StringComparison.Ordinal))
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

    private static ResourceProfile DeserializeResourceProfile(string json)
    {
        ResourceProfile? profile = JsonSerializer.Deserialize<ResourceProfile>(json, SnapshotJsonOptions);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || profile.MaxValue < 0)
            throw new InvalidOperationException("AFK character snapshot has an invalid resource profile.");
        return profile;
    }

    private static ResolvedTalentModifiers ResolveTalentModifiers(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            TalentStatModifiers stats = DeserializeTalentProperty<TalentStatModifiers>(root, "stats")
                ?? new TalentStatModifiers();
            TalentCombatModifiers combat = DeserializeTalentProperty<TalentCombatModifiers>(root, "combat")
                ?? new TalentCombatModifiers();
            HashSet<string> unlockedAbilityIds = DeserializeTalentProperty<HashSet<string>>(
                    root,
                    "unlockedAbilityIds")
                ?? new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, TalentAbilityModifiers> abilities =
                DeserializeTalentProperty<Dictionary<string, TalentAbilityModifiers>>(root, "abilities")
                ?? new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal);
            ResolvedTalentEventHook[] eventHooks =
                DeserializeTalentProperty<ResolvedTalentEventHook[]>(root, "eventHooks") ?? [];
            TalentModifierDefinition[] deferredHooks =
                DeserializeTalentProperty<TalentModifierDefinition[]>(root, "deferredHooks") ?? [];
            TalentProfileModifiers profiles =
                DeserializeTalentProperty<TalentProfileModifiers>(root, "profiles")
                ?? new TalentProfileModifiers();

            return new ResolvedTalentModifiers(
                stats,
                combat,
                unlockedAbilityIds,
                abilities,
                eventHooks,
                deferredHooks)
            {
                Profiles = profiles
            };
        }
        catch (JsonException)
        {
            return ResolvedTalentModifiers.Empty;
        }
    }

    private static T? DeserializeTalentProperty<T>(JsonElement root, string propertyName)
        where T : class
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out JsonElement value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(value.GetRawText(), SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement root,
        string propertyName,
        out JsonElement value)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            value = property.Value;
            return true;
        }

        value = default;
        return false;
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
        0, 0, 0, 0, request.Character.CurrentHp, 0, 0, 0, []);

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("AFK timestamps must be UTC.", parameterName);
    }

    private sealed record FightResult(bool Killed, TimeSpan Elapsed, decimal EstimatedIncomingDamage);
}
