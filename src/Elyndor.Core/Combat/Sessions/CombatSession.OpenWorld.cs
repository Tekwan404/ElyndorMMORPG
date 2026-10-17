using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Contribution;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Pvp;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private sealed class HostileTargetView(CombatSession session, Guid ownerId)
        : IReadOnlyDictionary<Guid, CombatParticipantDefinition>
    {
        public IEnumerable<CombatParticipantDefinition> Values => session.HostileParticipants(ownerId);
        public IEnumerable<Guid> Keys => Values.Select(value => value.Actor.ActorId);
        public int Count => Values.Count();
        public CombatParticipantDefinition this[Guid key] => TryGetValue(key, out var value)
            ? value : throw new KeyNotFoundException();
        public bool ContainsKey(Guid key) => session.IsHostileTarget(ownerId, key) && session.IsCombatTargetAvailable(key);
        public bool TryGetValue(Guid key, out CombatParticipantDefinition value)
        {
            if (ContainsKey(key) && session._combatTargetsById.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }
            value = null!;
            return false;
        }
        public IEnumerator<KeyValuePair<Guid, CombatParticipantDefinition>> GetEnumerator() =>
            Values.Select(value => new KeyValuePair<Guid, CombatParticipantDefinition>(value.Actor.ActorId, value)).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static readonly ParticipationPolicy OpenWorldParticipationPolicy = new(
        TimeSpan.Zero, minimumQualifyingActions: 1, minimumContributionScore: 1,
        eligibilityMode: ParticipationEligibilityMode.ContributionAndTime);

    private IEnumerable<CombatParticipantDefinition> HostileParticipants(Guid actorId) =>
        _enemies.Concat(IsOpenWorldPvp
            ? _playerStatesByActorId.Values.Where(state => IsHostileTarget(actorId, state.Definition.Actor.ActorId) && IsCombatTargetAvailable(state.Definition.Actor.ActorId))
                .Select(state => state.Definition)
                .Concat(ActiveSessionCompanions().Where(pet => IsHostileTarget(actorId, pet.Actor.ActorId)))
            : []);

    private bool IsCombatTargetAvailable(Guid actorId)
    {
        if (!IsOpenWorldPvp) return true;
        if (_openWorldCompanionOwners.TryGetValue(actorId, out var owner))
            return _participantRoster.GetStatus(owner) == CombatParticipantStatus.Active;
        return !_playerStatesByActorId.ContainsKey(actorId)
            || _participantRoster.GetStatus(actorId) is CombatParticipantStatus.Active or CombatParticipantStatus.Dead;
    }

    private bool AreFactionAllies(Guid firstId, Guid secondId)
    {
        if (firstId == secondId) return true;
        if (!IsOpenWorldPvp) return true;
        firstId = _openWorldCompanionOwners.GetValueOrDefault(firstId, firstId);
        secondId = _openWorldCompanionOwners.GetValueOrDefault(secondId, secondId);
        return _playerStatesByActorId.TryGetValue(firstId, out var first)
            && _playerStatesByActorId.TryGetValue(secondId, out var second)
            && first.Definition.RaceId == second.Definition.RaceId;
    }

    private bool IsHostileTarget(Guid sourceId, Guid targetId)
    {
        if (!IsOpenWorldPvp) return _enemiesById.ContainsKey(targetId);
        if (sourceId == targetId) return false;
        sourceId = _openWorldCompanionOwners.GetValueOrDefault(sourceId, sourceId);
        targetId = _openWorldCompanionOwners.GetValueOrDefault(targetId, targetId);
        bool sourcePlayer = _playerStatesByActorId.ContainsKey(sourceId);
        bool targetPlayer = _playerStatesByActorId.ContainsKey(targetId);
        if (sourcePlayer && targetPlayer) return !AreFactionAllies(sourceId, targetId);
        return sourcePlayer != targetPlayer
            && (sourcePlayer ? _enemiesById.ContainsKey(targetId) : _enemiesById.ContainsKey(sourceId));
    }

    private bool AreCapturedPartyAllies(Guid firstId, Guid secondId)
    {
        if (!IsOpenWorldPvp || firstId == secondId) return true;
        return AreFactionAllies(firstId, secondId)
            && _playerStatesByActorId.TryGetValue(firstId, out var first)
            && _playerStatesByActorId.TryGetValue(secondId, out var second)
            && first.PartyId is { } partyId && second.PartyId == partyId;
    }

    private IEnumerable<Guid> ActivePartyActorIds() => ActivePlayerActorIds()
        .Where(id => AreCapturedPartyAllies(_player.Actor.ActorId, id));

    public bool TryJoinOpenWorldParticipant(CombatPlayerDefinition definition, DateTimeOffset now,
        out string? errorCode)
    {
        errorCode = CombatErrorCodes.CommandRejected;
        if (!IsOpenWorldPvp || definition.Participant.Kind != CombatActorKind.Player
            || definition.Participant.RaceId is not ("HUMAN" or "UNDEAD")
            || definition.Participant.Actor.IsDead || definition.AccountId == Guid.Empty)
            return false;
        AdvanceTo(now);
        if (Status != CombatSessionStatus.Active) { errorCode = CombatErrorCodes.Ended; return false; }
        Guid id = definition.Participant.Actor.ActorId;
        if (_playerStatesByActorId.ContainsKey(id))
        {
            errorCode = CombatErrorCodes.AlreadyActive;
            return false;
        }
        ValidateAutoAttack(definition.Participant.AutoAttack);
        if (definition.Participant.OffHandAutoAttack is { } offHand) ValidateAutoAttack(offHand);
        if (_combatTargetsById.ContainsKey(id) || !_participantRoster.TryAdd(new(definition.AccountId, id, id), now))
        {
            errorCode = "world_pvp_combat_full";
            return false;
        }
        var runtime = CreateRuntime(definition.Participant.Actor,
            _combatTargetsById.Values.Select(participant => participant.Actor)
                .Concat(_companion is null ? [] : [_companion.Actor]));
        var state = new CombatPlayerRuntimeState(definition, runtime)
        {
            LastResourceRegenAtUtc = now,
            AutoAttackEnabled = false
        };
        state.InitializeTalentRuntime(_random, _procGuard);
        ConfigureOpenWorldControl(state.Definition.Actor);
        foreach (var cooldown in definition.InitialCooldowns ?? new Dictionary<string, DateTimeOffset>())
            if (cooldown.Value > now) runtime.Cooldowns.Add(cooldown.Key, cooldown.Value);
        AddActorToCombatRuntimes(definition.Participant.Actor);
        _playerStatesByActorId.Add(id, state);
        _combatTargetsById.Add(id, definition.Participant);
        _enemyRuntimes.Add(id, runtime);
        _enemyThreatTables.Add(id, new ThreatTable());
        _enemyForcedTargets.Add(id, new ForcedTargetState());
        _statisticsByActorId.Add(id, new CombatSessionStatisticsState(now));
        _participantRoster.TryAttach(id, now, out _);
        _contributionLedger.Register(id, id, now);
        if (definition.Companion is { } pet)
        {
            _contributionLedger.RegisterOwnedActor(id, pet.Actor.ActorId);
            InitializeOpenWorldCompanion(state, now);
        }
        foreach (var monster in _enemies)
            if (!monster.Actor.IsDead) _enemyThreatTables[monster.Actor.ActorId].AddThreat(id, 1);
        state.SelectedTargetActorId = HostileParticipants(id).First(target => !target.Actor.IsDead).Actor.ActorId;
        _setPassiveRuntime.RegisterParticipant(id, definition.Participant.EquippedSetPieces
            ?? new Dictionary<string, int>());
        _itemSpecialEffectLoadoutSnapshot = _itemSpecialEffectLoadoutSnapshot
            .Append(new(id, definition.Participant.EquippedSpecialEffectIds ?? new HashSet<string>()))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var previous = _activePlayerState;
        try
        {
            _activePlayerState = state;
            ConfigureMageIncomingDamage();
            InitializePaladinLoadout(now);
            ConfigurePaladinHitAdmission();
            ApplyGuardianStartingEffects(now);
            ApplyWarlordPassiveEffects(now);
        }
        finally { _activePlayerState = previous; }
        Append(new(CombatEventType.ActorJoined, now, id, definition.Participant.DefinitionId, SourceActorId: id));
        errorCode = null;
        return true;
    }

    private static void ConfigureOpenWorldControl(CombatActorState actor)
    {
        CrowdControlDiminishingReturns diminishing = new();
        actor.EffectApplicationPolicy = (source, effect, now) =>
        {
            if (source == actor.ActorId || !CrowdControlCategoryResolver.TryResolve(effect.Kind, out var category))
                return new(effect);
            var resolution = diminishing.Resolve(category, effect.Duration, now);
            return resolution.IsImmune ? new(null) : new(effect with { Duration = resolution.EffectiveDuration },
                () => diminishing.Commit(resolution, now + resolution.EffectiveDuration));
        };
    }

    private bool IsPveContributionEvent(CombatEvent combatEvent)
    {
        Guid source = combatEvent.SourceActorId ?? combatEvent.ActorId;
        Guid target = combatEvent.TargetActorId ?? combatEvent.ActorId;
        bool targetMonster = _enemies.Any(monster => monster.Actor.ActorId == target && monster.RewardEligible);
        bool sourceMonster = _enemies.Any(monster => monster.Actor.ActorId == source && monster.RewardEligible);
        if (combatEvent.Type is CombatEventType.DamageDealt or CombatEventType.TauntApplied)
            return targetMonster;
        if (combatEvent.Type is CombatEventType.ShieldAbsorbed or CombatEventType.DamageBlocked)
            return sourceMonster;
        if (combatEvent.Type == CombatEventType.HealingApplied && AreFactionAllies(source, target))
            return _contributionLedger.TryGetSnapshot(target, out var contribution)
                && contribution!.DamageDealt + contribution.TankingContribution > 0;
        return false;
    }

    private void FinishOpenWorldIfResolved(CombatEvent terminalEvent)
    {
        Guid[] active = ActivePlayerActorIds().ToArray();
        if (active.Length == 0)
        {
            Status = CombatSessionStatus.Defeat;
            EndCombat(terminalEvent);
        }
        else if (_enemies.All(monster => monster.Actor.IsDead)
            && active.Select(id => _playerStatesByActorId[id].Definition.RaceId).Distinct().Count() == 1)
        {
            Status = CombatSessionStatus.Victory;
            EndCombat(terminalEvent);
        }
    }

    private CombatSessionStatus PerspectiveStatus(CombatPlayerRuntimeState state) =>
        IsOpenWorldPvp && state.Definition.Actor.IsDead && Status != CombatSessionStatus.Active
            ? CombatSessionStatus.Defeat : Status;

    private void RetargetOpenWorldPlayers(DateTimeOffset now)
    {
        foreach (var state in _playerStatesByActorId.Values)
        {
            var target = HostileParticipants(state.Definition.Actor.ActorId)
                .FirstOrDefault(candidate => !candidate.Actor.IsDead && candidate.Actor.IsTargetable(now));
            if (target is null || _combatTargetsById.TryGetValue(state.SelectedTargetActorId, out var current)
                && !current.Actor.IsDead && IsCombatTargetAvailable(current.Actor.ActorId)
                && current.Actor.IsTargetable(now)) continue;
            state.SelectedTargetActorId = target.Actor.ActorId;
            Append(new(CombatEventType.TargetChanged, now, state.Definition.Actor.ActorId,
                SourceActorId: state.Definition.Actor.ActorId, TargetActorId: target.Actor.ActorId));
        }
    }
}
