from pathlib import Path

patches = {
    Path("src/Elyndor.Core/Combat/Encounters/EncounterModels.cs"): [
        (
            """public sealed record SummonDefinition(\n    string MonsterId,\n    int Count,\n    int MaxActive = 0,\n    TimeSpan? Lifetime = null,\n    bool LinkToCaster = false,\n    bool DespawnOnBossDeath = true,\n    bool NoReward = true);""",
            """public sealed record SummonDefinition(\n    string MonsterId,\n    int Count,\n    int MaxActive = 0,\n    TimeSpan? Lifetime = null,\n    bool LinkToCaster = false,\n    bool DespawnOnBossDeath = true,\n    bool NoReward = true,\n    bool IsCombatObject = false,\n    string? AuraEffectId = null,\n    string? AuraTargetSelector = null);""",
        ),
        (
            """            case EncounterActionType.Summon:\n                if (action.Summon is null\n                    || string.IsNullOrWhiteSpace(action.Summon.MonsterId)\n                    || action.Summon.Count <= 0\n                    || action.Summon.MaxActive < 0\n                    || action.Summon.Lifetime is { } lifetime && lifetime <= TimeSpan.Zero)\n                {\n                    errors.Add($\"Phase '{phaseId}' summon action is invalid.\");\n                }\n                break;""",
            """            case EncounterActionType.Summon:\n                if (action.Summon is null\n                    || string.IsNullOrWhiteSpace(action.Summon.MonsterId)\n                    || action.Summon.Count <= 0\n                    || action.Summon.MaxActive < 0\n                    || action.Summon.Lifetime is { } lifetime && lifetime <= TimeSpan.Zero\n                    || action.Summon.AuraEffectId is not null\n                        && string.IsNullOrWhiteSpace(action.Summon.AuraEffectId)\n                    || action.Summon.AuraEffectId is null\n                        && !string.IsNullOrWhiteSpace(action.Summon.AuraTargetSelector)\n                    || !EncounterTargetSelectors.IsSupported(action.Summon.AuraTargetSelector))\n                {\n                    errors.Add($\"Phase '{phaseId}' summon action is invalid.\");\n                }\n                break;""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Encounters/LinkedSummonRegistry.cs"): [
        (
            """public sealed record LinkedSummonRegistration(\n    Guid ActorId,\n    Guid OwnerActorId,\n    string MonsterId,\n    DateTimeOffset SpawnedAtUtc,\n    DateTimeOffset? ExpiresAtUtc,\n    bool LinkToOwner,\n    bool DespawnOnOwnerDeath,\n    bool NoReward);""",
            """public sealed record LinkedSummonRegistration(\n    Guid ActorId,\n    Guid OwnerActorId,\n    string MonsterId,\n    DateTimeOffset SpawnedAtUtc,\n    DateTimeOffset? ExpiresAtUtc,\n    bool LinkToOwner,\n    bool DespawnOnOwnerDeath,\n    bool NoReward,\n    bool IsCombatObject,\n    string? AuraEffectId,\n    IReadOnlyList<Guid> AuraTargetActorIds);""",
        ),
        (
            """    public LinkedSummonRegistration Register(\n        Guid actorId,\n        Guid ownerActorId,\n        SummonDefinition definition,\n        DateTimeOffset now)""",
            """    public LinkedSummonRegistration Register(\n        Guid actorId,\n        Guid ownerActorId,\n        SummonDefinition definition,\n        DateTimeOffset now,\n        IReadOnlyList<Guid>? auraTargetActorIds = null)""",
        ),
        (
            """        if (_active.ContainsKey(actorId))\n            throw new InvalidOperationException($\"Summon actor '{actorId}' is already registered.\");""",
            """        if (auraTargetActorIds?.Any(targetId => targetId == Guid.Empty) == true)\n            throw new ArgumentException(\"Aura target actor identifiers cannot be empty.\", nameof(auraTargetActorIds));\n        if (_active.ContainsKey(actorId))\n            throw new InvalidOperationException($\"Summon actor '{actorId}' is already registered.\");""",
        ),
        (
            """        LinkedSummonRegistration registration = new(\n            actorId,\n            ownerActorId,\n            definition.MonsterId,\n            now,\n            definition.Lifetime is { } lifetimeValue ? now + lifetimeValue : null,\n            definition.LinkToCaster,\n            definition.DespawnOnBossDeath,\n            definition.NoReward);""",
            """        LinkedSummonRegistration registration = new(\n            actorId,\n            ownerActorId,\n            definition.MonsterId,\n            now,\n            definition.Lifetime is { } lifetimeValue ? now + lifetimeValue : null,\n            definition.LinkToCaster,\n            definition.DespawnOnBossDeath,\n            definition.NoReward,\n            definition.IsCombatObject,\n            definition.AuraEffectId,\n            auraTargetActorIds?.Distinct().ToArray() ?? []);""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Sessions/CombatSessionModels.cs"): [
        (
            """public sealed record CombatParticipantDefinition(\n    CombatActorState Actor,\n    CombatActorKind Kind,\n    string DefinitionId,\n    string Name,\n    string ResourceType,\n    AutoAttackProfile AutoAttack,\n    IReadOnlySet<string> KnownAbilityIds,\n    decimal ResourceRegenPerSecond = 0,\n    bool CanAutoAttack = true,\n    AutoAttackProfile? OffHandAutoAttack = null,\n    MonsterRank? MonsterRank = null);""",
            """public sealed record CombatParticipantDefinition(\n    CombatActorState Actor,\n    CombatActorKind Kind,\n    string DefinitionId,\n    string Name,\n    string ResourceType,\n    AutoAttackProfile AutoAttack,\n    IReadOnlySet<string> KnownAbilityIds,\n    decimal ResourceRegenPerSecond = 0,\n    bool CanAutoAttack = true,\n    AutoAttackProfile? OffHandAutoAttack = null,\n    MonsterRank? MonsterRank = null,\n    bool IsCombatObject = false,\n    bool RewardEligible = true);""",
        ),
        (
            """    IReadOnlyDictionary<string, DateTimeOffset>? ConsumableCooldowns = null,\n    double? AutoAttackIntervalSeconds = null,\n    DateTimeOffset? NextAutoAttackAtUtc = null,\n    Guid? CurrentAggroTargetActorId = null);""",
            """    IReadOnlyDictionary<string, DateTimeOffset>? ConsumableCooldowns = null,\n    double? AutoAttackIntervalSeconds = null,\n    DateTimeOffset? NextAutoAttackAtUtc = null,\n    Guid? CurrentAggroTargetActorId = null,\n    bool IsCombatObject = false,\n    bool RewardEligible = true);""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.EncounterSupport.cs"): [
        (
            """    private CombatParticipantDefinition SpawnEncounterEnemy(\n        EncounterEnemyProfile profile,\n        Guid sourceActorId,\n        DateTimeOffset now)""",
            """    private CombatParticipantDefinition SpawnEncounterEnemy(\n        EncounterEnemyProfile profile,\n        Guid sourceActorId,\n        DateTimeOffset now,\n        bool isCombatObject = false,\n        bool rewardEligible = true)""",
        ),
        (
            """        CombatParticipantDefinition summoned = CreateSummonedParticipant(profile.Monster);\n        CombatActorState[] existingEnemyActors = _enemies""",
            """        CombatParticipantDefinition summoned = CreateSummonedParticipant(profile.Monster) with\n        {\n            KnownAbilityIds = isCombatObject\n                ? new HashSet<string>(StringComparer.Ordinal)\n                : new HashSet<string>(profile.Monster.AbilityIds, StringComparer.Ordinal),\n            CanAutoAttack = !isCombatObject,\n            IsCombatObject = isCombatObject,\n            RewardEligible = rewardEligible\n        };\n        CombatActorState[] existingEnemyActors = _enemies""",
        ),
        (
            """        _enemyAiRuntimes.Add(\n            summoned.Actor.ActorId,\n            new EnemyAiRuntime(\n                profile.AiProfile,\n                now,\n                now + summoned.AutoAttack.Interval));""",
            """        EnemyAiRuntime summonedAi = new(\n            profile.AiProfile,\n            now,\n            now + summoned.AutoAttack.Interval);\n        if (isCombatObject)\n            summonedAi.NextActionAtUtc = null;\n        _enemyAiRuntimes.Add(summoned.Actor.ActorId, summonedAi);""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.GenericEncounter.cs"): [
        (
            """                if (action.Type == EncounterActionType.Summon\n                    && action.Summon is { } summon\n                    && !enemyProfiles.ContainsKey(summon.MonsterId))\n                {\n                    throw new ArgumentException(\n                        $\"Encounter '{definition.Id}' has no runtime profile for summon '{summon.MonsterId}'.\",\n                        nameof(enemyProfiles));\n                }""",
            """                if (action.Type == EncounterActionType.Summon\n                    && action.Summon is { } summon\n                    && !enemyProfiles.ContainsKey(summon.MonsterId))\n                {\n                    throw new ArgumentException(\n                        $\"Encounter '{definition.Id}' has no runtime profile for summon '{summon.MonsterId}'.\",\n                        nameof(enemyProfiles));\n                }\n                if (action.Type == EncounterActionType.Summon\n                    && action.Summon?.AuraEffectId is { } auraEffectId\n                    && !effects.ContainsKey(auraEffectId))\n                {\n                    throw new ArgumentException(\n                        $\"Encounter '{definition.Id}' has no runtime aura effect '{auraEffectId}'.\",\n                        nameof(effects));\n                }""",
        ),
        (
            """            eventType = EncounterTriggerType.AddDeath;\n            eventDefinitionId = deadEnemy.DefinitionId;\n            _genericEncounterSummons?.TryRemove(combatEvent.ActorId, out _);""",
            """            eventType = EncounterTriggerType.AddDeath;\n            eventDefinitionId = deadEnemy.DefinitionId;\n            if (_genericEncounterSummons?.TryRemove(\n                    combatEvent.ActorId,\n                    out LinkedSummonRegistration? removed) == true\n                && removed is not null)\n            {\n                RemoveLinkedSummonAura(removed, combatEvent.OccurredAtUtc);\n            }""",
        ),
        (
            """        foreach (LinkedSummonRegistration summon in expired)\n        {\n            DeactivateEncounterEnemy(""",
            """        foreach (LinkedSummonRegistration summon in expired)\n        {\n            RemoveLinkedSummonAura(summon, summon.ExpiresAtUtc ?? now);\n            DeactivateEncounterEnemy(""",
        ),
        (
            """        foreach (LinkedSummonRegistration summon in\n                 _genericEncounterSummons.CollectForOwnerDeath(ownerActorId))\n        {\n            DeactivateEncounterEnemy(""",
            """        foreach (LinkedSummonRegistration summon in\n                 _genericEncounterSummons.CollectForOwnerDeath(ownerActorId))\n        {\n            RemoveLinkedSummonAura(summon, now);\n            DeactivateEncounterEnemy(""",
        ),
        (
            """            CombatParticipantDefinition spawned = SpawnEncounterEnemy(\n                profile,\n                _primaryEnemyActorId,\n                now);\n            registry.Register(\n                spawned.Actor.ActorId,\n                _primaryEnemyActorId,\n                summon with { Count = 1 },\n                now);""",
            """            CombatParticipantDefinition spawned = SpawnEncounterEnemy(\n                profile,\n                _primaryEnemyActorId,\n                now,\n                summon.IsCombatObject,\n                rewardEligible: !summon.NoReward);\n            Guid[] auraTargetActorIds = string.IsNullOrWhiteSpace(summon.AuraEffectId)\n                ? []\n                : ResolveGenericEncounterTargets(summon.AuraTargetSelector, now)\n                    .Select(target => target.ActorId)\n                    .ToArray();\n            LinkedSummonRegistration registration = registry.Register(\n                spawned.Actor.ActorId,\n                _primaryEnemyActorId,\n                summon with { Count = 1 },\n                now,\n                auraTargetActorIds);\n            ApplyLinkedSummonAura(registration, now);""",
        ),
        (
            """    private void ExecuteGenericApplyEffect(\n        EncounterActionDefinition action,\n        DateTimeOffset now)""",
            """    private void ApplyLinkedSummonAura(\n        LinkedSummonRegistration registration,\n        DateTimeOffset now)\n    {\n        if (string.IsNullOrWhiteSpace(registration.AuraEffectId)\n            || registration.AuraTargetActorIds.Count == 0)\n        {\n            return;\n        }\n\n        EffectDefinition source = _genericEncounterEffects[registration.AuraEffectId];\n        TimeSpan duration = registration.ExpiresAtUtc is { } expiresAtUtc\n            ? expiresAtUtc - now\n            : PersistentEncounterEffectDuration;\n        if (duration <= TimeSpan.Zero)\n            return;\n\n        EffectDefinition aura = source with\n        {\n            Duration = duration,\n            SourceSpecific = true\n        };\n        foreach (Guid targetActorId in registration.AuraTargetActorIds)\n        {\n            CombatActorState? target = ResolveCombatActor(targetActorId);\n            if (target is null || target.IsDead)\n                continue;\n            ApplyKernelEvents(\n                EffectEngine.Apply(target, registration.ActorId, aura, now),\n                registration.ActorId,\n                target.ActorId,\n                aura.Id);\n        }\n    }\n\n    private void RemoveLinkedSummonAura(\n        LinkedSummonRegistration registration,\n        DateTimeOffset now)\n    {\n        if (string.IsNullOrWhiteSpace(registration.AuraEffectId))\n            return;\n\n        foreach (Guid targetActorId in registration.AuraTargetActorIds)\n        {\n            CombatActorState? target = ResolveCombatActor(targetActorId);\n            if (target is null)\n                continue;\n            ApplyKernelEvents(\n                EffectEngine.RemoveOwned(\n                    target,\n                    registration.AuraEffectId,\n                    registration.ActorId,\n                    now),\n                registration.ActorId,\n                target.ActorId,\n                registration.AuraEffectId);\n        }\n    }\n\n    private void ExecuteGenericApplyEffect(\n        EncounterActionDefinition action,\n        DateTimeOffset now)""",
        ),
    ],
    Path("src/Elyndor.Core/Content/Validation/EncounterContentValidator.cs"): [
        (
            """                    EncounterActionDefinition action = phase.Actions[actionIndex];\n                    string actionPath = $\"{phasePath}.actions[{actionIndex}]\";\n                    switch (action.Type)""",
            """                    EncounterActionDefinition action = phase.Actions[actionIndex];\n                    string actionPath = $\"{phasePath}.actions[{actionIndex}]\";\n                    if (action.Type == EncounterActionType.Summon\n                        && action.Summon?.AuraEffectId is { Length: > 0 } auraEffectId\n                        && !effectIds.Contains(auraEffectId))\n                    {\n                        context.Errors.Add(new ContentValidationError(\n                            \"UNKNOWN_ENCOUNTER_AURA_EFFECT\",\n                            $\"{actionPath}.summon.auraEffectId\",\n                            $\"Encounter '{encounter.Id}' references unknown aura effect '{auraEffectId}'.\"));\n                    }\n                    switch (action.Type)""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs"): [
        (
            """            .Select(enemy => ActorSnapshot(\n                enemy,\n                _enemyRuntimes[enemy.Actor.ActorId],\n                Status == CombatSessionStatus.Active && !enemy.Actor.IsDead))""",
            """            .Select(enemy => ActorSnapshot(\n                enemy,\n                _enemyRuntimes[enemy.Actor.ActorId],\n                Status == CombatSessionStatus.Active\n                    && !enemy.Actor.IsDead\n                    && enemy.CanAutoAttack))""",
        ),
        (
            """        Append(new CombatEvent(\n            CombatEventType.EnemyKilled,\n            death.OccurredAtUtc,\n            _player.Actor.ActorId,\n            killedEnemy.DefinitionId,\n            SourceActorId: death.SourceActorId ?? _player.Actor.ActorId,\n            TargetActorId: killedEnemy.Actor.ActorId,\n            IsPeriodic: death.IsPeriodic,\n            DamageType: death.DamageType,\n            WeaponHand: death.WeaponHand,\n            WeaponDefinitionId: death.WeaponDefinitionId));\n        TriggerTalent(\n            TalentModifierKeys.OnEnemyKilled,\n            death.OccurredAtUtc);\n        ApplyBerserkerEnemyKilledHooks(death.OccurredAtUtc);\n        ApplyPyromancerEnemyKilledHooks(death);\n        ApplyArcherEnemyKilledHooks(death.OccurredAtUtc);\n        ApplyWarlordEnemyKilledHooks(death);""",
            """        if (!killedEnemy.IsCombatObject)\n        {\n            Append(new CombatEvent(\n                CombatEventType.EnemyKilled,\n                death.OccurredAtUtc,\n                _player.Actor.ActorId,\n                killedEnemy.DefinitionId,\n                SourceActorId: death.SourceActorId ?? _player.Actor.ActorId,\n                TargetActorId: killedEnemy.Actor.ActorId,\n                IsPeriodic: death.IsPeriodic,\n                DamageType: death.DamageType,\n                WeaponHand: death.WeaponHand,\n                WeaponDefinitionId: death.WeaponDefinitionId));\n            TriggerTalent(\n                TalentModifierKeys.OnEnemyKilled,\n                death.OccurredAtUtc);\n            ApplyBerserkerEnemyKilledHooks(death.OccurredAtUtc);\n            ApplyPyromancerEnemyKilledHooks(death);\n            ApplyArcherEnemyKilledHooks(death.OccurredAtUtc);\n            ApplyWarlordEnemyKilledHooks(death);\n        }""",
        ),
        (
            """            definition.Kind == CombatActorKind.Monster\n                ? GetEnemyCurrentTargetActorId(definition.Actor.ActorId, CurrentTimeUtc)\n                : null);""",
            """            definition.Kind == CombatActorKind.Monster && !definition.IsCombatObject\n                ? GetEnemyCurrentTargetActorId(definition.Actor.ActorId, CurrentTimeUtc)\n                : null,\n            definition.IsCombatObject,\n            definition.RewardEligible);""",
        ),
    ],
    Path("src/Elyndor.Infrastructure/Progression/CombatRewardService.cs"): [
        (
            """        CombatActorSnapshot[] enemies =\n            snapshot.Enemies?.ToArray() ?? [snapshot.Enemy];""",
            """        CombatActorSnapshot[] enemies =\n            (snapshot.Enemies?.ToArray() ?? [snapshot.Enemy])\n                .Where(enemy => enemy.RewardEligible)\n                .ToArray();""",
        ),
    ],
    Path("src/Elyndor.Infrastructure/Professions/ProfessionService.cs"): [
        (
            """        foreach (CombatActorSnapshot enemy in enemies)\n        {\n            if (enemy.Hp > 0 || !sources.ContainsKey(enemy.DefinitionId))""",
            """        foreach (CombatActorSnapshot enemy in enemies)\n        {\n            if (!enemy.RewardEligible\n                || enemy.Hp > 0\n                || !sources.ContainsKey(enemy.DefinitionId))""",
        ),
    ],
    Path("tests/Elyndor.UnitTests/Combat/CombatSessionGenericEncounterTests.cs"): [
        (
            """using Elyndor.Core.Combat.Damage;\nusing Elyndor.Core.Combat.Encounters;""",
            """using Elyndor.Core.Combat.Damage;\nusing Elyndor.Core.Combat.Effects;\nusing Elyndor.Core.Combat.Encounters;""",
        ),
        (
            """        CombatActorSnapshot summoned = Assert.Single(\n            session.Snapshot().Enemies!,\n            enemy => enemy.DefinitionId == add.Id);\n        Assert.Equal(Now.AddSeconds(5), session.NextDueAtUtc);""",
            """        CombatActorSnapshot summoned = Assert.Single(\n            session.Snapshot().Enemies!,\n            enemy => enemy.DefinitionId == add.Id);\n        Assert.False(summoned.RewardEligible);\n        Assert.Equal(Now.AddSeconds(5), session.NextDueAtUtc);""",
        ),
        (
            """    [Fact]\n    public void HpPhaseChangesBossAbilitySetBeforeItsNextAction()""",
            """    [Fact]\n    public void LinkedCombatObjectAppliesAuraUntilDestroyedAndIsRewardIneligible()\n    {\n        AbilityDefinition strike = DamageAbility(\"OBJECT_STRIKE\", 50);\n        EffectDefinition aura = new(\n            \"TEST_BANNER_AURA\",\n            EffectKind.StatModifier,\n            TimeSpan.FromMinutes(10),\n            1,\n            EffectStackPolicy.Replace,\n            25,\n            ModifiedStat: EffectStat.Armor,\n            ModifierMode: EffectModifierMode.Percent);\n        MonsterDefinition banner = Monster(\n            \"TEST_BANNER\",\n            abilityIds: [],\n            aiProfileId: \"BANNER_PASSIVE\",\n            autoAttackInterval: TimeSpan.FromSeconds(1));\n        EncounterDefinition encounter = new(\n            \"TEST_LINKED_OBJECT\",\n            \"TEST_BOSS\",\n            [\n                new EncounterPhaseDefinition(\n                    \"OPEN\",\n                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),\n                    [\n                        new EncounterActionDefinition(\n                            EncounterActionType.Summon,\n                            Summon: new SummonDefinition(\n                                banner.Id,\n                                Count: 1,\n                                MaxActive: 1,\n                                LinkToCaster: true,\n                                NoReward: true,\n                                IsCombatObject: true,\n                                AuraEffectId: aura.Id,\n                                AuraTargetSelector: EncounterTargetSelectors.Boss))\n                    ])\n            ]);\n        CombatSession session = Session(\n            bossAbilityIds: new HashSet<string>(StringComparer.Ordinal),\n            abilities: Abilities(strike),\n            bossAi: new MonsterAiProfile(\"BOSS_PASSIVE\", []),\n            bossAutoAttackInterval: TimeSpan.FromHours(1),\n            playerAbilityIds: new HashSet<string>([strike.Id], StringComparer.Ordinal));\n\n        session.ConfigureGenericEncounter(\n            encounter,\n            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)\n            {\n                [banner.Id] = new(banner, new MonsterAiProfile(\"BANNER_PASSIVE\", []))\n            },\n            new Dictionary<string, EffectDefinition>(StringComparer.Ordinal)\n            {\n                [aura.Id] = aura\n            });\n\n        CombatActorSnapshot objectSnapshot = Assert.Single(\n            session.Snapshot().Enemies!,\n            enemy => enemy.DefinitionId == banner.Id);\n        Assert.True(objectSnapshot.IsCombatObject);\n        Assert.False(objectSnapshot.RewardEligible);\n        Assert.False(objectSnapshot.AutoAttackEnabled);\n        Assert.Empty(objectSnapshot.KnownAbilityIds);\n        Assert.Contains(session.Snapshot().Enemy.Effects, effect => effect.Id == aura.Id);\n\n        CombatCommandResult destroyed = session.Handle(\n            new UseAbilityCommand(\"destroy-banner\", strike.Id, objectSnapshot.ActorId),\n            Now.AddMilliseconds(10));\n\n        Assert.True(destroyed.Succeeded, destroyed.ErrorCode);\n        Assert.Equal(0, session.Snapshot().Enemies!\n            .Single(enemy => enemy.ActorId == objectSnapshot.ActorId).Hp);\n        Assert.DoesNotContain(session.Snapshot().Enemy.Effects, effect => effect.Id == aura.Id);\n        Assert.DoesNotContain(session.GetEventsAfter(0), item =>\n            item.Type == CombatEventType.EnemyKilled\n            && item.TargetActorId == objectSnapshot.ActorId);\n    }\n\n    [Fact]\n    public void HpPhaseChangesBossAbilitySetBeforeItsNextAction()""",
        ),
    ],
}

for path, replacements in patches.items():
    text = path.read_text(encoding="utf-8")
    for index, (old, new) in enumerate(replacements, start=1):
        count = text.count(old)
        if count != 1:
            raise SystemExit(f"{path}: patch fragment {index} expected exactly once, found {count}.")
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8")
