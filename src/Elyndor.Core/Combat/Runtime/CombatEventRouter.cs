using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat;

// Reactions consume results. They never replay the damage/healing represented by an event.
internal sealed class CombatEventRouter(
    Func<CombatReactionContext> context,
    IReadOnlyDictionary<CombatReaction, Action<CombatEvent>> handlers,
    CombatAbilityReactionHandlers abilityHandlers,
    Action<CombatEvent, string, Action> runProcHooks,
    Func<CombatEvent, CombatEvent> captureProcOrigin)
{
    private readonly Func<CombatReactionContext> _context = context;
    private readonly IReadOnlyDictionary<CombatReaction, Action<CombatEvent>> _handlers = handlers;
    private readonly Action<CombatEvent, string, Action> _runProcHooks = runProcHooks;
    private readonly Func<CombatEvent, CombatEvent> _captureProcOrigin = captureProcOrigin;

    public void DispatchAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        abilityHandlers.PyromancerStarted(ability, now);
        abilityHandlers.MageStarted(ability, now);
        abilityHandlers.ArcherStarted(ability, now);
    }

    public void DispatchAbilityResolved(AbilityDefinition ability, AbilityExecutionResult execution, DateTimeOffset now)
    {
        abilityHandlers.WarriorResolved(ability, execution, now);
        abilityHandlers.PyromancerResolved(ability, execution, now);
        abilityHandlers.MageResolved(ability, execution, now);
        abilityHandlers.ArcherResolved(ability, execution, now);
    }

    public CombatEvent[] Normalize(
        IEnumerable<CombatEvent> events, Guid sourceActorId, Guid targetActorId,
        string? definitionId, CombatWeaponHand? weaponHand, string? weaponDefinitionId) =>
        events.Select(item => _captureProcOrigin(item) with
        {
            DefinitionId = item.DefinitionId ?? definitionId,
            SourceActorId = item.SourceActorId ?? sourceActorId,
            TargetActorId = item.TargetActorId ?? targetActorId,
            WeaponHand = item.WeaponHand ?? weaponHand,
            WeaponDefinitionId = item.WeaponDefinitionId ?? weaponDefinitionId
        }).ToArray();

    public static CombatRuntimeEvent ToRuntimeEvent(
        CombatEvent combatEvent, CombatRuntimeEventKind kind, long sequence) =>
        new(kind, combatEvent.OccurredAtUtc,
            combatEvent.SourceActorId ?? combatEvent.ActorId,
            combatEvent.TargetActorId, combatEvent.DefinitionId,
            Amount: combatEvent.Amount,
            DamageType: combatEvent.DamageType,
            IsPeriodic: combatEvent.IsPeriodic,
            IsProc: combatEvent.IsProc || combatEvent.IsReflected,
            ProcDepth: combatEvent.ProcDepth,
            ProcOriginId: combatEvent.ProcOriginId,
            Sequence: sequence)
        { ProcDispatchToken = combatEvent.ProcDispatchToken };

    // PvE calls Paladin during its pre-publication threat/encounter stage. A hosted
    // Arena calls it after publication. Keep those existing authoritative boundaries.
    public void DispatchPaladin(CombatEvent combatEvent) => React(CombatReaction.Paladin, combatEvent);

    public void Dispatch(CombatEvent combatEvent, bool hosted = false)
    {
        if (hosted)
            DispatchPaladin(combatEvent);
        _runProcHooks(combatEvent, "sets", () => React(CombatReaction.SetPassives, combatEvent));
        _runProcHooks(combatEvent, "item-special-effects", () => React(CombatReaction.ItemSpecialEffects, combatEvent));
        _runProcHooks(combatEvent, "active-effects", () => React(CombatReaction.ActiveEffects, combatEvent));
        React(CombatReaction.EventThreat, combatEvent);
        _runProcHooks(combatEvent, "class-talents", () => DispatchTalentReactions(combatEvent));
        if (combatEvent.Type == CombatEventType.DamageBlocked)
            _runProcHooks(combatEvent, "guardian-block", () => React(CombatReaction.GuardianBlock, combatEvent));
    }

    // Terminal lifecycle credit deliberately bypasses hit-proc admission, including
    // lethal periodic/secondary hits. The session remains responsible for death dedup.
    public void DispatchKill(CombatEvent death)
    {
        React(CombatReaction.GenericEnemyKilled, death);
        React(CombatReaction.BerserkerEnemyKilled, death);
        React(CombatReaction.PyromancerEnemyKilled, death);
        React(CombatReaction.ArcherEnemyKilled, death);
        React(CombatReaction.WarlordEnemyKilled, death);
    }

    private void React(CombatReaction reaction, CombatEvent combatEvent) =>
        _handlers[reaction](combatEvent);

    private void DispatchTalentReactions(CombatEvent combatEvent)
    {
        if (combatEvent.Type == CombatEventType.AbilityCompleted
            && combatEvent.SourceActorId == _context().OwnerActorId)
        {
            React(CombatReaction.GenericAbilityUsed, combatEvent);
            React(CombatReaction.GuardianAbility, combatEvent);
            React(CombatReaction.WarlordAbility, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.Dodge
            && combatEvent.TargetActorId == _context().OwnerActorId)
        {
            React(CombatReaction.GenericDodge, combatEvent);
            React(CombatReaction.GuardianDodge, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.Amount > 0)
        {
            bool playerTarget = combatEvent.TargetActorId == _context().OwnerActorId;
            bool partyTarget = playerTarget
                || _context().CompanionActorId is not null
                && combatEvent.TargetActorId == _context().CompanionActorId;
            if (partyTarget && !combatEvent.IsPeriodic)
            {
                if (playerTarget
                    && _context().UsesRage)
                {
                    React(CombatReaction.DirectDamageTakenResource, combatEvent);
                }
                if (playerTarget)
                {
                    React(CombatReaction.GenericDamageTaken, combatEvent);
                    React(CombatReaction.GuardianDamageTaken, combatEvent);
                }
                React(CombatReaction.WarlordPartyDamage, combatEvent);
            }

            if (playerTarget)
            {
                React(CombatReaction.BerserkerDamageTaken, combatEvent);
                React(CombatReaction.MageDamageTaken, combatEvent);
                React(CombatReaction.ArcherDamageTaken, combatEvent);
            }

            React(CombatReaction.WarlordAutoAttack, combatEvent);
            React(CombatReaction.GuardianAutoAttack, combatEvent);
        }

        // A fully absorbed or blocked hit still landed; avoidance produces no DamageDealt event.
        if (combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.SourceActorId == _context().OwnerActorId)
            React(CombatReaction.GuardianSuccessfulHit, combatEvent);

        if (combatEvent.Type == CombatEventType.ShieldAbsorbed
            && combatEvent.TargetActorId == _context().OwnerActorId)
        {
            React(CombatReaction.MageShieldAbsorbed, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.ResourceChanged
            && combatEvent.ActorId == _context().OwnerActorId)
        {
            React(CombatReaction.MageResourceThreshold, combatEvent);
            React(CombatReaction.ArcherResourceThreshold, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && combatEvent.SourceActorId == _context().OwnerActorId)
        {
            React(CombatReaction.GenericCriticalHit, combatEvent);
            React(CombatReaction.BerserkerCritical, combatEvent);
            React(CombatReaction.GuardianCritical, combatEvent);
            React(CombatReaction.PyromancerCritical, combatEvent);
            React(CombatReaction.MageCritical, combatEvent);
            React(CombatReaction.ArcherCritical, combatEvent);
            React(CombatReaction.WarlordPartyCritical, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && _context().CompanionActorId is not null
            && combatEvent.SourceActorId == _context().CompanionActorId)
        {
            React(CombatReaction.ArcherCritical, combatEvent);
            React(CombatReaction.WarlordPartyCritical, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.CriticalHit
            && combatEvent.TargetActorId == _context().OwnerActorId)
        {
            React(CombatReaction.PyromancerIncomingCritical, combatEvent);
            React(CombatReaction.MageIncomingCritical, combatEvent);
            React(CombatReaction.ArcherIncomingCritical, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.AbilityInterrupted
            && combatEvent.ActorId == _context().OwnerActorId)
        {
            React(CombatReaction.PyromancerAbilityInterrupted, combatEvent);
            React(CombatReaction.MageAbilityInterrupted, combatEvent);
        }

        if (combatEvent.Type == CombatEventType.DamageDealt)
            React(CombatReaction.ArcherCompanionDamage, combatEvent);

        if (combatEvent.Type == CombatEventType.HealingApplied)
            React(CombatReaction.ArcherHealing, combatEvent);
    }
}

internal readonly record struct CombatReactionContext(
    Guid OwnerActorId, Guid? CompanionActorId, bool UsesRage);

internal enum CombatReaction
{
    ActiveEffects,
    GuardianSuccessfulHit,
    Paladin,
    SetPassives,
    ItemSpecialEffects,
    EventThreat,
    GuardianBlock,
    GenericAbilityUsed,
    GenericDodge,
    GenericDamageTaken,
    GenericCriticalHit,
    DirectDamageTakenResource,
    GenericEnemyKilled,
    BerserkerEnemyKilled,
    PyromancerEnemyKilled,
    ArcherEnemyKilled,
    WarlordEnemyKilled,
    GuardianAbility,
    WarlordAbility,
    GuardianDodge,
    GuardianDamageTaken,
    WarlordPartyDamage,
    BerserkerDamageTaken,
    MageDamageTaken,
    ArcherDamageTaken,
    WarlordAutoAttack,
    GuardianAutoAttack,
    MageShieldAbsorbed,
    MageResourceThreshold,
    ArcherResourceThreshold,
    BerserkerCritical,
    GuardianCritical,
    PyromancerCritical,
    MageCritical,
    ArcherCritical,
    WarlordPartyCritical,
    PyromancerIncomingCritical,
    MageIncomingCritical,
    ArcherIncomingCritical,
    PyromancerAbilityInterrupted,
    MageAbilityInterrupted,
    ArcherCompanionDamage,
    ArcherHealing,
}
