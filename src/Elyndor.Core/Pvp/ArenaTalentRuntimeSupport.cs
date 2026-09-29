using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

/// <summary>
/// Central capability registry for talent mechanics that have Arena semantics.
/// Talent IDs remain content keys inside the shared resolvers; Arena itself only
/// asks whether a resolved mechanic is supported and applies the shared ability
/// transformations in one place.
/// </summary>
public static class ArenaTalentRuntimeSupport
{
    public static void ConfigureActorRuntime(
        CombatActorState actor,
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(talents);

        actor.IncomingDamageModifier = (context, random) =>
            ArenaTalentEventDispatcher.ApplyIncomingDamageModifiers(talents, context, random);
    }

    public static AbilityDefinition ApplyAbilityDefinitionModifiers(
        AbilityDefinition ability,
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(talents);

        AbilityDefinition resolved = TalentAbilityResolver.Apply(ability, talents);
        resolved = PyromancerStaticAbilityHookResolver.Apply(resolved, talents);
        resolved = MageStaticAbilityHookResolver.Apply(resolved, talents);
        resolved = ArcherStaticAbilityHookResolver.Apply(resolved, talents);
        return resolved;
    }

    public static bool SupportsEventHook(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        return ArenaTalentEventDispatcher.Supports(hook)
            || PyromancerStaticAbilityHookResolver.Supports(hook)
            || MageStaticAbilityHookResolver.Supports(hook)
            || ArcherStaticAbilityHookResolver.Supports(hook);
    }

    public static IReadOnlyList<ResolvedTalentEventHook> UnsupportedEventHooks(
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(talents);
        return talents.EventHooks.Where(hook => !SupportsEventHook(hook)).ToArray();
    }

    public static string DescribeUnsupportedHook(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        string target = string.IsNullOrWhiteSpace(hook.TargetId) ? "<none>" : hook.TargetId;
        return $"{hook.Key}:{target}";
    }
}
