using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Participants;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly Dictionary<Guid, Guid> _openWorldCompanionOwners = [];

    private void AddActorToCombatRuntimes(CombatActorState actor)
    {
        var runtimes = _playerStatesByActorId.Values.Select(state => state.Runtime)
            .Concat(_enemyRuntimes.Values)
            .Concat(_primaryCompanionRuntime is null ? [] : [_primaryCompanionRuntime])
            .Concat(_playerStatesByActorId.Values.Where(state => state.CompanionRuntime is not null)
                .Select(state => state.CompanionRuntime!)).Distinct();
        foreach (var runtime in runtimes)
            if (!runtime.Actors.ContainsKey(actor.ActorId)) runtime.AddActor(actor);
    }

    private IEnumerable<CombatParticipantDefinition> SessionCompanions() => IsOpenWorldPvp
        ? _playerStatesByActorId.Values.Where(state => state.Companion is not null).Select(state => state.Companion!)
        : _primaryCompanion is null ? [] : [_primaryCompanion];

    private CombatParticipantDefinition? CompanionFor(CombatPlayerRuntimeState state) =>
        IsOpenWorldPvp ? state.Companion : _primaryCompanion;

    private CombatRuntimeState? CompanionRuntimeFor(CombatPlayerRuntimeState state) =>
        IsOpenWorldPvp ? state.CompanionRuntime : _primaryCompanionRuntime;

    private IEnumerable<CombatParticipantDefinition> ActiveSessionCompanions() => SessionCompanions()
        .Where(pet => !pet.Actor.IsDead && (!IsOpenWorldPvp
            || _openWorldCompanionOwners.TryGetValue(pet.Actor.ActorId, out var owner)
            && _participantRoster.GetStatus(owner) == CombatParticipantStatus.Active));

    private void ForEachSessionCompanion(Action action)
    {
        if (!IsOpenWorldPvp) { action(); return; }
        var previous = _activePlayerState;
        try
        {
            foreach (var state in _playerStatesByActorId.Values)
            {
                if (state.Companion is null || _participantRoster.GetStatus(state.Definition.Actor.ActorId) != CombatParticipantStatus.Active)
                    continue;
                _activePlayerState = state;
                action();
            }
        }
        finally { _activePlayerState = previous; }
    }

    private void InitializeOpenWorldCompanion(CombatPlayerRuntimeState state, DateTimeOffset now)
    {
        if (state.Companion is not { } pet) return;
        _openWorldCompanionOwners[pet.Actor.ActorId] = state.Definition.Actor.ActorId;
        state.CompanionRuntime = CreateRuntime(pet.Actor, _combatTargetsById.Values.Select(actor => actor.Actor)
            .Concat(SessionCompanions().Select(companion => companion.Actor)));
        _combatTargetsById.Add(pet.Actor.ActorId, pet);
        _enemyRuntimes.Add(pet.Actor.ActorId, state.CompanionRuntime);
        AddActorToCombatRuntimes(pet.Actor);
        foreach (var monster in _enemies)
            _enemyThreatTables[monster.Actor.ActorId].AddThreat(pet.Actor.ActorId, 1);
        state.CompanionNextAutoAttackAtUtc = pet.CanAutoAttack ? now + pet.AutoAttack.Interval : null;
    }
}
