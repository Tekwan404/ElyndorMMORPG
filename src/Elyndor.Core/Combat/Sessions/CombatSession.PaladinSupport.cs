using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly Dictionary<(Guid Beneficiary, string Talent), DateTimeOffset> _paladinJudgementReadyAt = [];

    private void ApplyPaladinJudgementSupportMarks(Guid targetId, DateTimeOffset now)
    {
        if (ResolvePaladinActor(targetId) is not { IsDead: false } target)
            return;
        foreach (string talentId in new[] { "H-3-3", "H-7-1" })
        {
            if (!TryGetPaladinHook(talentId, out var hook) || hook.Duration <= TimeSpan.Zero)
                continue;
            ApplyPaladinEffect(target, new EffectDefinition(hook.TargetId!, EffectKind.Debuff,
                hook.Duration, 1, EffectStackPolicy.Refresh, hook.Value, SourceSpecific: true), now);
        }
    }

    private void ProcessPaladinJudgementSupport(CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.DamageDealt || combatEvent.Amount < 0
            || combatEvent.IsPeriodic || combatEvent.IsProc || combatEvent.IsReflected
            || combatEvent.SourceActorId is not { } attackerId
            || combatEvent.TargetActorId is not { } targetId
            || !_playerStatesByActorId.TryGetValue(attackerId, out var attacker)
            || !ActivePlayerActorIds().Contains(attackerId)
            || !_enemiesById.TryGetValue(targetId, out var enemy))
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        CombatPlayerRuntimeState previous = _activePlayerState;
        try
        {
            foreach (string talentId in new[] { "H-3-3", "H-7-1" })
            {
                var key = (attackerId, talentId);
                if (_paladinJudgementReadyAt.TryGetValue(key, out var ready) && ready > now)
                    continue;
                string effectId = "PALADIN_" + talentId.Replace('-', '_');
                foreach (ActiveEffect mark in enemy.Actor.ActiveEffects
                    .Where(effect => effect.Definition.Id == effectId && effect.ExpiresAtUtc > now)
                    .OrderByDescending(effect => effect.Definition.Magnitude)
                    .ThenBy(effect => effect.Sequence).ToArray())
                {
                    if (!ActivePlayerActorIds().Contains(mark.SourceId)
                        || !_playerStatesByActorId.TryGetValue(mark.SourceId, out var owner))
                        continue;
                    ResolvedTalentEventHook? hook = owner.Talents.EventHooks.FirstOrDefault(h => h.TalentId == talentId);
                    if (hook is null || (talentId == "H-3-3" && attacker.Definition.ResourceType != "MANA"))
                        continue;
                    if (_random.NextUnit() >= hook.ChancePercent / 100m)
                        break;

                    // Personal ICD is shared across marks from different Paladins.
                    _paladinJudgementReadyAt[key] = now + hook.InternalCooldown;
                    ActivatePlayer(mark.SourceId);
                    if (talentId == "H-3-3")
                        AddResource(attacker.Definition.Actor,
                            attacker.Definition.Actor.MaxResource * hook.Value / 100m, now, talentId);
                    else
                        ApplySecondaryPaladinHealing(attackerId,
                            attacker.Definition.Actor.MaxHp * hook.Value / 100m, now, talentId);
                    break;
                }
            }
        }
        finally
        {
            _activePlayerState = previous;
        }
    }

    private decimal ResolvePaladinThreatMultiplier(CombatEvent combatEvent)
    {
        decimal multiplier = 1;
        if (combatEvent.IsPeriodic && combatEvent.DefinitionId == "PALADIN_CONSECRATION_DAMAGE"
            && TryGetPaladinHook("P-3-4", out var ground))
            multiplier *= 1 + ground.SecondaryValue / 100m;
        if (combatEvent.DefinitionId is "AUTO_ATTACK" or "JUDGEMENT" or "CRUSADER_STRIKE"
            or "TEMPLARS_VERDICT" or "DIVINE_STORM" or "PALADIN_VERDICT_BONUS"
            or "PALADIN_SEAL_COMMAND_PROC" or "PALADIN_SEAL_RIGHTEOUSNESS_PROC" or "PALADIN_ZEAL_PROC"
            && TryGetPaladinHook("R-5-2", out var fanaticism))
            multiplier *= Math.Max(0, 1 - fanaticism.SecondaryValue / 100m);
        return multiplier;
    }
}
