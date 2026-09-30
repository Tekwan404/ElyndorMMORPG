using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

/// <summary>
/// PvE's Judgement-completion effects, scoped to a completed Paladin cast.
/// The content's P-4-3 trigger is legacy metadata: PvE applies it on Judgement,
/// not whenever the Paladin takes damage.
/// </summary>
public static class ArenaPaladinJudgementTalentRuntime
{
    private const string JudgementAbilityId = "JUDGEMENT";

    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return hook.TalentId switch
        {
            "R-6-4" => Matches(hook, TalentModifierKeys.OnAbilityUsed, "PALADIN_R_6_4"),
            "R-3-1" => Matches(hook, TalentModifierKeys.OnAbilityUsed, "PALADIN_R_3_1"),
            "P-4-3" => Matches(hook, TalentModifierKeys.OnDamageTaken, "PALADIN_P_4_3"),
            _ => false
        };
    }

    public static IReadOnlyList<ArenaTalentRuntimeEffect> Dispatch(
        ResolvedTalentModifiers talents,
        ArenaTalentCombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(talents);
        ArgumentNullException.ThrowIfNull(combatEvent);

        if (combatEvent.Type != ArenaTalentEventType.OnCast
            || !string.Equals(combatEvent.Ability?.Id, JudgementAbilityId, StringComparison.Ordinal))
        {
            return [];
        }

        List<ArenaTalentRuntimeEffect> effects = [];
        foreach (ResolvedTalentEventHook hook in talents.EventHooks.Where(Supports))
        {
            switch (hook.TalentId)
            {
                case "R-6-4":
                    effects.Add(new ArenaTalentRuntimeEffect(
                        ArenaTalentEffectKind.GainResource,
                        hook.TalentId,
                        combatEvent.SourceActorId,
                        combatEvent.SourceActorId,
                        Amount: 4m * hook.Rank));
                    break;
                case "R-3-1":
                    effects.Add(StatusEffect(hook, combatEvent,
                        new EffectDefinition(
                            "PALADIN_CRUSADERS_JUDGEMENT",
                            EffectKind.StatModifier,
                            TimeSpan.FromSeconds(12),
                            1,
                            EffectStackPolicy.Refresh,
                            1.10m,
                            ModifiedStat: EffectStat.IncomingMagicalDamageMultiplier,
                            ModifierMode: EffectModifierMode.Multiplicative,
                            SourceSpecific: true)));
                    break;
                case "P-4-3":
                    effects.Add(StatusEffect(hook, combatEvent,
                        new EffectDefinition(
                            "PALADIN_JUDGEMENT_OF_JUSTICE",
                            EffectKind.StatModifier,
                            TimeSpan.FromSeconds(8),
                            1,
                            EffectStackPolicy.Refresh,
                            0.82m,
                            ModifiedStat: EffectStat.AttackSpeed,
                            ModifierMode: EffectModifierMode.Multiplicative,
                            SourceSpecific: true)));
                    break;
            }
        }

        return effects;
    }

    private static bool Matches(ResolvedTalentEventHook hook, string key, string targetId) =>
        string.Equals(hook.Key, key, StringComparison.Ordinal)
        && string.Equals(hook.TargetId, targetId, StringComparison.Ordinal)
        && hook.Rank > 0;

    private static ArenaTalentRuntimeEffect StatusEffect(
        ResolvedTalentEventHook hook,
        ArenaTalentCombatEvent combatEvent,
        EffectDefinition effect) => new(
            ArenaTalentEffectKind.ApplyDebuff,
            hook.TalentId,
            combatEvent.SourceActorId,
            combatEvent.TargetActorId,
            Effect: effect);
}
