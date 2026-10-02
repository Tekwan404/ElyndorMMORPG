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
using Elyndor.Core.Progression;
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
    IReadOnlyDictionary<string, AbilityDefinition>? AbilitiesById = null,
    LevelProgressionDefinition? LevelProgression = null,
    ProgressionBalanceProfile? ProgressionBalance = null);

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
    IReadOnlyList<AfkFarmLootCandidate> LootCandidates,
    IReadOnlyList<string> DefeatedMonsterIds);

/// <summary>
/// Calculates AFK intervals without creating online combat sessions or mutating durable state.
/// Incoming damage informs combat timing only; AFK never spends HP or causes death.
/// Direct single-target instant and casted abilities are resolved through the shared ability/damage pipeline;
/// delayed actions and stateful class proc runtimes remain intentionally outside the lightweight AFK model.
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
        TimeSpan successfulEncounterPressureWindow = TimeSpan.Zero;
        List<AfkFarmLootCandidate> loot = [];
        List<string> defeatedMonsterIds = [];

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
            defeatedMonsterIds.Add(monster.Id);
            successfulKillTime += fight.Elapsed;
            successfulEncounterPressureWindow += monster.AutoAttackInterval;
            xp += request.LevelProgression is not null
                && request.ProgressionBalance is not null
                ? ProgressionRewardCalculator.ResolveMonsterXp(
                    monster,
                    request.Character.Level,
                    1,
                    request.LevelProgression,
                    request.ProgressionBalance)
                : monster.XpReward;
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

        // Efficiency must be class-neutral. Using the player's auto-attack interval as the baseline
        // makes weapon-centric classes look artificially stronger than casters. Instead, compare the
        // actual build's time-to-kill against the defeated encounters' own attack cadence. A build
        // averaging one kill within one enemy attack cycle reaches 100%; slower kills scale down.
        int efficiency = kills == 0
            ? 0
            : successfulKillTime <= TimeSpan.Zero
                ? 100
                : Math.Clamp(
                    (int)Math.Floor(
                        successfulEncounterPressureWindow.TotalMilliseconds
                        / successfulKillTime.TotalMilliseconds
                        * 100d),
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
            loot,
            defeatedMonsterIds);
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
        TimeSpan nextAutoAttackAt = playerInterval;
        DateTimeOffset? nextAbilityDecisionAtUtc = runtime is not null && farmAbilities.Length > 0
            ? fightStartedAtUtc
            : null;

        while (!enemy.IsDead && elapsed < available)
        {
            DateTimeOffset? castResolvesAtUtc = runtime?.ActiveCast?.ResolvesAtUtc;
            TimeSpan nextEventAt = nextAutoAttackAt;
            if (castResolvesAtUtc is { } castAt)
                nextEventAt = Min(nextEventAt, castAt - fightStartedAtUtc);
            if (nextAbilityDecisionAtUtc is { } abilityAt)
                nextEventAt = Min(nextEventAt, abilityAt - fightStartedAtUtc);

            if (nextEventAt > available)
            {
                AdvanceCombatResource(player, resourceProfile, available - elapsed);
                elapsed = available;
                break;
            }

            if (nextEventAt < elapsed)
                nextEventAt = elapsed;

            AdvanceCombatResource(player, resourceProfile, nextEventAt - elapsed);
            elapsed = nextEventAt;
            DateTimeOffset actionAtUtc = fightStartedAtUtc + elapsed;

            if (runtime?.ActiveCast is { } activeCast && activeCast.ResolvesAtUtc <= actionAtUtc)
            {
                AbilityEngine.CompleteCast(runtime, actionAtUtc, random);
                if (enemy.IsDead)
                    break;
                nextAbilityDecisionAtUtc = actionAtUtc;
            }

            if (runtime is not null
                && runtime.ActiveCast is null
                && nextAbilityDecisionAtUtc is { } decisionAt
                && decisionAt <= actionAtUtc)
            {
                bool usedAbility = TryUseFarmAbility(
                    runtime,
                    farmAbilities,
                    enemy.ActorId,
                    actionAtUtc,
                    random,
                    ref abilityCommandSequence);

                if (enemy.IsDead)
                    break;

                if (usedAbility && runtime.ActiveCast is { } startedCast)
                {
                    TimeSpan castEndsAt = startedCast.ResolvesAtUtc - fightStartedAtUtc;
                    if (nextAutoAttackAt <= castEndsAt)
                        nextAutoAttackAt = castEndsAt + playerInterval;
                    nextAbilityDecisionAtUtc = startedCast.ResolvesAtUtc;
                }
                else
                {
                    nextAbilityDecisionAtUtc = ResolveNextAbilityDecisionAtUtc(
                        runtime,
                        farmAbilities,
                        resourceProfile,
                        actionAtUtc);
                }
            }

            if (!enemy.IsDead && nextAutoAttackAt <= elapsed)
            {
                if (runtime?.ActiveCast is null)
                {
                    decimal baseDamage = AutoAttackDamageRoller.RollPlayerDamage(
                        baseAutoAttack, snapshot.Stats.AttackPower, random);
                    DamageResult autoAttack = DamagePipeline.Resolve(
                        new DamageRequest(player, enemy, baseDamage, baseAutoAttack.DamageType),
                        random,
                        actionAtUtc);
                    if (autoAttack.Avoidance == DamageAvoidance.None && baseAutoAttack.ResourceOnHit > 0)
                        player.AddResource(baseAutoAttack.ResourceOnHit);

                    nextAutoAttackAt += playerInterval;
                    if (runtime is not null && farmAbilities.Length > 0)
                    {
                        nextAbilityDecisionAtUtc = nextAbilityDecisionAtUtc is null
                            || nextAbilityDecisionAtUtc > actionAtUtc
                            ? actionAtUtc
                            : nextAbilityDecisionAtUtc;
                    }
                }
                else
                {
                    TimeSpan castEndsAt = runtime.ActiveCast.ResolvesAtUtc - fightStartedAtUtc;
                    nextAutoAttackAt = castEndsAt + playerInterval;
                }
            }

            if (nextEventAt == elapsed
                && nextAutoAttackAt <= elapsed
                && (nextAbilityDecisionAtUtc is null || nextAbilityDecisionAtUtc <= actionAtUtc)
                && runtime?.ActiveCast is null)
            {
                // Defensive guard against malformed zero-duration content producing a zero-time loop.
                nextAutoAttackAt = elapsed + TimeSpan.FromTicks(1);
            }
        }

        TimeSpan resolvedElapsed = elapsed;
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

    private static bool TryUseFarmAbility(
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
                return true;
        }
        return false;
    }

    private static DateTimeOffset? ResolveNextAbilityDecisionAtUtc(
        CombatRuntimeState runtime,
        AbilityDefinition[] abilities,
        ResourceProfile? resourceProfile,
        DateTimeOffset now)
    {
        DateTimeOffset? next = null;
        foreach (AbilityDefinition ability in abilities)
        {
            DateTimeOffset readyAt = now;
            if (ability.UsesGlobalCooldown
                && runtime.GlobalCooldownEndsAtUtc is { } gcdAt
                && gcdAt > readyAt)
            {
                readyAt = gcdAt;
            }
            if (runtime.Cooldowns.TryGetValue(ability.Id, out DateTimeOffset cooldownAt)
                && cooldownAt > readyAt)
            {
                readyAt = cooldownAt;
            }

            if (runtime.Actor.CurrentResource < ability.ResourceCost)
            {
                if (resourceProfile is null || resourceProfile.CombatRegenPerSecond <= 0)
                    continue;

                decimal missing = ability.ResourceCost - runtime.Actor.CurrentResource;
                double seconds = (double)(missing / resourceProfile.CombatRegenPerSecond);
                DateTimeOffset resourceReadyAt = now + TimeSpan.FromSeconds(seconds);
                if (resourceReadyAt > readyAt)
                    readyAt = resourceReadyAt;
            }

            if (readyAt <= now)
                continue;
            next = next is null || readyAt < next ? readyAt : next;
        }
        return next;
    }

    private static void AdvanceCombatResource(
        CombatActorState player,
        ResourceProfile? resourceProfile,
        TimeSpan elapsed)
    {
        if (resourceProfile is null
            || resourceProfile.CombatRegenPerSecond <= 0
            || elapsed <= TimeSpan.Zero)
        {
            return;
        }

        player.AddResource(resourceProfile.CombatRegenPerSecond * (decimal)elapsed.TotalSeconds);
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

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
        if ((ability.Type != AbilityType.Instant && ability.Type != AbilityType.Casted)
            || (ability.Type == AbilityType.Casted && ability.CastTime <= TimeSpan.Zero)
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
        0, 0, 0, 0, request.Character.CurrentHp, 0, 0, 0, [], []);

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("AFK timestamps must be UTC.", parameterName);
    }

    private sealed record FightResult(bool Killed, TimeSpan Elapsed, decimal EstimatedIncomingDamage);
}
