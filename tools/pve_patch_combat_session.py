from pathlib import Path

path = Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs")
text = path.read_text(encoding="utf-8")

replacements = [
    (
        """        _enemyAiRuntimes = _enemies.ToDictionary(
            enemy => enemy.Actor.ActorId,
            enemy => new EnemyAiRuntime(
                enemyAiProfiles[enemy.Actor.ActorId],
                startedAtUtc + enemy.AutoAttack.Interval));""",
        """        _enemyAiRuntimes = _enemies.ToDictionary(
            enemy => enemy.Actor.ActorId,
            enemy => new EnemyAiRuntime(
                enemyAiProfiles[enemy.Actor.ActorId],
                startedAtUtc,
                startedAtUtc + enemy.AutoAttack.Interval));""",
    ),
    (
        """            _enemyAiRuntimes.Add(
                summoned.Actor.ActorId,
                new EnemyAiRuntime(
                    _summonProfile.AiProfile,
                    now + summoned.AutoAttack.Interval));""",
        """            _enemyAiRuntimes.Add(
                summoned.Actor.ActorId,
                new EnemyAiRuntime(
                    _summonProfile.AiProfile,
                    now,
                    now + summoned.AutoAttack.Interval));""",
    ),
    (
        """        foreach (string abilityId in aiRuntime.Profile.PriorityAbilityIds)
        {
            if (!enemy.KnownAbilityIds.Contains(abilityId)
                || !_abilities.TryGetValue(abilityId, out AbilityDefinition? ability))
            {
                continue;
            }

            Guid[] targetIds = ResolveEnemyAbilityTargetIds(enemy, ability, now);
            if (targetIds.Length == 0)
                continue;

            Dictionary<Guid, AbilityTargetModifier>? targetModifiers =
                ResolveEnemyAbilityTargetModifiers(ability, targetIds);

            string commandId = $"ai:{enemyActorId:N}:{Sequence + 1}:{abilityId}";
            AbilityExecutionResult execution = AbilityEngine.Execute(
                runtime,
                ability,
                new AbilityIntent(
                    commandId,
                    abilityId,
                    targetIds[0],
                    targetIds,
                    targetModifiers),
                now,
                _random);
            if (!execution.Succeeded)
                continue;

            ApplyKernelEvents(
                execution.Events,
                enemyActorId,
                targetIds[0],
                abilityId);
            Append(new CombatEvent(
                CombatEventType.AbilityUsed,
                now,
                enemyActorId,
                abilityId,
                SourceActorId: enemyActorId,
                TargetActorId: targetIds[0]));
            SyncArcherConditionalEffects(now);
            if (Status != CombatSessionStatus.Active || enemy.Actor.IsDead)
            {
                aiRuntime.NextActionAtUtc = null;
                return;
            }

            aiRuntime.NextActionAtUtc = runtime.ActiveCast?.ResolvesAtUtc
                ?? NextEnemyActionAfter(enemy, now);
            return;
        }""",
        """        AbilityTargetCandidate[] targetCandidates = BuildMonsterAiTargetCandidates(
            enemy,
            now,
            out Guid? currentThreatTargetId);
        HashSet<string> failedAbilityIds = new(StringComparer.Ordinal);
        while (true)
        {
            MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
                aiRuntime.Profile,
                enemy.Actor,
                enemy.KnownAbilityIds,
                _abilities,
                targetCandidates,
                aiRuntime.StartedAtUtc,
                now,
                aiRuntime.SchedulerState,
                _random,
                currentThreatTargetId,
                excludedAbilityIds: failedAbilityIds);
            if (decision is null)
                break;

            AbilityDefinition ability = decision.Ability;
            string abilityId = ability.Id;
            Guid[] targetIds = decision.TargetActorIds.ToArray();
            Dictionary<Guid, AbilityTargetModifier>? targetModifiers =
                ResolveEnemyAbilityTargetModifiers(ability, targetIds);

            string commandId = $"ai:{enemyActorId:N}:{Sequence + 1}:{abilityId}";
            AbilityExecutionResult execution = AbilityEngine.Execute(
                runtime,
                ability,
                new AbilityIntent(
                    commandId,
                    abilityId,
                    targetIds[0],
                    targetIds,
                    targetModifiers),
                now,
                _random);
            if (!execution.Succeeded)
            {
                failedAbilityIds.Add(abilityId);
                continue;
            }

            aiRuntime.SchedulerState.MarkExecuted(
                decision.Rule,
                ability,
                now,
                _random);
            ApplyKernelEvents(
                execution.Events,
                enemyActorId,
                targetIds[0],
                abilityId);
            Append(new CombatEvent(
                CombatEventType.AbilityUsed,
                now,
                enemyActorId,
                abilityId,
                SourceActorId: enemyActorId,
                TargetActorId: targetIds[0]));
            SyncArcherConditionalEffects(now);
            if (Status != CombatSessionStatus.Active || enemy.Actor.IsDead)
            {
                aiRuntime.NextActionAtUtc = null;
                return;
            }

            aiRuntime.NextActionAtUtc = runtime.ActiveCast?.ResolvesAtUtc
                ?? NextEnemyActionAfter(enemy, now);
            return;
        }""",
    ),
    (
        """    private sealed class EnemyAiRuntime(
        MonsterAiProfile profile,
        DateTimeOffset nextActionAtUtc)
    {
        public MonsterAiProfile Profile { get; } = profile;
        public MonsterAiState State { get; set; } = MonsterAiState.InCombat;
        public DateTimeOffset? NextActionAtUtc { get; set; } = nextActionAtUtc;
    }""",
        """    private sealed class EnemyAiRuntime(
        MonsterAiProfile profile,
        DateTimeOffset startedAtUtc,
        DateTimeOffset nextActionAtUtc)
    {
        public MonsterAiProfile Profile { get; } = profile;
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;
        public MonsterAbilitySchedulerState SchedulerState { get; } = new();
        public MonsterAiState State { get; set; } = MonsterAiState.InCombat;
        public DateTimeOffset? NextActionAtUtc { get; set; } = nextActionAtUtc;
    }""",
    ),
]

for index, (old, new) in enumerate(replacements, start=1):
    count = text.count(old)
    if count != 1:
        raise SystemExit(
            f"Patch fragment {index} expected exactly once, found {count}."
        )
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8")
