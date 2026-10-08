using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Mage;

internal sealed record MageCombatContext(
    CombatParticipantDefinition Player,
    CombatRuntimeState Runtime,
    ResolvedTalentModifiers Talents,
    IReadOnlyDictionary<string, AbilityDefinition> Abilities,
    IReadOnlyDictionary<Guid, CombatParticipantDefinition> Enemies,
    IReadOnlyDictionary<Guid, CombatRuntimeState> EnemyRuntimes,
    IGameRandom Random,
    ProcGuard ProcGuard,
    DateTimeOffset StartedAtUtc,
    Func<Guid> SelectedTarget,
    Func<bool> IsActive,
    Action<IEnumerable<CombatEvent>, Guid, Guid, string?> Publish,
    Action<CombatActorState, decimal, DateTimeOffset, string> AddResource,
    Action<AbilityExecutionResult, string, Action> RunResolvedHooks);

internal abstract class MageRuntimeBase(MageCombatContext context, MageCombatRuntime owner)
{
    protected MageCombatContext Context { get; } = context;
    protected MageCombatRuntime Owner { get; } = owner;
    protected CombatParticipantDefinition _player => Context.Player;
    protected CombatRuntimeState _playerRuntime => Context.Runtime;
    protected ResolvedTalentModifiers _playerTalents => Context.Talents;
    protected IReadOnlyDictionary<string, AbilityDefinition> _abilities => Context.Abilities;
    protected IReadOnlyDictionary<Guid, CombatParticipantDefinition> _enemiesById => Context.Enemies;
    protected IReadOnlyDictionary<Guid, CombatRuntimeState> _enemyRuntimes => Context.EnemyRuntimes;
    protected IGameRandom _random => Context.Random;
    protected ProcGuard _procGuard => Context.ProcGuard;
    protected Guid _selectedTargetActorId => Context.SelectedTarget();
    protected DateTimeOffset _combatStartedAtUtc => Context.StartedAtUtc;
    protected bool IsMage => _player.DefinitionId == "MAGE";
    protected void ApplyKernelEvents(IEnumerable<CombatEvent> events, Guid source, Guid target, string? id) => Context.Publish(events, source, target, id);
    protected void AddResource(CombatActorState actor, decimal amount, DateTimeOffset now, string id) => Context.AddResource(actor, amount, now, id);
    protected void RunResolvedProcHooks(AbilityExecutionResult result, string path, Action action) => Context.RunResolvedHooks(result, path, action);
    protected void ApplyTalentEffect(CombatActorState target, Guid source, EffectDefinition effect, DateTimeOffset now) => ApplyKernelEvents(EffectEngine.Apply(target, source, effect, now), source, target.ActorId, effect.Id);
    protected void RemoveTalentEffects(IEnumerable<CombatEvent> events) => ApplyKernelEvents(events, _player.Actor.ActorId, _player.Actor.ActorId, null);
    protected bool TalentCooldownReady(string id, DateTimeOffset now) => _procGuard.IsReady(_player.Actor.ActorId, id, now);
    protected void StartTalentCooldown(ResolvedTalentEventHook hook, DateTimeOffset now) => _procGuard.StartCooldown(_player.Actor.ActorId, hook.TalentId, now, hook.InternalCooldown);
    protected static decimal HpPercent(CombatActorState actor) => actor.MaxHp <= 0 ? 0 : actor.CurrentHp / actor.MaxHp * 100m;
    protected static bool ReduceCooldown(CombatRuntimeState runtime, string id, TimeSpan reduction, DateTimeOffset now)
    {
        if (!runtime.Cooldowns.TryGetValue(id, out var ready) || ready <= now || reduction <= TimeSpan.Zero) return false;
        if (ready - reduction <= now) runtime.Cooldowns.Remove(id);
        else runtime.Cooldowns[id] = ready - reduction;
        return true;
    }
    internal const string FireballId = "MAGE_FIREBALL";
    internal const string ArcaneSparkId = "MAGE_ARCANE_SPARK";
    internal const string IceShardId = "MAGE_ICE_SHARD";
    internal const string FireBlastId = "MAGE_FIRE_BLAST";
    internal const string ScorchId = "MAGE_SCORCH";
    internal const string PyroblastId = "MAGE_PYROBLAST";
    internal const string FlamestrikeId = "MAGE_FLAMESTRIKE";
    internal const string BlastWaveId = "MAGE_BLAST_WAVE";
    internal const string CombustionId = "MAGE_COMBUSTION";
    internal const string IgniteEffectId = "MAGE_FIRE_IGNITE";
    internal const string FireVulnerabilityEffectId = "MAGE_FIRE_VULNERABILITY";
    internal const string BlastWaveSlowEffectId = "MAGE_BLAST_WAVE_SLOW";
    internal const string KindlingEffectId = "MAGE_KINDLING_FLAME";
    internal const string HeatDiscountEffectId = "MAGE_HEAT_DISCOUNT";
    internal const string CombustionEffectId = "MAGE_COMBUSTION_ACTIVE";
    internal const string CombustionCritStackEffectId = "MAGE_COMBUSTION_CRIT_STACK";
    internal const string CombustionFirstPyroEffectId = "MAGE_COMBUSTION_FIRST_PYRO";
    internal const string PyromaniacEffectId = "MAGE_PYROMANIAC";
    internal const string PyroclasmEffectId = "MAGE_PYROCLASM";
    internal const string HotStreakEffectId = "MAGE_HOT_STREAK";
    internal const string PyroblastBurnEffectId = "MAGE_PYROBLAST_BURN";
    internal const string FlamestrikeBurnEffectId = "MAGE_FLAMESTRIKE_BURN";
    internal const string CombustionPulseEffectId = "MAGE_COMBUSTION_PULSE";
    internal const string ArcaneMissilesId = "MAGE_ARCANE_MISSILES";
    internal const string ArcaneExplosionId = "MAGE_ARCANE_EXPLOSION";
    internal const string ManaShieldId = "MAGE_MANA_SHIELD";
    internal const string CounterspellId = "MAGE_COUNTERSPELL";
    internal const string PresenceOfMindId = "MAGE_PRESENCE_OF_MIND";
    internal const string ArcanePowerId = "MAGE_ARCANE_POWER";
    internal const string EvocationId = "MAGE_EVOCATION";
    internal const string FrostNovaId = "MAGE_FROST_NOVA";
    internal const string BlizzardId = "MAGE_BLIZZARD";
    internal const string ColdSnapId = "MAGE_COLD_SNAP";
    internal const string IceBlockId = "MAGE_ICE_BLOCK";
    internal const string ConeOfColdId = "MAGE_CONE_OF_COLD";
    internal const string IceBarrierId = "MAGE_ICE_BARRIER";
    internal const string IceLanceId = "MAGE_ICE_LANCE";
    internal const string ClearcastingEffectId = "MAGE_CLEARCASTING";
    internal const string ClearcastingRegenEffectId = "MAGE_CLEARCASTING_REGEN";
    internal const string PresenceOfMindEffectId = "MAGE_PRESENCE_OF_MIND_ACTIVE";
    internal const string ArcanePowerEffectId = "MAGE_ARCANE_POWER_ACTIVE";
    internal const string ArcanePowerFreeCostEffectId = "MAGE_ARCANE_POWER_FREE_COST";
    internal const string ArcaneFortitudeEffectId = "MAGE_ARCANE_FORTITUDE";
    internal const string ManaShieldEffectId = "MAGE_MANA_SHIELD_EFFECT";
    internal const string ManaShieldEfficiencyEffectId = "MAGE_MANA_SHIELD_EFFICIENCY";
    internal const string ArcaneEchoEffectId = "MAGE_ARCANE_ECHO";
    internal const string ChillEffectId = "MAGE_CHILL";
    internal const string FreezeEffectId = "MAGE_FREEZE";
    internal const string DeepChillEffectId = "MAGE_DEEP_CHILL";
    internal const string WinterChillEffectId = "MAGE_WINTERS_CHILL";
    internal const string FrostExtendedEffectId = "MAGE_FROST_EXTENDED";
    internal const string IceBlockEffectId = "MAGE_ICE_BLOCK_ACTIVE";
    internal const string IceBlockImmunityEffectId = "MAGE_ICE_BLOCK_IMMUNITY";
    internal const string ColdBloodEffectId = "MAGE_COLD_BLOOD";
    internal const string IceBarrierEffectId = "MAGE_ICE_BARRIER_EFFECT";
    internal const string FrostArmorEffectId = "MAGE_FROST_ARMOR";
    internal const string EmergencyIceEffectId = "MAGE_EMERGENCY_ICE";
    internal const string ColdSnapLanceEffectId = "MAGE_COLD_SNAP_LANCE";
    protected ActiveEffect? FindOwnEffect(CombatActorState actor, string effectId, DateTimeOffset now) =>
        actor.ActiveEffects.FirstOrDefault(effect =>
            string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
            && effect.SourceId == _player.Actor.ActorId
            && effect.ExpiresAtUtc > now);

    protected bool HasOwnEffect(CombatActorState actor, string effectId, DateTimeOffset now) =>
        FindOwnEffect(actor, effectId, now) is not null;

    protected bool IsBossEnemy(CombatActorState target) =>
        _enemiesById.TryGetValue(target.ActorId, out CombatParticipantDefinition? enemy)
        && string.Equals(enemy.MonsterRank?.ToString(), "Boss", StringComparison.OrdinalIgnoreCase);

    protected static AbilityActionDefinition[]? ScaleSpellPower(IReadOnlyList<AbilityActionDefinition>? actions, decimal multiplier) =>
        actions?.Select(action => action.Type == AbilityActionType.Damage
            ? action with { SpellPowerCoefficient = action.SpellPowerCoefficient * multiplier }
            : action).ToArray();

    protected static TimeSpan ClampCastTime(TimeSpan castTime) =>
        castTime <= TimeSpan.Zero ? TimeSpan.Zero : castTime < TimeSpan.FromMilliseconds(100) ? TimeSpan.FromMilliseconds(100) : castTime;

    protected static bool DidHit(AbilityExecutionResult execution) =>
        execution.Events.Any(item => item.Type == CombatEventType.DamageDealt && item.Amount > 0);

    protected static bool DidCrit(AbilityExecutionResult execution) =>
        execution.Events.Any(item => item.Type == CombatEventType.CriticalHit);

    protected void ConsumeEffectStack(
        CombatActorState actor,
        ActiveEffect effect,
        string effectId,
        DateTimeOffset now)
    {
        effect.Stacks = Math.Max(0, effect.Stacks - 1);
        if (effect.Stacks == 0)
            RemoveMageEffect(actor, effectId, now);
    }

    protected void ApplyMageShield(
        string effectId,
        decimal amount,
        TimeSpan duration,
        DateTimeOffset now)
    {
        if (amount <= 0 || duration <= TimeSpan.Zero) return;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            effectId, EffectKind.Shield, duration, 1,
            EffectStackPolicy.Replace, amount), now);
    }

    protected void ApplyDelayedMageDamage(
        CombatActorState target,
        string effectId,
        decimal amount,
        TimeSpan delay,
        DateTimeOffset now)
    {
        if (target.IsDead || amount <= 0 || delay <= TimeSpan.Zero) return;
        ApplyMageEffect(target, new EffectDefinition(
            effectId, EffectKind.DamageOverTime, delay, 1,
            EffectStackPolicy.Replace, amount, delay,
            SourceSpecific: true, PeriodicDamageType: DamageType.Magical), now);
    }

    protected IEnumerable<CombatActorState> HitTargets(AbilityExecutionResult execution) =>
        execution.Events
            .Where(item => item.Type == CombatEventType.DamageDealt
                && item.TargetActorId.HasValue
                && item.Amount > 0)
            .Select(item => item.TargetActorId!.Value)
            .Distinct()
            .Where(_enemiesById.ContainsKey)
            .Select(id => _enemiesById[id].Actor);

    protected static ActiveEffect? FindAnyActiveEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        actor.ActiveEffects.FirstOrDefault(effect =>
            string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
            && effect.ExpiresAtUtc > now);

    protected decimal ResourcePercent() =>
        _player.Actor.MaxResource <= 0
            ? 0
            : _player.Actor.CurrentResource / _player.Actor.MaxResource * 100m;

    protected bool HasMageTalent(string talentId) =>
        _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    protected bool TryGetMageHook(string talentId, out ResolvedTalentEventHook hook)
    {
        hook = _playerTalents.EventHooks.FirstOrDefault(item =>
            string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!;
        return hook is not null;
    }

    protected void ApplyMageEffect(
        CombatActorState target,
        EffectDefinition effect,
        DateTimeOffset now) =>
        ApplyTalentEffect(target, _player.Actor.ActorId, effect, now);

    protected void RemoveMageEffect(
        CombatActorState target,
        string effectId,
        DateTimeOffset now) =>
        RemoveTalentEffects(EffectEngine.RemoveOwned(
            target,
            effectId,
            _player.Actor.ActorId,
            now));

    protected static bool IsOffensiveMageAbility(AbilityDefinition ability) =>
        ability.IsSpell
        && ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            && action.DamageType == DamageType.Magical) == true;
    protected void SyncIncomingDamageReductionEffect(
        string effectId,
        decimal reductionPercent,
        DateTimeOffset now)
    {
        if (reductionPercent <= 0)
        {
            RemoveMageEffect(_player.Actor, effectId, now);
            return;
        }

        if (HasOwnEffect(_player.Actor, effectId, now)) return;
        ApplyMageEffect(_player.Actor, new EffectDefinition(
            effectId, EffectKind.StatModifier, TimeSpan.FromHours(12), 1,
            EffectStackPolicy.Replace, Math.Max(0, 1 - reductionPercent / 100m),
            ModifiedStat: EffectStat.IncomingDamageMultiplier,
            ModifierMode: EffectModifierMode.Multiplicative), now);
    }
}
