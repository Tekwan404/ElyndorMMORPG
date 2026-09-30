using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Talents;

/// <summary>
/// Warlord hooks whose PvE behaviour is fully captured by an ability definition.
/// Party-wide effects and hooks with combat state remain unsupported in 1v1 Arena.
/// </summary>
public static class WarlordStaticAbilityHookResolver
{
    private static readonly HashSet<string> CryIds =
    [
        "BATTLE_CRY", "ENDURANCE_CRY", "CRY_OF_VENGEANCE", "RALLY_CRY"
    ];

    private static readonly HashSet<string> FlagIds =
    [
        "WAR_BANNER", "VICTORY_FLAG", "BATTLE_STANDARD"
    ];

    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return hook.Key == TalentModifierKeys.OnPartyEvent
            && hook.TargetId is null
            && hook.Value > 0
            && hook.TalentId is "W-1-1" or "W-4-4" or "W-5-2";
    }

    public static AbilityDefinition Apply(
        AbilityDefinition ability,
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(talents);

        decimal resourceCost = ability.ResourceCost;
        TimeSpan cooldown = ability.Cooldown;
        IReadOnlyList<AbilityActionDefinition>? actions = ability.Actions;

        if (CryIds.Contains(ability.Id))
        {
            if (FindHook(talents, "W-1-1") is { } voice)
                resourceCost = Math.Max(0, resourceCost - voice.Value);

            if (FindHook(talents, "W-4-4") is { } echo && actions is not null)
            {
                actions = actions.Select(action => action.Effect is null
                        || action.Effect.Duration <= TimeSpan.Zero
                    ? action
                    : action with
                    {
                        Effect = action.Effect with
                        {
                            Duration = action.Effect.Duration
                                + TimeSpan.FromSeconds((double)echo.Value)
                        }
                    }).ToArray();
            }
        }

        if (FlagIds.Contains(ability.Id)
            && FindHook(talents, "W-5-2") is { } fearlessness)
        {
            cooldown = cooldown - TimeSpan.FromSeconds((double)fearlessness.Value);
            if (cooldown < TimeSpan.Zero)
                cooldown = TimeSpan.Zero;
        }

        return ability with
        {
            ResourceCost = resourceCost,
            Cooldown = cooldown,
            Actions = actions
        };
    }

    private static ResolvedTalentEventHook? FindHook(
        ResolvedTalentModifiers talents,
        string talentId) => talents.EventHooks.FirstOrDefault(hook =>
            hook.TalentId == talentId && Supports(hook));
}
