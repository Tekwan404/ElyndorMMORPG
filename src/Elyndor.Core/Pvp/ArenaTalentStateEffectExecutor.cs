using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Pvp;

/// <summary>
/// Generic executors for normalized talent effects that mutate canonical combat state.
/// Crowd-control effects are deliberately excluded here and must continue through
/// ArenaCombatSession's ApplyAbilityAction -> PvP CC/DR path.
/// </summary>
public static class ArenaTalentStateEffectExecutor
{
    public static bool ExecuteCooldownMutation(
        CombatRuntimeState runtime,
        ArenaTalentRuntimeEffect effect,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.Kind != ArenaTalentEffectKind.ModifyCooldown
            || string.IsNullOrWhiteSpace(effect.AbilityId))
        {
            throw new NotSupportedException("Arena talent cooldown effect is incomplete.");
        }

        return effect.ResetCooldown
            ? runtime.ResetCooldown(effect.AbilityId)
            : runtime.ModifyCooldown(effect.AbilityId, effect.CooldownDelta, now);
    }

    public static IReadOnlyList<CombatEvent> ExecuteStatusEffect(
        CombatActorState target,
        ArenaTalentRuntimeEffect effect,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.Kind is not (ArenaTalentEffectKind.ApplyBuff or ArenaTalentEffectKind.ApplyDebuff)
            || effect.Effect is null)
        {
            throw new NotSupportedException("Arena talent status effect is incomplete.");
        }

        if (CrowdControlCategoryResolver.TryResolve(effect.Effect.Kind, out _))
        {
            throw new NotSupportedException(
                "Crowd-control talent effects must execute through the arena CC/DR path.");
        }

        return EffectEngine.Apply(target, effect.SourceActorId, effect.Effect, now);
    }
}
