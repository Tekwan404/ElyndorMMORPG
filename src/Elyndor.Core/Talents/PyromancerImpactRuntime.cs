using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Talents;

/// <summary>
/// Shared production resolver for the Pyromancer Impact talent. The resolver only decides
/// whether the proc occurs and emits a normal ApplyEffect(Stun) action. The active combat
/// runtime remains responsible for applying that action and any PvP-specific control rules.
/// </summary>
public static class PyromancerImpactRuntime
{
    public const string TalentId = "F-2-2";
    public const string StunEffectId = "MAGE_FIRE_IMPACT_STUN";

    public static bool SupportsArenaHook(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return string.Equals(hook.TalentId, TalentId, StringComparison.Ordinal)
            && string.Equals(hook.Key, TalentModifierKeys.OnAbilityUsed, StringComparison.Ordinal);
    }

    public static AbilityActionDefinition? TryResolveStunAction(
        ResolvedTalentModifiers talents,
        AbilityDefinition ability,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(talents);
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(random);

        if (!string.Equals(ability.School, "FIRE", StringComparison.Ordinal)
            || ability.Actions?.Any(action => action.Type == AbilityActionType.Damage) != true)
        {
            return null;
        }

        ResolvedTalentEventHook? impact = talents.EventHooks.FirstOrDefault(SupportsArenaHook);
        if (impact is null || impact.Value <= 0 || impact.Duration <= TimeSpan.Zero
            || random.NextUnit() >= impact.Value / 100m)
        {
            return null;
        }

        return new AbilityActionDefinition(
            AbilityActionType.ApplyEffect,
            Effect: new EffectDefinition(
                StunEffectId,
                EffectKind.Stun,
                impact.Duration,
                1,
                EffectStackPolicy.Replace,
                0,
                SourceSpecific: true));
    }
}
