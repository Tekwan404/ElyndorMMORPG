using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Simulation;

public enum CombatSimulationGearState
{
    None,
    Weak,
    Normal,
    Good
}

public sealed record CombatSimulationScenario(
    string ClassId,
    int PlayerLevel,
    string MonsterId,
    int Iterations = 100,
    int Seed = 1337,
    int MaxDurationSeconds = 90,
    IReadOnlyList<string>? AbilityPriority = null,
    IReadOnlyDictionary<string, int>? SelectedTalentRanks = null,
    IReadOnlyDictionary<string, int>? EquippedSetPieces = null,
    CombatSimulationGearState GearState = CombatSimulationGearState.None);

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
    IReadOnlyList<CombatSimulationDamageSource> DamageSources)
{
    public CombatSimulationGearState GearState { get; init; }
    public decimal PlayerMaxHp { get; init; }
    public decimal PlayerArmor { get; init; }
    public decimal PlayerMagicResistance { get; init; }
    public decimal PlayerPhysicalEhp { get; init; }
    public decimal PlayerMagicEhp { get; init; }
    public decimal PlayerAttackPower { get; init; }
    public decimal PlayerSpellPower { get; init; }
    public decimal EstimatedEnemyTtkSeconds { get; init; }
    public decimal EstimatedPlayerTtdSeconds { get; init; }
}

public sealed class CombatSimulationException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}

public sealed partial class CombatSimulationRunner(GameContentPackage content)
{
    private static readonly DateTimeOffset SimulationEpoch =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ActionStep = TimeSpan.FromMilliseconds(100);
    private static readonly Guid SimulationEquipmentSeed =
        Guid.Parse("8c90d387-4e78-4f6f-b8ae-c76463cf8d22");

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

        ResolvedTalentModifiers talentModifiers =
            ResolveTalentModifiers(scenario);
        SimulationEquipment simulationEquipment = ResolveSimulationEquipment(
            scenario,
            classProfile);
        EquipmentModifierSummary equipment = simulationEquipment.Modifiers;
        TalentPrimaryStatPercentages talentPercentages = new(
            talentModifiers.Stats.StrengthPercent,
            talentModifiers.Stats.AgilityPercent,
            talentModifiers.Stats.IntellectPercent,
            talentModifiers.Stats.StaminaPercent);
        CharacterStats playerStats =
            new CharacterStatCalculator(formula, classProfiles)
                .Calculate(
                    scenario.ClassId,
                    scenario.PlayerLevel,
                    CharacterStatInputs.Empty with
                    {
                        Equipment = equipment.PrimaryStats,
                        EquipmentDerived = new CharacterEquipmentDerivedModifiers(
                            MaxHpFlat: equipment.MaxHpFlat,
                            AttackPowerFlat: equipment.AttackPowerFlat,
                            SpellPowerFlat: equipment.SpellPowerFlat,
                            CriticalChancePercent: equipment.CriticalChancePercent,
                            CriticalDamagePercent: equipment.CriticalDamagePercent,
                            AccuracyPercent: equipment.AccuracyPercent,
                            AttackSpeedPercent: equipment.AttackSpeedPercent,
                            ArmorFlat: equipment.ArmorFlat,
                            MagicResistanceFlat: equipment.MagicResistanceFlat,
                            DodgePercent: equipment.DodgePercent,
                            ArmorPenetrationPercent: equipment.ArmorPenetrationPercent,
                            MagicPenetrationPercent: equipment.MagicPenetrationPercent,
                            BlockChancePercent: equipment.BlockChancePercent,
                            BlockValueMin: equipment.BlockValueMin,
                            BlockValueMax: equipment.BlockValueMax),
                        TalentPercentages = talentPercentages,
                        TalentDerived = talentModifiers.Stats
                    });
        ResourceProfile resource = CharacterResourceProfileResolver.Resolve(
            baseResource,
            content.ResourceScaling,
            playerStats,
            talentModifiers.Stats.MaxResourceFlat + equipment.MaxResourceFlat);

        Dictionary<string, AbilityDefinition> abilities = (content.Abilities ?? [])
            .ToDictionary(ability => ability.Id, StringComparer.Ordinal);
        string[] knownAbilityIds = CharacterKnownAbilityResolver.Resolve(
                classProfile,
                scenario.PlayerLevel,
                talentModifiers.UnlockedAbilityIds)
            .Where(abilities.ContainsKey)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
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
                talentModifiers,
                equipment,
                scenario.EquippedSetPieces ?? simulationEquipment.SetPieces,
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

        decimal physicalMultiplier = DefenseMitigationFormula.CalculateDamageMultiplier(
            playerStats.Armor,
            scenario.PlayerLevel);
        decimal magicMultiplier = DefenseMitigationFormula.CalculateDamageMultiplier(
            playerStats.MagicResistance,
            scenario.PlayerLevel);

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
            sources)
        {
            GearState = scenario.GearState,
            PlayerMaxHp = playerStats.MaxHp,
            PlayerArmor = playerStats.Armor,
            PlayerMagicResistance = playerStats.MagicResistance,
            PlayerPhysicalEhp = physicalMultiplier <= 0 ? playerStats.MaxHp : playerStats.MaxHp / physicalMultiplier,
            PlayerMagicEhp = magicMultiplier <= 0 ? playerStats.MaxHp : playerStats.MaxHp / magicMultiplier,
            PlayerAttackPower = playerStats.AttackPower,
            PlayerSpellPower = playerStats.SpellPower,
            EstimatedEnemyTtkSeconds = averagePlayerDps <= 0 ? 0 : monster.MaxHp / averagePlayerDps,
            EstimatedPlayerTtdSeconds = averageEnemyDps <= 0 ? 0 : playerStats.MaxHp / averageEnemyDps
        };
    }

    private SimulationRun RunSingle(
        CombatSimulationScenario scenario,
        int iteration,
        ClassProfile classProfile,
        CharacterStats playerStats,
        ResourceProfile resource,
        MonsterDefinition monster,
        MonsterAiProfile enemyAi,
        Dictionary<string, AbilityDefinition> abilities,
        IReadOnlyList<string> knownAbilityIds,
        IReadOnlyList<AbilityDefinition> abilityPriority,
        ResolvedTalentModifiers talentModifiers,
        EquipmentModifierSummary equipment,
        IReadOnlyDictionary<string, int> equippedSetPieces,
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
        double baseAttackIntervalSeconds = equipment.WeaponBaseAttackIntervalSeconds is { } weaponInterval
            ? (double)weaponInterval
            : classAutoAttack.Interval.TotalSeconds;
        AutoAttackProfile playerAutoAttack = classAutoAttack with
        {
            Interval = TimeSpan.FromSeconds(
                baseAttackIntervalSeconds / (double)attackSpeedMultiplier),
            BaseDamageMin = equipment.WeaponDamageMin ?? classAutoAttack.BaseDamageMin,
            BaseDamageMax = equipment.WeaponDamageMax ?? classAutoAttack.BaseDamageMax
        };

        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            scenario.ClassId,
            $"SIM_{scenario.ClassId}",
            resource.Id,
            playerAutoAttack,
            new HashSet<string>(knownAbilityIds, StringComparer.Ordinal),
            resource.CombatRegenPerSecond,
            EquippedSetPieces: equippedSetPieces,
            SetPassives: EquipmentSetEffectResolver.Resolve(content.EquipmentSets ?? []));
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
                0,
                monster.AutoAttackBaseDamageMin,
                monster.AutoAttackBaseDamageMax),
            new HashSet<string>(monster.AbilityIds, StringComparer.Ordinal));

        DateTimeOffset startedAt = SimulationEpoch.AddDays(iteration);
        CombatSession session = new(
            Guid.NewGuid(),
            player,
            enemy,
            abilities,
            enemyAi,
            talentModifiers,
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

    private ResolvedTalentModifiers ResolveTalentModifiers(
        CombatSimulationScenario scenario)
    {
        IReadOnlyDictionary<string, int> selected =
            scenario.SelectedTalentRanks
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        if (selected.Count == 0)
            return ResolvedTalentModifiers.Empty;

        TalentTreeDefinition tree = (content.TalentTrees ?? [])
            .SingleOrDefault(candidate =>
                string.Equals(
                    candidate.ClassId,
                    scenario.ClassId,
                    StringComparison.Ordinal))
            ?? throw Invalid(
                "simulation_talent_tree_missing",
                $"Talent tree for class '{scenario.ClassId}' does not exist.");

        Dictionary<string, TalentDefinition> nodes =
            tree.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach ((string talentId, int rank) in selected)
        {
            if (!nodes.TryGetValue(talentId, out TalentDefinition? node)
                || rank < 1
                || rank > node.MaxRank)
            {
                throw Invalid(
                    "simulation_talent_rank_invalid",
                    $"Talent '{talentId}' has an invalid simulation rank.");
            }
        }

        return TalentModifierResolver.Resolve(tree, selected);
    }

    private static AbilityDefinition[] ResolveAbilityPriority(
        IReadOnlyList<string>? requested,
        IReadOnlyList<string> knownAbilityIds,
        Dictionary<string, AbilityDefinition> abilities)
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

    private SimulationEquipment ResolveSimulationEquipment(
        CombatSimulationScenario scenario,
        ClassProfile classProfile,
        bool preferManaBudget = false)
    {
        if (scenario.GearState == CombatSimulationGearState.None)
        {
            return new SimulationEquipment(
                EquipmentStatModifierResolver.ResolveDetailed([], content.EquipmentSets ?? []),
                new Dictionary<string, int>(StringComparer.Ordinal));
        }

        CombatBalanceProfile balance = content.CombatBalance
            ?? throw Invalid(
                "simulation_combat_balance_missing",
                "Combat balance profile is required for gear benchmarks.");
        CombatGearBenchmarkDefinition gearState = balance.GearStates.SingleOrDefault(item =>
                string.Equals(
                    item.Id,
                    scenario.GearState.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            ?? throw Invalid(
                "simulation_gear_state_missing",
                $"Combat gear benchmark '{scenario.GearState}' does not exist.");
        ItemizationDefinition itemization = content.Itemization
            ?? throw Invalid(
                "simulation_itemization_missing",
                "Itemization content is required for gear benchmarks.");

        int targetRequiredLevel = Math.Max(1, scenario.PlayerLevel - gearState.ItemLevelLag);
        ItemDefinition[] wearable = (content.Items ?? [])
            .Where(item =>
                item.Type == ItemType.Equipment
                && item.Slot is not null
                && item.RequiredLevel <= scenario.PlayerLevel
                && (!preferManaBudget || item.Rarity != ItemRarity.Unique
                    && item.SpecialEffectIds is not { Count: > 0 }
                    && item.HonorPrice == 0
                    && item.SetId?.StartsWith("SET_L60_PVE_T1_", StringComparison.Ordinal) != true)
                && CanEquip(classProfile, item))
            .ToArray();

        List<ItemDefinition> selected = [];
        ItemDefinition Generate(ItemDefinition template)
        {
            var key = ItemGenerationKey.Create(SimulationEquipmentSeed,
                $"{scenario.ClassId}|MANA|{template.Id}", (int)template.Slot!.Value);
            var generated = ProceduralItemPolicy.Generate(template,
                ItemizationBudgetPolicy.NormalizeForTemplate(template, itemization),
                gearState.QualityProfileId, key, "SIMULATION");
            return generated is null ? template
                : ItemInstanceGenerator.ApplyGeneratedAffixes(template, generated.Affixes, generated.DisplayName);
        }
        decimal ManaValue(ItemDefinition item) =>
            item.Stats.Intellect * (content.ResourceScaling?.ManaPerIntellect ?? 0) + item.MaxResourceFlat;
        List<ItemDefinition> manaMainHands = [];
        foreach (IGrouping<EquipmentSlot, ItemDefinition> group in wearable
                     .GroupBy(item => CanonicalSlot(item.Slot!.Value)))
        {
            ItemDefinition[] atOrBelowTarget = group
                .Where(item => item.RequiredLevel <= targetRequiredLevel)
                .OrderByDescending(item => preferManaBudget
                    ? item.Stats.Intellect * (content.ResourceScaling?.ManaPerIntellect ?? 0) + item.MaxResourceFlat : 0)
                .ThenByDescending(item => item.RequiredLevel)
                .ThenByDescending(item => item.Rarity)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            ItemDefinition? template = atOrBelowTarget.FirstOrDefault()
                ?? group
                    .OrderBy(item => item.RequiredLevel)
                    .ThenBy(item => item.Rarity)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
            if (template is null)
                continue;

            if (preferManaBudget)
            {
                var candidates = atOrBelowTarget.Length == 0 ? [template] : atOrBelowTarget;
                var rolled = candidates.Select(Generate)
                    .OrderByDescending(ManaValue).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
                selected.Add(rolled[0]);
                if (group.Key == EquipmentSlot.MainHand) manaMainHands.AddRange(rolled);
                continue;
            }

            ItemizationDefinition effectiveItemization =
                ItemizationBudgetPolicy.NormalizeForTemplate(template, itemization);
            ItemGenerationKey key = ItemGenerationKey.Create(
                SimulationEquipmentSeed,
                $"{scenario.ClassId}|{scenario.PlayerLevel}|{template.Id}",
                selected.Count);
            GeneratedItemInstance? generated = ProceduralItemPolicy.Generate(
                template,
                effectiveItemization,
                gearState.QualityProfileId,
                key,
                "SIMULATION");
            selected.Add(generated is null
                ? template
                : ItemInstanceGenerator.ApplyGeneratedAffixes(
                    template,
                    generated.Affixes,
                    generated.DisplayName));
        }

        ItemDefinition? mainHand = selected.SingleOrDefault(item =>
            CanonicalSlot(item.Slot!.Value) == EquipmentSlot.MainHand);
        if (preferManaBudget && mainHand is not null)
        {
            decimal offHandMana = selected.Where(item => item.Slot == EquipmentSlot.OffHand)
                .Sum(ManaValue);
            var best = manaMainHands.OrderByDescending(item => ManaValue(item)
                + (EquipmentCategoryIds.UsesBothHands(item.WeaponCategory) ? 0 : offHandMana)).First();
            selected.Remove(mainHand);
            selected.Add(best);
            mainHand = best;
        }
        if (mainHand is not null && EquipmentCategoryIds.UsesBothHands(mainHand.WeaponCategory))
        {
            selected.RemoveAll(item =>
                CanonicalSlot(item.Slot!.Value) == EquipmentSlot.OffHand);
        }

        EquipmentModifierSummary modifiers = EquipmentStatModifierResolver.ResolveDetailed(
            selected,
            content.EquipmentSets ?? []);
        IReadOnlyDictionary<string, int> setPieces = selected
            .Where(item => !string.IsNullOrWhiteSpace(item.SetId))
            .GroupBy(item => item.SetId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new SimulationEquipment(modifiers, setPieces);
    }

    private bool CanEquip(ClassProfile classProfile, ItemDefinition item)
    {
        string? explicitClassOwner = (content.ClassProfiles ?? [])
            .Select(profile => profile.Id)
            .FirstOrDefault(classId =>
                item.Id.StartsWith($"{classId}_", StringComparison.Ordinal)
                || item.Id.Contains($"_{classId}_", StringComparison.Ordinal)
                || item.RandomAffixPoolId?.StartsWith($"{classId}_", StringComparison.Ordinal) == true);
        if (explicitClassOwner is not null
            && !string.Equals(explicitClassOwner, classProfile.Id, StringComparison.Ordinal))
            return false;

        if (item.WeaponCategory is not null
            && !classProfile.AllowedWeaponCategories.Contains(item.WeaponCategory, StringComparer.Ordinal))
            return false;
        if (item.ArmorCategory is not null
            && !classProfile.AllowedArmorCategories.Contains(item.ArmorCategory, StringComparer.Ordinal))
            return false;
        if (item.OffHandCategory is not null
            && !(classProfile.AllowedOffHandCategories ?? []).Contains(
                item.OffHandCategory,
                StringComparer.Ordinal))
            return false;
        return true;
    }

    private static EquipmentSlot CanonicalSlot(EquipmentSlot slot) => slot;

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

    private static decimal Percentile(List<decimal> sorted, decimal percentile)
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
            throw new ArgumentOutOfRangeException(
                nameof(scenario),
                "Player level must be between 1 and 60.");
        if (scenario.Iterations is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(
                nameof(scenario),
                "Iterations must be between 1 and 1000.");
        if (scenario.MaxDurationSeconds is < 1 or > 180)
            throw new ArgumentOutOfRangeException(
                nameof(scenario),
                "Maximum duration must be between 1 and 180 seconds.");
    }

    private static CombatSimulationException Invalid(string code, string message) =>
        new(code, message);

    private sealed record SimulationEquipment(
        EquipmentModifierSummary Modifiers,
        IReadOnlyDictionary<string, int> SetPieces);

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
