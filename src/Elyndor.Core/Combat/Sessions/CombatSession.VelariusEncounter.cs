using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;

namespace Elyndor.Core.Combat.Sessions;

public sealed record VelariusCombatEncounterProfile(
    EncounterEnemyProfile ManaFeeder,
    VelariusManaEncounterDefinition? Definition = null);

public sealed partial class CombatSession
{
    private const string VelariusPhaseGuardEffectId = "VELARIUS_PHASE_GUARD";
    private const string VelariusLastBarrierEffectId = "VELARIUS_LAST_BARRIER";
    private const string VelariusFinalPhaseEffectId = "VELARIUS_FINAL_PHASE";
    private const string VelariusManaFeedAbilityId = "VELARIUS_MANA_FEED";
    private const string VelariusDrainLifeAbilityId = "VELARIUS_DRAIN_LIFE";
    private static readonly TimeSpan VelariusPersistentEffectDuration = TimeSpan.FromHours(24);

    private VelariusManaEncounterRuntime? _velariusEncounterRuntime;
    private VelariusCombatEncounterProfile? _velariusEncounterProfile;
    private Guid? _velariusBossActorId;

    public void ConfigureVelariusEncounter(VelariusCombatEncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profile.ManaFeeder);
        if (_velariusEncounterRuntime is not null)
            throw new InvalidOperationException("Velarius encounter is already configured for this combat session.");
        if (Status != CombatSessionStatus.Active)
            throw new InvalidOperationException("Velarius encounter can only be configured for an active combat session.");

        ValidateVelariusProfile(profile);
        _velariusEncounterProfile = profile;
        _velariusEncounterRuntime = new VelariusManaEncounterRuntime(
            profile.Definition ?? VelariusManaEncounterDefinition.Default);
        CombatParticipantDefinition boss = ConfigurePrimaryEncounterResource("MANA", 100, 100);
        _velariusBossActorId = boss.Actor.ActorId;
        EnsureVelariusPhaseGuard(CurrentTimeUtc);
    }

    private void ProcessVelariusEncounterEvent(CombatEvent combatEvent)
    {
        if (_velariusEncounterRuntime is null
            || _velariusEncounterProfile is null
            || _velariusBossActorId is not { } bossActorId
            || Status != CombatSessionStatus.Active)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.AbilityCompleted
            && string.Equals(combatEvent.DefinitionId, VelariusManaFeedAbilityId, StringComparison.Ordinal)
            && combatEvent.SourceActorId is { } feederActorId
            && _enemiesById.TryGetValue(feederActorId, out CombatParticipantDefinition? feeder)
            && string.Equals(
                feeder.DefinitionId,
                _velariusEncounterProfile.ManaFeeder.Monster.Id,
                StringComparison.Ordinal)
            && !_velariusEncounterRuntime.FinalPhaseActive)
        {
            CombatActorState bossActor = _enemiesById[bossActorId].Actor;
            ChangeEncounterResource(
                bossActor,
                _velariusEncounterRuntime.ManaPerFeed,
                feederActorId,
                combatEvent.OccurredAtUtc,
                VelariusManaFeedAbilityId);
            return;
        }

        if (combatEvent.Type == CombatEventType.AbilityCompleted
            && combatEvent.SourceActorId == bossActorId
            && string.Equals(combatEvent.DefinitionId, VelariusDrainLifeAbilityId, StringComparison.Ordinal))
        {
            HealEncounterActor(
                _enemiesById[bossActorId].Actor,
                600,
                bossActorId,
                combatEvent.OccurredAtUtc,
                VelariusDrainLifeAbilityId);
            return;
        }

        if (combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.TargetActorId != bossActorId
            || combatEvent.IsPeriodic)
        {
            return;
        }

        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (_velariusEncounterRuntime.TryTriggerFinalBarrier(
                boss.CurrentHp,
                boss.MaxHp,
                boss.CurrentResource,
                out decimal shieldMagnitude))
        {
            BeginVelariusFinalBarrier(
                boss,
                shieldMagnitude,
                combatEvent.OccurredAtUtc);
            return;
        }

        if (_velariusEncounterRuntime.TryTriggerFeeders(boss.CurrentHp, boss.MaxHp))
        {
            for (var index = 0; index < _velariusEncounterRuntime.FeederCount; index++)
            {
                SpawnEncounterEnemy(
                    _velariusEncounterProfile.ManaFeeder,
                    bossActorId,
                    combatEvent.OccurredAtUtc);
            }
        }
    }

    private void BeginVelariusFinalBarrier(
        CombatActorState boss,
        decimal shieldMagnitude,
        DateTimeOffset now)
    {
        if (_velariusEncounterRuntime is null)
            return;

        decimal minimumFinalPhaseHp =
            boss.MaxHp * _velariusEncounterRuntime.FinalBarrierTriggerHpPercent;
        if (boss.CurrentHp < minimumFinalPhaseHp)
            boss.SetCurrentHp(minimumFinalPhaseHp);

        decimal remainingMana = boss.CurrentResource;
        if (remainingMana > 0)
        {
            ChangeEncounterResource(
                boss,
                -remainingMana,
                boss.ActorId,
                now,
                VelariusLastBarrierEffectId);
        }

        if (shieldMagnitude > 0)
        {
            ApplyKernelEvents(
                EffectEngine.Apply(
                    boss,
                    boss.ActorId,
                    new EffectDefinition(
                        VelariusLastBarrierEffectId,
                        EffectKind.Shield,
                        VelariusPersistentEffectDuration,
                        1,
                        EffectStackPolicy.Replace,
                        shieldMagnitude),
                    now),
                boss.ActorId,
                boss.ActorId,
                VelariusLastBarrierEffectId);
        }

        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                boss.ActorId,
                new EffectDefinition(
                    VelariusFinalPhaseEffectId,
                    EffectKind.Buff,
                    VelariusPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now),
            boss.ActorId,
            boss.ActorId,
            VelariusFinalPhaseEffectId);
        RemoveVelariusEffect(VelariusPhaseGuardEffectId, now);
    }

    private void EnsureVelariusPhaseGuard(DateTimeOffset now)
    {
        if (_velariusBossActorId is not { } bossActorId)
            return;
        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (boss.ActiveEffects.Any(effect =>
                string.Equals(effect.Definition.Id, VelariusPhaseGuardEffectId, StringComparison.Ordinal)))
        {
            return;
        }

        ApplyKernelEvents(
            EffectEngine.Apply(
                boss,
                bossActorId,
                new EffectDefinition(
                    VelariusPhaseGuardEffectId,
                    EffectKind.LethalDamagePrevention,
                    VelariusPersistentEffectDuration,
                    1,
                    EffectStackPolicy.Replace,
                    0),
                now),
            bossActorId,
            bossActorId,
            VelariusPhaseGuardEffectId);
    }

    private void RemoveVelariusEffect(string effectId, DateTimeOffset now)
    {
        if (_velariusBossActorId is not { } bossActorId)
            return;
        CombatActorState boss = _enemiesById[bossActorId].Actor;
        if (!boss.ActiveEffects.Any(effect =>
                string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)))
        {
            return;
        }

        ApplyKernelEvents(
            EffectEngine.Remove(boss, effectId, now),
            bossActorId,
            bossActorId,
            effectId);
    }

    private void ValidateVelariusProfile(VelariusCombatEncounterProfile profile)
    {
        if (profile.ManaFeeder.Monster.MaxHp <= 0
            || profile.ManaFeeder.Monster.AutoAttackInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Velarius mana feeder combat profile is invalid.", nameof(profile));
        }
        if (profile.ManaFeeder.Monster.AbilityIds.Any(abilityId => !_abilities.ContainsKey(abilityId)))
            throw new ArgumentException("Velarius mana feeder references an unknown ability.", nameof(profile));
        if (profile.ManaFeeder.AiProfile.PriorityAbilityIds.Any(abilityId =>
                !profile.ManaFeeder.Monster.AbilityIds.Contains(abilityId, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "Velarius mana feeder AI references an ability the feeder does not know.",
                nameof(profile));
        }
    }
}
