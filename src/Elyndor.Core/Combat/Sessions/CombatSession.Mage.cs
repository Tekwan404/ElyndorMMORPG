using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Talents;
using Elyndor.Core.Combat.Mage;
using Elyndor.Core.Combat.Participants;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string FireballId = MageRuntimeBase.FireballId;
    private const string FireBlastId = MageRuntimeBase.FireBlastId;
    private const string ScorchId = MageRuntimeBase.ScorchId;
    private const string PyroblastId = MageRuntimeBase.PyroblastId;
    private const string FlamestrikeId = MageRuntimeBase.FlamestrikeId;
    private const string BlastWaveId = MageRuntimeBase.BlastWaveId;
    private const string IgniteEffectId = MageRuntimeBase.IgniteEffectId;
    private const string PyroblastBurnEffectId = MageRuntimeBase.PyroblastBurnEffectId;
    private const string FlamestrikeBurnEffectId = MageRuntimeBase.FlamestrikeBurnEffectId;
    private const string CombustionPulseEffectId = MageRuntimeBase.CombustionPulseEffectId;
    private const string ClearcastingRegenEffectId = MageRuntimeBase.ClearcastingRegenEffectId;
    private const string ArcaneEchoEffectId = MageRuntimeBase.ArcaneEchoEffectId;
    private static readonly HashSet<string> FireDamageDefinitionIds = new(StringComparer.Ordinal)
    {
        FireballId,
        FireBlastId,
        ScorchId,
        PyroblastId,
        FlamestrikeId,
        BlastWaveId,
        IgniteEffectId,
        PyroblastBurnEffectId,
        FlamestrikeBurnEffectId,
        CombustionPulseEffectId
    };
    private bool IsMage => string.Equals(_player.DefinitionId, "MAGE", StringComparison.Ordinal);
    private MageCombatRuntime ActiveMageRuntime => _activePlayerState.MageRuntime ??= CreateMageRuntime(_activePlayerState);
    private DateTimeOffset? _lastMageManaSpendAtUtc => ActiveMageRuntime.Arcane.LastManaSpendAtUtc;
    private MageCombatRuntime CreateMageRuntime(CombatPlayerRuntimeState state) => new(new MageCombatContext(
        state.Definition, state.Runtime, state.Talents, _abilities, _enemiesById, _enemyRuntimes,
        _random, _procGuard, _combatStartedAtUtc, () => state.SelectedTargetActorId,
        () => Status == CombatSessionStatus.Active,
        (events, source, target, id) => WithMageOwner(state, () => ApplyKernelEvents(events, source, target, id)),
        (actor, amount, now, id) => WithMageOwner(state, () => AddResource(actor, amount, now, id)),
        (result, path, action) => WithMageOwner(state, () => RunResolvedProcHooks(result, path, action))));
    private void WithMageOwner(CombatPlayerRuntimeState state, Action action)
    {
        var previous = _activePlayerState;
        try { _activePlayerState = state; action(); }
        finally { _activePlayerState = previous; }
    }
    private void SyncMageConditionalEffects(DateTimeOffset now) { if (IsMage) ActiveMageRuntime.Sync(now); }
    private void ConfigureMageIncomingDamage() { if (IsMage) ActiveMageRuntime.Arcane.ConfigureMageIncomingDamage(); }
    private decimal EffectivePlayerResourceRegenPerSecond(DateTimeOffset now) => IsMage ? ActiveMageRuntime.Arcane.EffectivePlayerResourceRegenPerSecond(now) : _player.ResourceRegenPerSecond;
    private AbilityDefinition ResolvePyromancerAbility(AbilityDefinition ability, DateTimeOffset now) => IsMage ? ActiveMageRuntime.Fire.ResolvePyromancerAbility(ability, now) : ability;
    private AbilityDefinition ResolveArcaneMageAbility(AbilityDefinition ability, DateTimeOffset now) => IsMage ? ActiveMageRuntime.Arcane.ResolveArcaneMageAbility(ability, now) : ability;
    private AbilityDefinition ResolveFrostMageAbility(AbilityDefinition ability, DateTimeOffset now) => IsMage ? ActiveMageRuntime.Frost.ResolveFrostMageAbility(ability, now) : ability;
    private AbilityTargetModifier ResolvePyromancerTargetAbilityModifier(AbilityDefinition ability, CombatActorState target, AbilityTargetModifier modifier, DateTimeOffset now) => IsMage ? ActiveMageRuntime.Fire.ResolvePyromancerTargetAbilityModifier(ability, target, modifier, now) : modifier;
    private AbilityTargetModifier ResolveMageTargetAbilityModifier(AbilityDefinition ability, CombatActorState target, AbilityTargetModifier modifier, DateTimeOffset now) => IsMage ? ActiveMageRuntime.Frost.ResolveMageTargetAbilityModifier(ability, target, modifier, now) : modifier;
    private void OnPyromancerAbilityStarted(AbilityDefinition ability, DateTimeOffset now) { if (IsMage) ActiveMageRuntime.Fire.OnPyromancerAbilityStarted(ability, now); }
    private void OnMageAbilityStarted(AbilityDefinition ability, DateTimeOffset now) { if (IsMage) ActiveMageRuntime.Arcane.OnMageAbilityStarted(ability, now); }
    private void OnPyromancerAbilityResolved(AbilityDefinition ability, AbilityExecutionResult execution, DateTimeOffset now) { if (IsMage) ActiveMageRuntime.Fire.OnPyromancerAbilityResolved(ability, execution, now); }
    private void OnMageAbilityResolved(AbilityDefinition ability, AbilityExecutionResult execution, DateTimeOffset now) { if (IsMage) ActiveMageRuntime.Arcane.OnMageAbilityResolved(ability, execution, now); }
    private void ApplyPyromancerEnemyKilledHooks(CombatEvent input) { if (IsMage) ActiveMageRuntime.Fire.ApplyPyromancerEnemyKilledHooks(input); }
    private void OnPyromancerAbilityInterrupted(CombatEvent input) { if (IsMage) ActiveMageRuntime.Fire.OnPyromancerAbilityInterrupted(input); }
    private void ApplyMageResourceThresholdHooks(CombatEvent input) { if (IsMage) ActiveMageRuntime.Arcane.ApplyMageResourceThresholdHooks(input); }
    private void ApplyMageShieldAbsorbedHooks(CombatEvent input) { if (IsMage) ActiveMageRuntime.Arcane.ApplyMageShieldAbsorbedHooks(input); }

    private bool IsPlayerAbilityKnown(string abilityId, DateTimeOffset now) =>
        _player.KnownAbilityIds.Contains(abilityId)
        && (_player.DefinitionId != "WARRIOR" || GuardianTalentRuntimeCatalog.IsStandaloneAbility(abilityId, _player.KnownAbilityIds));

    private HashSet<string> GetPlayerKnownAbilityIds(DateTimeOffset now) =>
        new(_player.KnownAbilityIds.Where(id => _player.DefinitionId != "WARRIOR"
            || GuardianTalentRuntimeCatalog.IsStandaloneAbility(id, _player.KnownAbilityIds)), StringComparer.Ordinal);

    private static void ApplyPyromancerCriticalHooks(CombatEvent combatEvent)
    {
        // Critical-dependent Fire mechanics are resolved from AbilityExecutionResult so
        // multi-target casts cannot double-trigger shared player state.
    }

    private static void ApplyPyromancerIncomingCriticalHooks(CombatEvent combatEvent) { }

    private ActiveEffect? FindOwnEffect(CombatActorState actor, string effectId, DateTimeOffset now) =>
        actor.ActiveEffects.FirstOrDefault(effect =>
            string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
            && effect.SourceId == _player.Actor.ActorId
            && effect.ExpiresAtUtc > now);

    private bool HasOwnEffect(CombatActorState actor, string effectId, DateTimeOffset now) =>
        FindOwnEffect(actor, effectId, now) is not null;

    private bool IsBossEnemy(CombatActorState target) =>
        _enemiesById.TryGetValue(target.ActorId, out CombatParticipantDefinition? enemy)
        && string.Equals(enemy.MonsterRank?.ToString(), "Boss", StringComparison.OrdinalIgnoreCase);

    private static AbilityActionDefinition[]? ScaleSpellPower(IReadOnlyList<AbilityActionDefinition>? actions, decimal multiplier) =>
        actions?.Select(action => action.Type == AbilityActionType.Damage
            ? action with { SpellPowerCoefficient = action.SpellPowerCoefficient * multiplier }
            : action).ToArray();

    private static TimeSpan ClampCastTime(TimeSpan castTime) =>
        castTime <= TimeSpan.Zero ? TimeSpan.Zero : castTime < TimeSpan.FromMilliseconds(100) ? TimeSpan.FromMilliseconds(100) : castTime;

    private static bool DidHit(AbilityExecutionResult execution) =>
        execution.Events.Any(item => item.Type == CombatEventType.DamageDealt && item.Amount > 0);

    private static bool DidCrit(AbilityExecutionResult execution) =>
        execution.Events.Any(item => item.Type == CombatEventType.CriticalHit);

    private static bool IsFireDamageDefinition(string? definitionId) =>
        definitionId is not null && FireDamageDefinitionIds.Contains(definitionId);

    private static void ApplyMageCriticalHooks(CombatEvent combatEvent) { }

    private static void ApplyMageIncomingCriticalHooks(CombatEvent combatEvent) { }

    private static void ApplyMageDamageTakenHooks(CombatEvent combatEvent) { }

    private static void OnMageAbilityInterrupted(CombatEvent combatEvent) { }

    private void SyncActiveMageConditionalEffects(DateTimeOffset now)
    {
        CombatPlayerRuntimeState previous = _activePlayerState;
        try
        {
            foreach (CombatParticipantSnapshot participant in _participantRoster.Participants.Where(participant =>
                         participant.Status == CombatParticipantStatus.Active))
            {
                ActivatePlayer(participant.CharacterId);
                if (IsMage && !_player.Actor.IsDead)
                    SyncMageConditionalEffects(now);
            }
        }
        finally
        {
            _activePlayerState = previous;
        }
    }

    private decimal ResourcePercent() =>
        _player.Actor.MaxResource <= 0
            ? 0
            : _player.Actor.CurrentResource / _player.Actor.MaxResource * 100m;

    private bool HasMageTalent(string talentId) =>
        _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    private bool TryGetMageHook(string talentId, out ResolvedTalentEventHook hook)
    {
        hook = _playerTalents.EventHooks.FirstOrDefault(item =>
            string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!;
        return hook is not null;
    }

    private void ApplyMageEffect(
        CombatActorState target,
        EffectDefinition effect,
        DateTimeOffset now) =>
        ApplyTalentEffect(target, _player.Actor.ActorId, effect, now);

    private void RemoveMageEffect(
        CombatActorState target,
        string effectId,
        DateTimeOffset now) =>
        RemoveTalentEffects(EffectEngine.RemoveOwned(
            target,
            effectId,
            _player.Actor.ActorId,
            now));
}
