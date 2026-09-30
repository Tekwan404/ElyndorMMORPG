using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

/// <summary>
/// Archer defenses that need no PvE-only combat state: control duration is
/// captured on the actor and Iron Will checks the actor's live control effects.
/// </summary>
public static class ArenaArcherDefensiveTalentRuntime
{
    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return hook.Key == TalentModifierKeys.OnDamageTaken
            && hook.Value is > 0 and <= 100
            && (hook.TalentId switch
            {
                "S-1-2" => hook.TargetId == "CONTROL_DURATION_REDUCTION",
                "S-5-4" => hook.TargetId == "IRON_WILL"
                    && hook.SecondaryValue is > 0 and <= 100,
                _ => false
            });
    }

    public static void ConfigureActor(CombatActorState actor, ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(talents);

        decimal controlReduction = talents.EventHooks
            .Where(Supports)
            .Where(hook => hook.TalentId is "S-1-2" or "S-5-4")
            .Sum(hook => hook.Value);
        if (controlReduction > 0)
        {
            actor.IncomingControlDurationMultiplier = Math.Clamp(
                actor.IncomingControlDurationMultiplier * (1 - controlReduction / 100m),
                0.1m, 1m);
        }
    }

    public static decimal ApplyIncomingDamage(
        ResolvedTalentModifiers talents,
        IncomingDamageContext context,
        decimal damage)
    {
        ArgumentNullException.ThrowIfNull(talents);
        ArgumentNullException.ThrowIfNull(context);

        ResolvedTalentEventHook? ironWill = talents.EventHooks.FirstOrDefault(hook =>
            hook.TalentId == "S-5-4" && Supports(hook));
        if (ironWill is null
            || !(EffectEngine.HasControl(context.Target, EffectKind.Stun, context.OccurredAtUtc)
                || EffectEngine.HasControl(context.Target, EffectKind.Silence, context.OccurredAtUtc)))
        {
            return damage;
        }

        return Math.Max(0, damage * Math.Max(0.1m, 1 - ironWill.SecondaryValue / 100m));
    }
}
