using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private static readonly TimeSpan PersistentEncounterEffectDuration = TimeSpan.FromHours(24);
    private const int GenericEncounterExecutionLimit = 128;

    private EncounterDefinition? _genericEncounterDefinition;
    private EncounterPhaseRuntimeState? _genericEncounterState;
    private Dictionary<string, EncounterEnemyProfile> _genericEncounterEnemyProfiles =
        new(StringComparer.Ordinal);
    private Dictionary<string, EffectDefinition> _genericEncounterEffects =
        new(StringComparer.Ordinal);
    private LinkedSummonRegistry? _genericEncounterSummons;
    private readonly List<EncounterPlannedAction> _genericEncounterPendingActions = [];
    private bool _processingGenericEncounter;

    public void ConfigureGenericEncounter(
        EncounterDefinition definition,
        IReadOnlyDictionary<string, EncounterEnemyProfile> enemyProfiles,
        IReadOnlyDictionary<string, EffectDefinition> effects)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(enemyProfiles);
        ArgumentNullException.ThrowIfNull(effects);
        if (_genericEncounterDefinition is not null)
            throw new InvalidOperationException("A generic encounter is already configured for this combat session.");
        if (Status != CombatSessionStatus.Active)
            throw new InvalidOperationException("Generic encounter can only be configured for an active combat session.");
        if (!string.Equals(definition.MonsterId, _primaryEnemy.DefinitionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Encounter '{definition.Id}' targets monster '{definition.MonsterId}', not primary enemy '{_primaryEnemy.DefinitionId}'.",
                nameof(definition));
        }

        IReadOnlyList<string> validationErrors = EncounterDefinitionValidator.Validate(definition);
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                $"Encounter '{definition.Id}' is invalid: {string.Join(" | ", validationErrors)}",
                nameof(definition));
        }

        HashSet<string> phaseIds = definition.Phases
            .Select(phase => phase.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (EncounterPhaseDefinition phase in definition.Phases)
        {
            foreach (string abilityId in phase.AbilityIds ?? [])
                EnsureGenericEncounterAbilityExists(definition.Id, abilityId);

            foreach (EncounterActionDefinition action in phase.Actions)
            {
                if (action.Type == EncounterActionType.Summon
                    && action.Summon is { } summon
                    && !enemyProfiles.ContainsKey(summon.MonsterId))
                {
                    throw new ArgumentException(
                        $"Encounter '{definition.Id}' has no runtime profile for summon '{summon.MonsterId}'.",
                        nameof(enemyProfiles));
                }
                if (action.Type == EncounterActionType.Summon
                    && action.Summon?.AuraEffectId is { } auraEffectId
                    && !effects.ContainsKey(auraEffectId))
                {
                    throw new ArgumentException(
                        $"Encounter '{definition.Id}' has no runtime aura effect '{auraEffectId}'.",
                        nameof(effects));
                }
                if (action.Type == EncounterActionType.ApplyEffect
                    && action.EffectId is { } effectId
                    && !effects.ContainsKey(effectId))
                {
                    throw new ArgumentException(
                        $"Encounter '{definition.Id}' has no runtime effect '{effectId}'.",
                        nameof(effects));
                }
                if (action.Type == EncounterActionType.ChangeAbilitySet)
                {
                    foreach (string abilityId in action.AbilityIds ?? [])
                        EnsureGenericEncounterAbilityExists(definition.Id, abilityId);
                }
                if (action.Type == EncounterActionType.SetPhase
                    && action.PhaseId is { } phaseId
                    && !phaseIds.Contains(phaseId))
                {
                    throw new ArgumentException(
                        $"Encounter '{definition.Id}' references unknown phase '{phaseId}'.",
                        nameof(definition));
                }
            }
        }

        _genericEncounterDefinition = definition;
        _genericEncounterState = new EncounterPhaseRuntimeState();
        _genericEncounterEnemyProfiles = enemyProfiles.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        _genericEncounterEffects = effects.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        _genericEncounterSummons = new LinkedSummonRegistry();
        _genericEncounterPendingActions.Clear();

        ProcessGenericEncounterDue(CurrentTimeUtc);
    }

    private DateTimeOffset? NextGenericEncounterDueAtUtc
    {
        get
        {
            if (_genericEncounterDefinition is null
                || _genericEncounterState is null
                || _primaryEnemy.Actor.IsDead)
            {
                return null;
            }

            DateTimeOffset? next = _genericEncounterPendingActions.Count == 0
                ? null
                : _genericEncounterPendingActions.Min(action => action.ExecuteAtUtc);

            if (_genericEncounterSummons is not null)
            {
                DateTimeOffset? nextExpiry = _genericEncounterSummons.Active
                    .Where(item => item.ExpiresAtUtc.HasValue)
                    .Select(item => item.ExpiresAtUtc)
                    .Min();
                next = Min(next, nextExpiry);
            }

            DateTimeOffset startedAtUtc = _enemyAiRuntimes[_primaryEnemyActorId].StartedAtUtc;
            foreach (EncounterPhaseDefinition phase in _genericEncounterDefinition.Phases)
            {
                if (phase.Trigger.Type != EncounterTriggerType.ElapsedTime
                    || phase.Trigger.Elapsed is not { } elapsed
                    || phase.Trigger.Once && _genericEncounterState.HasFired(phase.Id)
                    || phase.Trigger.PhaseId is { } requiredPhaseId
                        && !string.Equals(
                            requiredPhaseId,
                            _genericEncounterState.CurrentPhaseId,
                            StringComparison.Ordinal))
                {
                    continue;
                }

                DateTimeOffset phaseDue = startedAtUtc + elapsed;
                next = Min(next, phaseDue < CurrentTimeUtc ? CurrentTimeUtc : phaseDue);
            }

            return next is { } value && value < CurrentTimeUtc
                ? CurrentTimeUtc
                : next;
        }
    }

    private void ProcessGenericEncounterDue(DateTimeOffset now) =>
        ExecuteGenericEncounterCycle(now, eventType: null, eventDefinitionId: null);

    private void ProcessGenericEncounterEvent(CombatEvent combatEvent)
    {
        if (_genericEncounterDefinition is null
            || _genericEncounterState is null
            || _processingGenericEncounter)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.ActorDied
            && combatEvent.ActorId == _primaryEnemyActorId)
        {
            DespawnGenericEncounterSummonsForOwnerDeath(
                _primaryEnemyActorId,
                combatEvent.OccurredAtUtc);
            _genericEncounterPendingActions.Clear();
            return;
        }

        EncounterTriggerType? eventType = null;
        string? eventDefinitionId = combatEvent.DefinitionId;
        if (combatEvent.Type == CombatEventType.ActorDied
            && _enemiesById.TryGetValue(
                combatEvent.ActorId,
                out CombatParticipantDefinition? deadEnemy)
            && combatEvent.ActorId != _primaryEnemyActorId)
        {
            eventType = EncounterTriggerType.AddDeath;
            eventDefinitionId = deadEnemy.DefinitionId;
            if (_genericEncounterSummons?.TryRemove(
                    combatEvent.ActorId,
                    out LinkedSummonRegistration? removed) == true
                && removed is not null)
            {
                RemoveLinkedSummonAura(removed, combatEvent.OccurredAtUtc);
            }
        }
        else if (combatEvent.Type == CombatEventType.EffectExpired)
        {
            eventType = EncounterTriggerType.EffectExpired;
        }
        else if (combatEvent.Type == CombatEventType.AbilityInterrupted)
        {
            eventType = EncounterTriggerType.CastInterrupted;
        }

        if (_primaryEnemy.Actor.IsDead)
            return;

        ExecuteGenericEncounterCycle(
            combatEvent.OccurredAtUtc,
            eventType,
            eventDefinitionId);
    }

    private void ExecuteGenericEncounterCycle(
        DateTimeOffset now,
        EncounterTriggerType? eventType,
        string? eventDefinitionId)
    {
        if (_genericEncounterDefinition is null
            || _genericEncounterState is null
            || _processingGenericEncounter
            || Status != CombatSessionStatus.Active
            || _primaryEnemy.Actor.IsDead)
        {
            return;
        }

        _processingGenericEncounter = true;
        try
        {
            EncounterTriggerType? pendingEventType = eventType;
            string? pendingEventDefinitionId = eventDefinitionId;
            for (var iteration = 0; iteration < GenericEncounterExecutionLimit; iteration++)
            {
                bool progressed = ExpireGenericEncounterSummons(now);
                int pendingBefore = _genericEncounterPendingActions.Count;
                PlanGenericEncounterActions(
                    now,
                    pendingEventType,
                    pendingEventDefinitionId);
                pendingEventType = null;
                pendingEventDefinitionId = null;
                progressed |= _genericEncounterPendingActions.Count != pendingBefore;

                EncounterPlannedAction[] dueActions = _genericEncounterPendingActions
                    .Where(item => item.ExecuteAtUtc <= now)
                    .OrderBy(item => item.ExecuteAtUtc)
                    .ThenBy(item => item.PhaseId, StringComparer.Ordinal)
                    .ToArray();
                if (dueActions.Length == 0)
                {
                    if (!progressed)
                        return;
                    continue;
                }

                foreach (EncounterPlannedAction planned in dueActions)
                {
                    _genericEncounterPendingActions.Remove(planned);
                    ExecuteGenericEncounterAction(planned);
                    if (Status != CombatSessionStatus.Active || _primaryEnemy.Actor.IsDead)
                        return;
                }
            }

            throw new InvalidOperationException(
                $"Encounter '{_genericEncounterDefinition.Id}' exceeded the execution safety limit.");
        }
        finally
        {
            _processingGenericEncounter = false;
        }
    }

    private void PlanGenericEncounterActions(
        DateTimeOffset now,
        EncounterTriggerType? eventType,
        string? eventDefinitionId)
    {
        if (_genericEncounterDefinition is null || _genericEncounterState is null)
            return;

        Dictionary<string, int>? activeSummons = _genericEncounterSummons?.Active
            .GroupBy(item => item.MonsterId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        EncounterRuntimeSnapshot snapshot = new(
            _primaryEnemy.Actor.CurrentHp,
            _primaryEnemy.Actor.MaxHp,
            _primaryEnemy.Actor.CurrentResource,
            _primaryEnemy.Actor.MaxResource,
            _enemyAiRuntimes[_primaryEnemyActorId].StartedAtUtc,
            now,
            _genericEncounterState.CurrentPhaseId,
            eventDefinitionId,
            eventType);
        _genericEncounterPendingActions.AddRange(EncounterActionPlanner.Plan(
            _genericEncounterDefinition,
            snapshot,
            _genericEncounterState,
            activeSummons));
    }

    private bool ExpireGenericEncounterSummons(DateTimeOffset now)
    {
        if (_genericEncounterSummons is null)
            return false;

        IReadOnlyList<LinkedSummonRegistration> expired =
            _genericEncounterSummons.CollectExpired(now);
        foreach (LinkedSummonRegistration summon in expired)
        {
            RemoveLinkedSummonAura(summon, summon.ExpiresAtUtc ?? now);
            DeactivateEncounterEnemy(
                summon.ActorId,
                summon.OwnerActorId,
                summon.ExpiresAtUtc ?? now,
                summon.MonsterId);
        }
        return expired.Count > 0;
    }

    private void DespawnGenericEncounterSummonsForOwnerDeath(
        Guid ownerActorId,
        DateTimeOffset now)
    {
        if (_genericEncounterSummons is null)
            return;

        foreach (LinkedSummonRegistration summon in
                 _genericEncounterSummons.CollectForOwnerDeath(ownerActorId))
        {
            RemoveLinkedSummonAura(summon, now);
            DeactivateEncounterEnemy(
                summon.ActorId,
                ownerActorId,
                now,
                summon.MonsterId);
        }
    }

    private void ExecuteGenericEncounterAction(EncounterPlannedAction planned)
    {
        EncounterActionDefinition action = planned.Action;
        DateTimeOffset now = planned.ExecuteAtUtc;
        switch (action.Type)
        {
            case EncounterActionType.Summon:
                ExecuteGenericSummon(action, now);
                break;
            case EncounterActionType.ApplyEffect:
                ExecuteGenericApplyEffect(action, now);
                break;
            case EncounterActionType.ChangeAbilitySet:
                ExecuteGenericAbilitySet(action);
                break;
            case EncounterActionType.Shield:
                ExecuteGenericShield(planned, now);
                break;
            case EncounterActionType.Vulnerability:
                ExecuteGenericVulnerability(planned, now);
                break;
            case EncounterActionType.ResourceChange:
                ExecuteGenericResourceChange(action, now);
                break;
            case EncounterActionType.SetPhase:
                _genericEncounterState!.SetCurrentPhase(action.PhaseId!);
                break;
            default:
                throw new InvalidOperationException(
                    $"Encounter action '{action.Type}' is not supported by the combat runtime.");
        }
    }

    private void ExecuteGenericSummon(EncounterActionDefinition action, DateTimeOffset now)
    {
        SummonDefinition summon = action.Summon
            ?? throw new InvalidOperationException("Encounter summon action has no summon definition.");
        EncounterEnemyProfile profile = _genericEncounterEnemyProfiles[summon.MonsterId];
        LinkedSummonRegistry registry = _genericEncounterSummons
            ?? throw new InvalidOperationException("Encounter summon registry is not configured.");

        for (var index = 0; index < summon.Count; index++)
        {
            CombatParticipantDefinition spawned = SpawnEncounterEnemy(
                profile,
                _primaryEnemyActorId,
                now,
                summon.IsCombatObject,
                rewardEligible: !summon.NoReward);
            if (summon.InitialHpPercent < 100)
            {
                decimal missingHp = spawned.Actor.MaxHp
                    * (100m - summon.InitialHpPercent)
                    / 100m;
                spawned.Actor.ApplyDamage(missingHp);
            }
            Guid[] auraTargetActorIds = string.IsNullOrWhiteSpace(summon.AuraEffectId)
                ? []
                : ResolveGenericEncounterTargets(summon.AuraTargetSelector, now)
                    .Select(target => target.ActorId)
                    .ToArray();
            LinkedSummonRegistration registration = registry.Register(
                spawned.Actor.ActorId,
                _primaryEnemyActorId,
                summon with { Count = 1 },
                now,
                auraTargetActorIds);
            ApplyLinkedSummonAura(registration, now);
        }
    }

    private void ApplyLinkedSummonAura(
        LinkedSummonRegistration registration,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(registration.AuraEffectId)
            || registration.AuraTargetActorIds.Count == 0)
        {
            return;
        }

        EffectDefinition source = _genericEncounterEffects[registration.AuraEffectId];
        TimeSpan duration = registration.ExpiresAtUtc is { } expiresAtUtc
            ? expiresAtUtc - now
            : PersistentEncounterEffectDuration;
        if (duration <= TimeSpan.Zero)
            return;

        EffectDefinition aura = source with
        {
            Duration = duration
        };
        foreach (Guid targetActorId in registration.AuraTargetActorIds)
        {
            CombatActorState? target = ResolveCombatActor(targetActorId);
            if (target is null || target.IsDead)
                continue;
            ApplyKernelEvents(
                EffectEngine.Apply(target, registration.ActorId, aura, now),
                registration.ActorId,
                target.ActorId,
                aura.Id);
        }
    }

    private void RemoveLinkedSummonAura(
        LinkedSummonRegistration registration,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(registration.AuraEffectId))
            return;

        foreach (Guid targetActorId in registration.AuraTargetActorIds)
        {
            CombatActorState? target = ResolveCombatActor(targetActorId);
            if (target is null)
                continue;
            ApplyKernelEvents(
                EffectEngine.RemoveOwned(
                    target,
                    registration.AuraEffectId,
                    registration.ActorId,
                    now),
                registration.ActorId,
                target.ActorId,
                registration.AuraEffectId);
        }
    }

    private void ExecuteGenericApplyEffect(
        EncounterActionDefinition action,
        DateTimeOffset now)
    {
        EffectDefinition source = _genericEncounterEffects[action.EffectId!];
        EffectDefinition effect = source with
        {
            Duration = action.Duration ?? source.Duration,
            Magnitude = action.Magnitude == 0 ? source.Magnitude : action.Magnitude
        };
        ApplyGenericEncounterEffect(effect, action.TargetSelector, now);
    }

    private void ExecuteGenericShield(
        EncounterPlannedAction planned,
        DateTimeOffset now)
    {
        EffectDefinition shield = new(
            $"ENCOUNTER_{_genericEncounterDefinition!.Id}_{planned.PhaseId}_SHIELD",
            EffectKind.Shield,
            planned.Action.Duration ?? PersistentEncounterEffectDuration,
            1,
            EffectStackPolicy.Replace,
            planned.Action.Magnitude);
        ApplyGenericEncounterEffect(shield, planned.Action.TargetSelector, now);
    }

    private void ExecuteGenericVulnerability(
        EncounterPlannedAction planned,
        DateTimeOffset now)
    {
        EffectDefinition vulnerability = new(
            $"ENCOUNTER_{_genericEncounterDefinition!.Id}_{planned.PhaseId}_VULNERABILITY",
            EffectKind.StatModifier,
            planned.Action.Duration ?? PersistentEncounterEffectDuration,
            1,
            EffectStackPolicy.Replace,
            planned.Action.Magnitude,
            ModifiedStat: EffectStat.IncomingDamageMultiplier,
            ModifierMode: EffectModifierMode.Percent);
        ApplyGenericEncounterEffect(
            vulnerability,
            planned.Action.TargetSelector,
            now);
    }

    private void ApplyGenericEncounterEffect(
        EffectDefinition effect,
        string? targetSelector,
        DateTimeOffset now)
    {
        foreach (CombatActorState target in ResolveGenericEncounterTargets(targetSelector, now))
        {
            ApplyKernelEvents(
                EffectEngine.Apply(
                    target,
                    _primaryEnemyActorId,
                    effect,
                    now),
                _primaryEnemyActorId,
                target.ActorId,
                effect.Id);
        }
    }

    private void ExecuteGenericResourceChange(
        EncounterActionDefinition action,
        DateTimeOffset now)
    {
        foreach (CombatActorState target in ResolveGenericEncounterTargets(
                     action.TargetSelector,
                     now))
        {
            ChangeEncounterResource(
                target,
                action.ResourceAmount,
                _primaryEnemyActorId,
                now,
                _genericEncounterDefinition!.Id);
        }
    }

    private void ExecuteGenericAbilitySet(EncounterActionDefinition action)
    {
        if (!string.IsNullOrWhiteSpace(action.TargetSelector)
            && !string.Equals(
                action.TargetSelector,
                EncounterTargetSelectors.Boss,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ChangeAbilitySet currently targets the primary boss only.");
        }

        HashSet<string> abilityIds = new(
            action.AbilityIds ?? [],
            StringComparer.Ordinal);
        foreach (string abilityId in abilityIds)
            EnsureGenericEncounterAbilityExists(_genericEncounterDefinition!.Id, abilityId);

        CombatParticipantDefinition current = _enemiesById[_primaryEnemyActorId];
        CombatParticipantDefinition updated = current with { KnownAbilityIds = abilityIds };
        _enemiesById[_primaryEnemyActorId] = updated;
        int index = _enemies.FindIndex(enemy => enemy.Actor.ActorId == _primaryEnemyActorId);
        if (index < 0)
            throw new InvalidOperationException("Primary encounter enemy is missing from the roster.");
        _enemies[index] = updated;
    }

    private CombatActorState[] ResolveGenericEncounterTargets(
        string? selector,
        DateTimeOffset now)
    {
        string resolvedSelector = string.IsNullOrWhiteSpace(selector)
            ? EncounterTargetSelectors.Boss
            : selector;
        CombatParticipantDefinition[] party = ActiveEnemyTargetCandidates();

        return resolvedSelector switch
        {
            EncounterTargetSelectors.Boss => [_primaryEnemy.Actor],
            EncounterTargetSelectors.CurrentTarget => party.Length == 0
                ? []
                : [ResolveEnemyPrimaryTarget(_primaryEnemy, now).Actor],
            EncounterTargetSelectors.AllPartyMembers => party
                .Select(item => item.Actor)
                .ToArray(),
            EncounterTargetSelectors.LowestHpPartyMember => party.Length == 0
                ? []
                : [party
                    .OrderBy(item => item.Actor.CurrentHp / item.Actor.MaxHp)
                    .ThenBy(item => item.Actor.ActorId)
                    .First()
                    .Actor],
            EncounterTargetSelectors.RandomPartyMember => ResolveRandomPartyTarget(party),
            _ => throw new InvalidOperationException(
                $"Encounter target selector '{resolvedSelector}' is not supported.")
        };
    }

    private CombatActorState[] ResolveRandomPartyTarget(CombatParticipantDefinition[] party)
    {
        if (party.Length == 0)
            return [];

        int index = Math.Min(
            party.Length - 1,
            (int)Math.Floor(_random.NextUnit() * party.Length));
        return [party[index].Actor];
    }

    private Guid? ResolveGenericEncounterOwnerTarget(Guid actorId)
    {
        if (_genericEncounterSummons is null
            || !_genericEncounterSummons.TryGet(
                actorId,
                out LinkedSummonRegistration? registration)
            || registration is null
            || !registration.LinkToOwner)
        {
            return null;
        }

        return registration.OwnerActorId;
    }

    private void EnsureGenericEncounterAbilityExists(string encounterId, string abilityId)
    {
        if (!_abilities.ContainsKey(abilityId))
        {
            throw new ArgumentException(
                $"Encounter '{encounterId}' references unknown ability '{abilityId}'.");
        }
    }
}
