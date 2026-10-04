using Elyndor.Core.Combat.Resources;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private CombatResourceRuntime? _resourceRuntime;

    // Read active participant rules on demand, including party activation and
    // nested events. Do not capture one player's build in a session-wide delegate.
    private CombatResourceRuntime ResourceRuntime => _resourceRuntime ??= new(
        Append, ScaleWarlordResource,
        id => _playerTalents.EventHooks.Any(hook => hook.TalentId == id),
        () => BaseRageFromDirectDamageTaken * GuardianRageMultiplier);

    private void AddResource(CombatActorState actor, decimal amount, DateTimeOffset now, string definitionId) =>
        ResourceRuntime.Grant(actor, amount, now, definitionId);

    private void ApplyPlayerResourceRegen(DateTimeOffset now)
    {
        foreach (CombatParticipantSnapshot participant in _participantRoster.Participants
                     .Where(item => item.Status == CombatParticipantStatus.Active))
        {
            ActivatePlayer(participant.CharacterId);
            if (now <= _lastPlayerResourceRegenAtUtc) continue;
            DateTimeOffset from = _lastPlayerResourceRegenAtUtc;
            // Time advancement remains session-owned, including dead-player cursors.
            _lastPlayerResourceRegenAtUtc = now;
            ResourceRuntime.Regenerate(_player.Actor, from, now,
                ResourceRegenRateAt, ResourceRegenBoundaries());
        }
    }

    private decimal ResourceRegenRateAt(DateTimeOffset time) =>
        EffectiveArcherResourceRegenPerSecond(EffectivePlayerResourceRegenPerSecond(time), time);

    // Existing Mage rules are an adapter, not a second elapsed-time integrator.
    private IEnumerable<DateTimeOffset> ResourceRegenBoundaries()
    {
        if (!IsMage) yield break;
        foreach (ActiveEffect effect in _player.Actor.ActiveEffects.Where(effect =>
                     effect.SourceId == _player.Actor.ActorId
                     && effect.Definition.Id == ClearcastingRegenEffectId))
        {
            yield return effect.AppliedAtUtc;
            yield return effect.ExpiresAtUtc;
        }
        if (TryGetMageHook("A-6-3", out ResolvedTalentEventHook meditation))
            yield return (_lastMageManaSpendAtUtc ?? _combatStartedAtUtc) + meditation.Duration;
    }
}
