using Elyndor.Core.World;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly Dictionary<(Guid Victim, Guid Source), decimal> _worldPvpDamage = [];
    private readonly Dictionary<(Guid Ally, Guid Healer), (decimal Amount, DateTimeOffset AtUtc)> _worldPvpSupport = [];
    private readonly Dictionary<Guid, (decimal Debt, DateTimeOffset LastHit)> _worldPvpWounds = [];
    private readonly Dictionary<Guid, DateTimeOffset> _worldPvpFirstHit = [];
    private readonly List<WorldPvpDefeat> _worldPvpDefeats = [];

    private void RecordWorldPvpProgress(CombatEvent ev)
    {
        if (!IsOpenWorldPvp) return;
        Guid source = ev.SourceActorId ?? ev.ActorId;
        source = _openWorldCompanionOwners.GetValueOrDefault(source, source);
        Guid target = ev.TargetActorId ?? ev.ActorId;
        if (ev.Type == CombatEventType.DamageDealt
            && _playerStatesByActorId.ContainsKey(source)
            && _playerStatesByActorId.TryGetValue(target, out var victim)
            && IsHostileTarget(source, target))
        {
            decimal amount = Math.Clamp(ev.Amount, 0, victim.Definition.Actor.MaxHp);
            _worldPvpDamage[(target, source)] = _worldPvpDamage.GetValueOrDefault((target, source)) + amount;
            var oldWound = _worldPvpWounds.GetValueOrDefault(target);
            decimal debt = ev.OccurredAtUtc - oldWound.LastHit <= WorldPvpBalance.SupportWindow ? oldWound.Debt : 0;
            _worldPvpWounds[target] = (debt + amount, ev.OccurredAtUtc);
            _worldPvpFirstHit.TryAdd(target, ev.OccurredAtUtc);
        }
        if (ev.Type == CombatEventType.HealingApplied
            && _playerStatesByActorId.ContainsKey(source) && _playerStatesByActorId.ContainsKey(target)
            && AreFactionAllies(source, target) && _worldPvpWounds.TryGetValue(target, out var wound)
            && ev.OccurredAtUtc - wound.LastHit <= WorldPvpBalance.SupportWindow)
        {
            decimal useful = Math.Min(Math.Max(0, ev.Amount), wound.Debt);
            _worldPvpWounds[target] = (wound.Debt - useful, wound.LastHit);
            if (source != target && useful > 0)
            {
                var old = _worldPvpSupport.GetValueOrDefault((target, source));
                decimal previous = ev.OccurredAtUtc - old.AtUtc <= WorldPvpBalance.SupportWindow ? old.Amount : 0;
                _worldPvpSupport[(target, source)] = (previous + useful, ev.OccurredAtUtc);
            }
        }
        if (ev.Type != CombatEventType.ActorDied || !_playerStatesByActorId.TryGetValue(ev.ActorId, out var deceased)
            || _worldPvpDefeats.Any(d => d.VictimId == ev.ActorId)) return;
        var damage = _worldPvpDamage.Where(pair => pair.Key.Victim == ev.ActorId).ToArray();
        if (!damage.Any(hit => hit.Value > 0)) return;
        // A real PvP kill counts even when anti-farm rules deny rating and currency.
        // Use the same highest-damage attribution as reward victories, excluding fled actors.
        bool killedByPlayer = _playerStatesByActorId.ContainsKey(source)
            && IsHostileTarget(source, ev.ActorId);
        Guid? killerId = killedByPlayer ? damage
            .Where(pair => pair.Value > 0 && _participantRoster.GetStatus(pair.Key.Source)
                != Elyndor.Core.Combat.Participants.CombatParticipantStatus.Fled)
            .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key.Source)
            .Select(pair => (Guid?)pair.Key.Source).FirstOrDefault() : null;
        var contributions = new Dictionary<Guid, WorldPvpContribution>();
        bool qualified = _worldPvpFirstHit.TryGetValue(ev.ActorId, out var first)
            && ev.OccurredAtUtc - first >= WorldPvpBalance.MinimumFightDuration
            && (_worldPvpDamage.Any(pair => pair.Key.Source == ev.ActorId && pair.Value > 0)
                || _worldPvpSupport.Any(pair => pair.Key.Healer == ev.ActorId && pair.Value.Amount > 0
                    && ev.OccurredAtUtc - pair.Value.AtUtc <= WorldPvpBalance.SupportWindow))
            && damage.Sum(pair => pair.Value) >= deceased.Definition.Actor.MaxHp * WorldPvpBalance.MinimumDamageFraction;
        if (qualified)
        {
            foreach (var hit in damage.Where(pair => _participantRoster.GetStatus(pair.Key.Source)
                != Elyndor.Core.Combat.Participants.CombatParticipantStatus.Fled))
                contributions[hit.Key.Source] = new(hit.Key.Source, hit.Value, 0);
            foreach (var heal in _worldPvpSupport.Where(pair => contributions.ContainsKey(pair.Key.Ally)
                && ev.OccurredAtUtc - pair.Value.AtUtc <= WorldPvpBalance.SupportWindow
                && _participantRoster.GetStatus(pair.Key.Healer) != Elyndor.Core.Combat.Participants.CombatParticipantStatus.Fled).ToArray())
            {
                if (AreFactionAllies(heal.Key.Healer, ev.ActorId)) continue;
                var existing = contributions.GetValueOrDefault(heal.Key.Healer, new(heal.Key.Healer, 0, 0));
                contributions[heal.Key.Healer] = existing with { Support = existing.Support + Math.Min(heal.Value.Amount, contributions[heal.Key.Ally].Damage) };
                _worldPvpSupport.Remove(heal.Key);
            }
        }
        _worldPvpDefeats.Add(new(ev.ActorId, ev.OccurredAtUtc, contributions.Values.ToArray(), killerId));
    }
}
