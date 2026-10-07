using Elyndor.Core.Combat.SetPassives;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private SetPassiveCombatRuntime _setPassiveRuntime = null!;
    private IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> _setPassiveLoadoutSnapshot =
        new Dictionary<Guid, IReadOnlyDictionary<string, int>>();

    /// <summary>
    /// Equipped set pieces are captured once, when the session is created: equipment cannot
    /// change while a combat session is active, and the roster is fixed by the constructor.
    /// </summary>
    private void InitializeSetPassiveLoadoutSnapshot()
    {
        _setPassiveLoadoutSnapshot = _playerStatesByActorId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, int>)(pair.Value.Definition.EquippedSetPieces is { } setPieces
                ? setPieces.ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal)));
        _setPassiveRuntime = new SetPassiveCombatRuntime(
            SetPassiveCatalog.Definitions.Concat(_playerStatesByActorId.Values
                .SelectMany(state => state.Definition.SetPassives ?? []))
                .DistinctBy(effect => effect.Id),
            _setPassiveLoadoutSnapshot,
            id => _playerStatesByActorId.TryGetValue(id, out var state) ? state.Definition.Actor
                : _enemiesById.TryGetValue(id, out var enemy) ? enemy.Actor
                : _companion?.Actor.ActorId == id ? _companion.Actor : null,
            _abilities,
            id => _playerStatesByActorId.TryGetValue(id, out var state) ? state.Runtime.Cooldowns : null,
            _companion is null ? null : new Dictionary<Guid, Guid> { [_companion.Actor.ActorId] = _companionOwnerActorId });
    }

    private void ApplySetPassiveHooks(CombatEvent combatEvent)
    {
        foreach (CombatEvent effectEvent in _setPassiveRuntime.Process(combatEvent, _procGuard))
            ApplyKernelEvents([effectEvent], effectEvent.SourceActorId ?? effectEvent.ActorId,
                effectEvent.TargetActorId ?? effectEvent.ActorId, effectEvent.DefinitionId);
    }
}
