using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
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

        ArenaArcherDefensiveTalentRuntime.ConfigureActor(actor, talents);
        actor.IncomingDamageModifier = (context, random) =>
            ArenaArcherDefensiveTalentRuntime.ApplyIncomingDamage(
                talents, context,
                ArenaTalentEventDispatcher.ApplyIncomingDamageModifiers(talents, context, random));
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
        resolved = WarlordStaticAbilityHookResolver.Apply(resolved, talents);
        return resolved;
    }

    public static bool SupportsEventHook(ResolvedTalentEventHook hook, string? classId = null)
    {
        ArgumentNullException.ThrowIfNull(hook);

        return classId is not null && PlayerCombatMechanicsCapabilities.SupportsHook(classId, hook)
            || ArenaTalentEventDispatcher.Supports(hook)
            || PyromancerStaticAbilityHookResolver.Supports(hook)
            || MageStaticAbilityHookResolver.Supports(hook)
            || ArcherStaticAbilityHookResolver.Supports(hook)
            || ArenaArcherDefensiveTalentRuntime.Supports(hook)
            || WarlordStaticAbilityHookResolver.Supports(hook);
    }

    public static IReadOnlyList<ResolvedTalentEventHook> UnsupportedEventHooks(
        ResolvedTalentModifiers talents, string? classId = null)
    {
        ArgumentNullException.ThrowIfNull(talents);
        return talents.EventHooks.Where(hook => !SupportsEventHook(hook, classId)).ToArray();
    }

    /// <summary>
    /// Removes only companion-dependent hooks from a 1v1 entrant. Other hooks
    /// remain available to runtime adapters and the production-build audit;
    /// unsupported owner hooks must never disappear during assembly.
    /// </summary>
    public static ResolvedTalentModifiers ForOneVsOne(ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(talents);

        ResolvedTalentEventHook[] retained = talents.EventHooks
            .Where(hook => !ArenaCompanionCapability.RequiresCompanion(hook))
            .ToArray();
        if (retained.Length == talents.EventHooks.Count)
            return talents;

        return talents with
        {
            EventHooks = retained
        };
    }

    public static string DescribeUnsupportedHook(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        string target = string.IsNullOrWhiteSpace(hook.TargetId) ? "<none>" : hook.TargetId;
        return $"{hook.Key}:{target}";
    }
}
