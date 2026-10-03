using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private CombatEventRouter? _eventRouter;

    // Read identity on demand: nested events and party commands can change the active
    // participant. Registration binds existing mechanics, not a second implementation.
    private CombatEventRouter EventRouter => _eventRouter ??= new(
        () => new CombatReactionContext(_player.Actor.ActorId, _companion?.Actor.ActorId,
            string.Equals(_player.ResourceType, "RAGE", StringComparison.Ordinal)),
        new Dictionary<CombatReaction, Action<CombatEvent>>
        {
            [CombatReaction.Paladin] = ProcessPaladinKernelEvent,
            [CombatReaction.SetPassives] = ApplySetPassiveHooks,
            [CombatReaction.EventThreat] = ApplyEventThreat,
            [CombatReaction.GuardianBlock] = ApplyGuardianBlockHooks,
            [CombatReaction.GenericAbilityUsed] = e => TriggerEventTalent(TalentModifierKeys.OnAbilityUsed, e, CombatRuntimeEventKind.AbilityCompleted),
            [CombatReaction.GenericDodge] = e => TriggerEventTalent(TalentModifierKeys.OnDodge, e, CombatRuntimeEventKind.Dodge),
            [CombatReaction.GenericDamageTaken] = e => TriggerEventTalent(TalentModifierKeys.OnDamageTaken, e, CombatRuntimeEventKind.DamageTaken),
            [CombatReaction.GenericCriticalHit] = e => TriggerEventTalent(TalentModifierKeys.OnCriticalHit, e, CombatRuntimeEventKind.CriticalHit),
            [CombatReaction.DirectDamageTakenResource] = e => AddResource(_player.Actor,
                BaseRageFromDirectDamageTaken * GuardianRageMultiplier, e.OccurredAtUtc, "DIRECT_DAMAGE_TAKEN"),
            [CombatReaction.GenericEnemyKilled] = e => TriggerTalent(TalentModifierKeys.OnEnemyKilled, e.OccurredAtUtc),
            [CombatReaction.BerserkerEnemyKilled] = e => ApplyBerserkerEnemyKilledHooks(e.OccurredAtUtc),
            [CombatReaction.PyromancerEnemyKilled] = ApplyPyromancerEnemyKilledHooks,
            [CombatReaction.ArcherEnemyKilled] = e => ApplyArcherEnemyKilledHooks(e.OccurredAtUtc),
            [CombatReaction.WarlordEnemyKilled] = ApplyWarlordEnemyKilledHooks,
            [CombatReaction.GuardianAbility] = ApplyGuardianAbilityHooks,
            [CombatReaction.WarlordAbility] = ApplyWarlordAbilityHooks,
            [CombatReaction.GuardianDodge] = ApplyGuardianDodgeHooks,
            [CombatReaction.GuardianDamageTaken] = ApplyGuardianDamageTakenHooks,
            [CombatReaction.WarlordPartyDamage] = ApplyWarlordPartyDamageHooks,
            [CombatReaction.BerserkerDamageTaken] = ApplyBerserkerDamageTakenHooks,
            [CombatReaction.MageDamageTaken] = ApplyMageDamageTakenHooks,
            [CombatReaction.ArcherDamageTaken] = ApplyArcherDamageTakenHooks,
            [CombatReaction.WarlordAutoAttack] = e => ApplyWarlordAutoAttackHooks(e),
            [CombatReaction.GuardianAutoAttack] = ApplyGuardianAutoAttackHooks,
            [CombatReaction.MageShieldAbsorbed] = ApplyMageShieldAbsorbedHooks,
            [CombatReaction.MageResourceThreshold] = ApplyMageResourceThresholdHooks,
            [CombatReaction.ArcherResourceThreshold] = ApplyArcherResourceThresholdHooks,
            [CombatReaction.BerserkerCritical] = ApplyBerserkerCriticalHooks,
            [CombatReaction.GuardianCritical] = ApplyGuardianCriticalHooks,
            [CombatReaction.PyromancerCritical] = ApplyPyromancerCriticalHooks,
            [CombatReaction.MageCritical] = ApplyMageCriticalHooks,
            [CombatReaction.ArcherCritical] = ApplyArcherCriticalHooks,
            [CombatReaction.WarlordPartyCritical] = ApplyWarlordPartyCriticalHooks,
            [CombatReaction.PyromancerIncomingCritical] = ApplyPyromancerIncomingCriticalHooks,
            [CombatReaction.MageIncomingCritical] = ApplyMageIncomingCriticalHooks,
            [CombatReaction.ArcherIncomingCritical] = ApplyArcherIncomingCriticalHooks,
            [CombatReaction.PyromancerAbilityInterrupted] = OnPyromancerAbilityInterrupted,
            [CombatReaction.MageAbilityInterrupted] = OnMageAbilityInterrupted,
            [CombatReaction.ArcherCompanionDamage] = ApplyArcherCompanionDamageHooks,
            [CombatReaction.ArcherHealing] = ApplyArcherHealingHooks,
        },
        RunProcHooks,
        CaptureProcOrigin);

    private void TriggerEventTalent(string key, CombatEvent combatEvent, CombatRuntimeEventKind kind) =>
        TriggerTalent(key, combatEvent.OccurredAtUtc,
            CombatEventRouter.ToRuntimeEvent(combatEvent, kind, Sequence));
}
