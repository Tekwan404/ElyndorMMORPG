using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Abilities;

public static class AbilityEngine
{
    private static readonly TimeSpan StandardGcd = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan ShortGcd = TimeSpan.FromSeconds(0.75);

    public static AbilityExecutionResult Execute(
        CombatRuntimeState runtime,
        AbilityDefinition ability,
        AbilityIntent intent,
        DateTimeOffset now,
        IGameRandom? random = null)
    {
        EnsureExecutable(ability, random);
        if (!runtime.ProcessedCommandIds.Add(intent.CommandId))
        {
            return AbilityExecutionResult.Failure(AbilityErrorCode.DuplicateCommand);
        }

        AbilityErrorCode validation = Validate(runtime, ability, intent, now);
        if (validation != AbilityErrorCode.None)
        {
            return AbilityExecutionResult.Failure(validation);
        }

        if (!runtime.Actor.TrySpendResource(ability.ResourceCost))
        {
            return AbilityExecutionResult.Failure(AbilityErrorCode.InsufficientResource);
        }

        List<CombatEvent> events =
        [
            new(CombatEventType.ResourceChanged, now, runtime.Actor.ActorId, ability.Id, -ability.ResourceCost),
            new(CombatEventType.AbilityStarted, now, runtime.Actor.ActorId, ability.Id)
        ];
        StartGcd(runtime, ability, now);

        if (ability.Type == AbilityType.Casted)
        {
            runtime.ActiveCast = new ActiveCast(
                Guid.NewGuid(), ability, intent.TargetId, now, now + ability.CastTime);
        }
        else
        {
            events.AddRange(ResolveActions(runtime, ability, intent.TargetId, now, random));
            StartCooldown(runtime, ability, now);
            events.Add(new CombatEvent(CombatEventType.AbilityCompleted, now, runtime.Actor.ActorId, ability.Id));
        }

        runtime.Version++;
        return new AbilityExecutionResult(true, AbilityErrorCode.None, events);
    }

    public static AbilityExecutionResult CompleteCast(
        CombatRuntimeState runtime,
        DateTimeOffset now,
        IGameRandom? random = null)
    {
        ActiveCast? cast = runtime.ActiveCast;
        if (cast is null)
        {
            return AbilityExecutionResult.Failure(AbilityErrorCode.NoActiveCast);
        }

        if (now < cast.ResolvesAtUtc)
        {
            return AbilityExecutionResult.Failure(AbilityErrorCode.CastNotReady);
        }

        EnsureExecutable(cast.Ability, random);
        runtime.ActiveCast = null;
        StartCooldown(runtime, cast.Ability, now);
        runtime.Version++;
        List<CombatEvent> events = ResolveActions(runtime, cast.Ability, cast.TargetId, now, random);
        events.Add(new CombatEvent(CombatEventType.AbilityCompleted, now, runtime.Actor.ActorId, cast.Ability.Id));
        return new AbilityExecutionResult(true, AbilityErrorCode.None,
            events);
    }

    public static AbilityExecutionResult Interrupt(
        CombatRuntimeState runtime,
        DateTimeOffset now,
        TimeSpan lockoutDuration)
    {
        ActiveCast? cast = runtime.ActiveCast;
        if (cast is null)
        {
            return AbilityExecutionResult.Failure(AbilityErrorCode.NoActiveCast);
        }

        if (!cast.Ability.Interruptible)
        {
            return AbilityExecutionResult.Failure(AbilityErrorCode.CastNotInterruptible);
        }

        runtime.ActiveCast = null;
        if (lockoutDuration > TimeSpan.Zero)
        {
            runtime.SchoolLockouts[cast.Ability.School] = now + lockoutDuration;
        }

        runtime.Version++;
        return new AbilityExecutionResult(true, AbilityErrorCode.None,
        [
            new CombatEvent(CombatEventType.AbilityInterrupted, now, runtime.Actor.ActorId, cast.Ability.Id)
        ]);
    }

    private static AbilityErrorCode Validate(
        CombatRuntimeState runtime,
        AbilityDefinition ability,
        AbilityIntent intent,
        DateTimeOffset now)
    {
        if (runtime.Actor.IsDead) return AbilityErrorCode.DeadActor;
        if (!string.Equals(intent.AbilityId, ability.Id, StringComparison.Ordinal))
            return AbilityErrorCode.AbilityUnavailable;
        if (ability.TargetType is not (AbilityTargetType.Self
            or AbilityTargetType.SingleAlly
            or AbilityTargetType.SingleEnemy))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.Self && intent.TargetId != runtime.Actor.ActorId)
            return AbilityErrorCode.InvalidTarget;
        if (!runtime.Actors.TryGetValue(intent.TargetId, out CombatActorState? target) || target.IsDead)
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.SingleEnemy && intent.TargetId == runtime.Actor.ActorId)
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.SingleAlly
            && intent.TargetId == runtime.Actor.ActorId
            && !ability.AllowSelfTarget)
            return AbilityErrorCode.InvalidTarget;
        if (EffectEngine.HasControl(runtime.Actor, EffectKind.Stun, now)) return AbilityErrorCode.ActorStunned;
        if (!ability.CanUseWhileSilenced
            && EffectEngine.HasControl(runtime.Actor, EffectKind.Silence, now))
            return AbilityErrorCode.ActorSilenced;
        if (runtime.Cooldowns.TryGetValue(ability.Id, out DateTimeOffset cooldown) && cooldown > now)
            return AbilityErrorCode.CooldownActive;
        if (ability.UsesGlobalCooldown && runtime.GlobalCooldownEndsAtUtc > now)
            return AbilityErrorCode.GlobalCooldownActive;
        if (runtime.SchoolLockouts.TryGetValue(ability.School, out DateTimeOffset lockout) && lockout > now)
            return AbilityErrorCode.SchoolLocked;
        if (runtime.ActiveCast is not null && !ability.CanUseWhileCasting)
            return AbilityErrorCode.CastAlreadyActive;
        if (runtime.Actor.CurrentResource < ability.ResourceCost)
            return AbilityErrorCode.InsufficientResource;
        return AbilityErrorCode.None;
    }

    private static List<CombatEvent> ResolveActions(
        CombatRuntimeState runtime,
        AbilityDefinition ability,
        Guid targetId,
        DateTimeOffset now,
        IGameRandom? random)
    {
        List<CombatEvent> events = [];
        if (ability.Actions is null || ability.Actions.Count == 0)
        {
            return events;
        }

        CombatActorState target = runtime.Actors[targetId];
        foreach (AbilityActionDefinition action in ability.Actions)
        {
            switch (action.Type)
            {
                case AbilityActionType.Damage:
                    if (random is null)
                    {
                        throw new InvalidOperationException("Damage actions require an injected game RNG.");
                    }

                    decimal attackPower = EffectEngine.CalculateStat(
                        runtime.Actor, EffectStat.AttackPower,
                        runtime.Actor.Stats.AttackPower, now);
                    decimal baseDamage = action.Amount
                        + attackPower * Math.Max(0, action.AttackPowerCoefficient);
                    DamageResult damage = DamagePipeline.Resolve(
                        new DamageRequest(runtime.Actor, target, baseDamage, action.DamageType,
                            CanMiss: action.CanMiss, CanDodge: action.CanDodge,
                            CanCrit: action.CanCrit), random, now);
                    events.AddRange(damage.Events);
                    break;
                case AbilityActionType.Healing:
                    HealingResult healing = HealingPipeline.Resolve(new HealingRequest(target, action.Amount));
                    events.AddRange(healing.Events.Select(combatEvent => combatEvent with { OccurredAtUtc = now }));
                    break;
                case AbilityActionType.ApplyEffect:
                    if (action.Effect is null)
                    {
                        throw new InvalidOperationException("ApplyEffect actions require an effect definition.");
                    }

                    events.AddRange(EffectEngine.Apply(target, runtime.Actor.ActorId, action.Effect, now));
                    break;
                case AbilityActionType.ResourceChange:
                    decimal actualChange = runtime.Actor.AddResource(action.Amount);
                    events.Add(new CombatEvent(
                        CombatEventType.ResourceChanged, now, runtime.Actor.ActorId,
                        ability.Id, actualChange));
                    break;
                case AbilityActionType.Taunt:
                    events.Add(new CombatEvent(
                        CombatEventType.TauntApplied, now, target.ActorId,
                        ability.Id, (decimal)(action.Duration ?? TimeSpan.Zero).TotalSeconds));
                    break;
            }
        }

        return events;
    }

    private static void EnsureExecutable(AbilityDefinition ability, IGameRandom? random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ability.ResourceCost);
        if (ability.Actions?.Any(action => action.Type == AbilityActionType.Damage) == true
            && random is null)
        {
            throw new InvalidOperationException("Damage actions require an injected game RNG.");
        }
    }

    private static void StartCooldown(
        CombatRuntimeState runtime,
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (ability.Cooldown > TimeSpan.Zero)
        {
            runtime.Cooldowns[ability.Id] = now + ability.Cooldown;
        }
    }

    private static void StartGcd(
        CombatRuntimeState runtime,
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (!ability.UsesGlobalCooldown) return;
        TimeSpan duration = ability.GlobalCooldownCategory switch
        {
            GlobalCooldownCategory.Reduced => ShortGcd,
            GlobalCooldownCategory.Standard => StandardGcd,
            _ => TimeSpan.Zero
        };
        runtime.GlobalCooldownEndsAtUtc = now + duration;
    }
}
