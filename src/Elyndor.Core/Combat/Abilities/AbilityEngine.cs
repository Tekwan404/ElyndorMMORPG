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

        decimal resourceCost = ResolveResourceCost(runtime, ability, now);
        if (!runtime.Actor.TrySpendResource(resourceCost))
        {
            return AbilityExecutionResult.Failure(AbilityErrorCode.InsufficientResource);
        }

        List<CombatEvent> events =
        [
            new(CombatEventType.ResourceChanged, now, runtime.Actor.ActorId, ability.Id, -resourceCost),
            new(CombatEventType.AbilityStarted, now, runtime.Actor.ActorId, ability.Id)
        ];
        if (!string.IsNullOrWhiteSpace(ability.ConsumeEffectId))
        {
            events.AddRange(EffectEngine.Remove(
                runtime.Actor,
                ability.ConsumeEffectId,
                now));
        }
        StartGcd(runtime, ability, now);

        Guid[] targetIds = ResolveTargetIds(ability, intent);
        if (ability.Type == AbilityType.Casted)
        {
            runtime.ActiveCast = new ActiveCast(
                Guid.NewGuid(),
                ability,
                targetIds[0],
                now,
                now + ability.CastTime,
                targetIds.ToArray(),
                intent.TargetModifiers is null
                    ? null
                    : new Dictionary<Guid, AbilityTargetModifier>(intent.TargetModifiers));
        }
        else
        {
            events.AddRange(ResolveActions(
                runtime,
                ability,
                targetIds,
                intent.TargetModifiers,
                now,
                random));
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
        IReadOnlyList<Guid> targetIds = cast.TargetIds ?? [cast.TargetId];
        List<CombatEvent> events = ResolveActions(
            runtime,
            cast.Ability,
            targetIds,
            cast.TargetModifiers,
            now,
            random);
        events.Add(new CombatEvent(CombatEventType.AbilityCompleted, now, runtime.Actor.ActorId, cast.Ability.Id));
        return new AbilityExecutionResult(true, AbilityErrorCode.None, events);
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
        if (!string.IsNullOrWhiteSpace(ability.RequiredActiveEffectId)
            && !HasActiveEffect(runtime.Actor, ability.RequiredActiveEffectId, now))
        {
            return AbilityErrorCode.AbilityUnavailable;
        }
        if (ability.TargetType is not (AbilityTargetType.Self
            or AbilityTargetType.SingleAlly
            or AbilityTargetType.SingleEnemy
            or AbilityTargetType.AllEnemiesInCombat
            or AbilityTargetType.NEnemiesInCombat
            or AbilityTargetType.SelfAndPartyMembersInCombat
            or AbilityTargetType.Owner))
            return AbilityErrorCode.InvalidTarget;

        Guid[] targetIds = ResolveTargetIds(ability, intent);
        if (targetIds.Length == 0
            || targetIds.Distinct().Count() != targetIds.Length
            || targetIds.Any(targetId =>
                !runtime.Actors.TryGetValue(targetId, out CombatActorState? target)
                || target.IsDead))
        {
            return AbilityErrorCode.InvalidTarget;
        }

        if (ability.TargetType == AbilityTargetType.Self
            && (targetIds.Length != 1 || targetIds[0] != runtime.Actor.ActorId))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.SingleEnemy
            && (targetIds.Length != 1 || targetIds[0] == runtime.Actor.ActorId))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.SingleAlly
            && (targetIds.Length != 1
                || targetIds[0] == runtime.Actor.ActorId && !ability.AllowSelfTarget))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.Owner
            && (targetIds.Length != 1 || targetIds[0] == runtime.Actor.ActorId))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType is AbilityTargetType.AllEnemiesInCombat
            or AbilityTargetType.NEnemiesInCombat
            && targetIds.Any(targetId => targetId == runtime.Actor.ActorId))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.SelfAndPartyMembersInCombat
            && (targetIds.Length == 0 || !targetIds.Contains(runtime.Actor.ActorId)))
        {
            return AbilityErrorCode.InvalidTarget;
        }
        if (ability.TargetType == AbilityTargetType.AllEnemiesInCombat
            && ability.TargetCount > 0
            && targetIds.Length > ability.TargetCount)
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.NEnemiesInCombat
            && (ability.TargetCount <= 0 || targetIds.Length > ability.TargetCount))
            return AbilityErrorCode.InvalidTarget;
        if (!ability.CanUseWhileStunned
            && EffectEngine.HasControl(runtime.Actor, EffectKind.Stun, now))
            return AbilityErrorCode.ActorStunned;
        if (!ability.CanUseWhileSilenced
            && EffectEngine.HasControl(runtime.Actor, EffectKind.Silence, now))
            return AbilityErrorCode.ActorSilenced;
        if (ability.RequiresMobility
            && EffectEngine.HasControl(runtime.Actor, EffectKind.Root, now))
            return AbilityErrorCode.ActorRooted;
        if (!ability.CanUseWhileFeared
            && EffectEngine.HasControl(runtime.Actor, EffectKind.Fear, now))
            return AbilityErrorCode.ActorFeared;
        if (ability.RequiresWeapon
            && EffectEngine.HasControl(runtime.Actor, EffectKind.Disarm, now))
            return AbilityErrorCode.ActorDisarmed;
        if (runtime.Cooldowns.TryGetValue(ability.Id, out DateTimeOffset cooldown) && cooldown > now)
            return AbilityErrorCode.CooldownActive;
        if (ability.UsesGlobalCooldown && runtime.GlobalCooldownEndsAtUtc > now)
            return AbilityErrorCode.GlobalCooldownActive;
        if (runtime.SchoolLockouts.TryGetValue(ability.School, out DateTimeOffset lockout) && lockout > now)
            return AbilityErrorCode.SchoolLocked;
        if (runtime.ActiveCast is not null && !ability.CanUseWhileCasting)
            return AbilityErrorCode.CastAlreadyActive;
        if (runtime.Actor.CurrentResource < ResolveResourceCost(runtime, ability, now))
            return AbilityErrorCode.InsufficientResource;
        return AbilityErrorCode.None;
    }

    private static List<CombatEvent> ResolveActions(
        CombatRuntimeState runtime,
        AbilityDefinition ability,
        IReadOnlyList<Guid> targetIds,
        IReadOnlyDictionary<Guid, AbilityTargetModifier>? targetModifiers,
        DateTimeOffset now,
        IGameRandom? random)
    {
        List<CombatEvent> events = [];
        if (ability.Actions is null || ability.Actions.Count == 0)
        {
            return events;
        }

        foreach (Guid targetId in targetIds)
        {
            CombatActorState target = runtime.Actors[targetId];
            AbilityTargetModifier targetModifier =
                targetModifiers?.GetValueOrDefault(targetId)
                ?? new AbilityTargetModifier();
            foreach (AbilityActionDefinition action in ability.Actions)
            {
                if (action.Delay is { } delay && delay > TimeSpan.Zero)
                {
                    runtime.SchedulePendingAction(
                        ability,
                        action with { Delay = null },
                        targetId,
                        targetModifier,
                        now + delay);
                    continue;
                }

                switch (action.Type)
                {
                    case AbilityActionType.Damage:
                        if (random is null)
                        {
                            throw new InvalidOperationException("Damage actions require an injected game RNG.");
                        }

                        decimal attackPower = EffectEngine.CalculateStat(
                            runtime.Actor,
                            EffectStat.AttackPower,
                            runtime.Actor.Stats.AttackPower,
                            now);
                        decimal spellPower = runtime.Actor.Stats.SpellPower;
                        decimal blockValueMin = EffectEngine.CalculateStat(
                            runtime.Actor,
                            EffectStat.BlockValueMin,
                            runtime.Actor.Stats.BlockValueMin,
                            now);
                        decimal blockValueMax = EffectEngine.CalculateStat(
                            runtime.Actor,
                            EffectStat.BlockValueMax,
                            runtime.Actor.Stats.BlockValueMax,
                            now);
                        decimal averageBlockValue = Math.Max(0, (blockValueMin + blockValueMax) / 2m);
                        decimal levelDamage = Math.Max(0, runtime.Actor.Stats.Level - 1)
                            * Math.Max(0, action.DamagePerCharacterLevel);
                        decimal baseDamage = action.Amount
                            + levelDamage
                            + attackPower * Math.Max(0, action.AttackPowerCoefficient)
                            + spellPower * Math.Max(0, action.SpellPowerCoefficient)
                            + averageBlockValue * Math.Max(0, action.BlockValueCoefficient);
                        DamageResult damage = DamagePipeline.Resolve(
                            new DamageRequest(
                                runtime.Actor,
                                target,
                                baseDamage,
                                action.DamageType,
                                CanMiss: action.CanMiss,
                                CanDodge: action.CanDodge,
                                CanCrit: action.CanCrit,
                                DamageMultiplier: ability.DamageMultiplier
                                    * Math.Max(0, targetModifier.DamageMultiplier),
                                ArmorPenetrationBonus: action.ArmorPenetrationBonus
                                    + targetModifier.ArmorPenetrationBonus,
                                AccuracyBonus: ability.AccuracyBonus
                                    + targetModifier.AccuracyBonus,
                                CriticalChanceBonus: ability.CriticalChanceBonus
                                    + targetModifier.CriticalChanceBonus,
                                CriticalDamageBonus: ability.CriticalDamageBonus
                                    + targetModifier.CriticalDamageBonus,
                                MagicPenetrationBonus: ability.MagicPenetrationBonus
                                    + targetModifier.MagicPenetrationBonus,
                                IsUnblockable: action.IsUnblockable),
                            random,
                            now);
                        events.AddRange(damage.Events);
                        break;
                    case AbilityActionType.Healing:
                        HealingResult healing = HealingPipeline.Resolve(
                            new HealingRequest(
                                target,
                                action.Amount,
                                OccurredAtUtc: now,
                                Source: runtime.Actor,
                                CanCrit: action.HealingCanCrit,
                                CriticalChanceBonus: ability.CriticalChanceBonus
                                    + targetModifier.CriticalChanceBonus,
                                CriticalDamageBonus: ability.CriticalDamageBonus
                                    + targetModifier.CriticalDamageBonus,
                                SpellPowerCoefficient: action.SpellPowerCoefficient,
                                DefinitionId: ability.Id),
                            random);
                        events.AddRange(healing.Events);
                        break;
                    case AbilityActionType.ApplyEffect:
                        if (action.Effect is null)
                        {
                            throw new InvalidOperationException(
                                "ApplyEffect actions require an effect definition.");
                        }

                        events.AddRange(EffectEngine.Apply(
                            target,
                            runtime.Actor.ActorId,
                            action.Effect,
                            now));
                        break;
                    case AbilityActionType.ResourceChange:
                        CombatActorState resourceTarget = action.ResourceTarget == AbilityResourceTarget.Target
                            ? target
                            : runtime.Actor;
                        decimal actualChange = resourceTarget.AddResource(action.Amount);
                        events.Add(new CombatEvent(
                            CombatEventType.ResourceChanged,
                            now,
                            resourceTarget.ActorId,
                            ability.Id,
                            actualChange,
                            SourceActorId: runtime.Actor.ActorId,
                            TargetActorId: resourceTarget.ActorId));
                        break;
                    case AbilityActionType.Dispel:
                        if (string.IsNullOrWhiteSpace(action.DispelCategory))
                        {
                            throw new InvalidOperationException(
                                "Dispel actions require a dispel category.");
                        }

                        events.AddRange(EffectEngine.Dispel(
                            target,
                            action.DispelCategory,
                            now));
                        break;
                    case AbilityActionType.Taunt:
                        events.Add(new CombatEvent(
                            CombatEventType.TauntApplied,
                            now,
                            target.ActorId,
                            ability.Id,
                            (decimal)(action.Duration ?? TimeSpan.Zero).TotalSeconds));
                        break;
                    case AbilityActionType.Interrupt:
                        // Cross-runtime interruption is authoritative at the CombatSession layer.
                        // ResolveActions intentionally has no local actor-state mutation here.
                        break;
                }
            }
        }

        return events;
    }

    public static IReadOnlyList<CombatEvent> ResolvePendingActions(
        CombatRuntimeState runtime,
        DateTimeOffset now,
        IGameRandom? random = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        PendingAbilityAction[] due = runtime.PendingActions
            .Where(action => action.ExecuteAtUtc <= now)
            .OrderBy(action => action.ExecuteAtUtc)
            .ThenBy(action => action.Sequence)
            .ToArray();
        if (due.Length == 0)
            return [];

        List<CombatEvent> events = [];
        foreach (PendingAbilityAction pending in due)
        {
            runtime.PendingActions.Remove(pending);
            if (!runtime.Actors.TryGetValue(
                    pending.TargetId,
                    out CombatActorState? target)
                || target.IsDead)
            {
                continue;
            }

            AbilityDefinition delayedAbility = pending.Ability with
            {
                Actions = [pending.Action]
            };
            EnsureExecutable(delayedAbility, random);
            Dictionary<Guid, AbilityTargetModifier> targetModifiers = new()
            {
                [pending.TargetId] = pending.TargetModifier
            };
            IReadOnlyList<CombatEvent> resolved = ResolveActions(
                runtime,
                delayedAbility,
                [pending.TargetId],
                targetModifiers,
                pending.ExecuteAtUtc,
                random);
            events.AddRange(resolved.Select(combatEvent => combatEvent with
            {
                DefinitionId = combatEvent.DefinitionId ?? pending.Ability.Id,
                SourceActorId = combatEvent.SourceActorId ?? runtime.Actor.ActorId,
                TargetActorId = combatEvent.TargetActorId ?? pending.TargetId
            }));
        }

        runtime.Version++;
        return events;
    }

    private static Guid[] ResolveTargetIds(
        AbilityDefinition ability,
        AbilityIntent intent)
    {
        if (ability.TargetType is AbilityTargetType.AllEnemiesInCombat
            or AbilityTargetType.NEnemiesInCombat
            or AbilityTargetType.SelfAndPartyMembersInCombat)
        {
            return intent.TargetIds?.ToArray() ?? [];
        }

        return intent.TargetIds is { Count: > 0 }
            ? intent.TargetIds.ToArray()
            : [intent.TargetId];
    }

    private static decimal ResolveResourceCost(
        CombatRuntimeState runtime,
        AbilityDefinition ability,
        DateTimeOffset now) =>
        !string.IsNullOrWhiteSpace(ability.FreeResourceCostWhileEffectId)
        && HasActiveEffect(runtime.Actor, ability.FreeResourceCostWhileEffectId, now)
            ? 0
            : ability.ResourceCost;

    private static bool HasActiveEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        actor.ActiveEffects.Any(effect =>
            effect.ExpiresAtUtc > now
            && string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal));

    private static void EnsureExecutable(AbilityDefinition ability, IGameRandom? random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ability.ResourceCost);
        ArgumentOutOfRangeException.ThrowIfNegative(ability.DamageMultiplier);
        bool requiresRandom = ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            || action.Type == AbilityActionType.Healing && action.HealingCanCrit) == true;
        if (requiresRandom && random is null)
        {
            throw new InvalidOperationException(
                "Damage actions and critical healing actions require an injected game RNG.");
        }

        if (ability.Actions?.Any(action =>
                action.Type == AbilityActionType.Dispel
                && string.IsNullOrWhiteSpace(action.DispelCategory)) == true)
        {
            throw new InvalidOperationException(
                "Dispel actions require a dispel category.");
        }
        if (ability.Actions?.Any(action =>
                action.Delay is { } delay && delay < TimeSpan.Zero) == true)
        {
            throw new InvalidOperationException(
                "Ability action delay cannot be negative.");
        }
        if (ability.Actions?.Any(action =>
                action.Type == AbilityActionType.Interrupt
                && (action.InterruptLockout is null
                    || action.InterruptLockout < TimeSpan.Zero
                    || action.Delay is not null)) == true)
        {
            throw new InvalidOperationException(
                "Interrupt actions require a non-negative lockout and cannot be delayed.");
        }
        if (ability.Actions?.Any(action =>
                action.Type != AbilityActionType.Interrupt
                && action.InterruptLockout is not null) == true)
        {
            throw new InvalidOperationException(
                "Interrupt lockout is only valid for interrupt actions.");
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