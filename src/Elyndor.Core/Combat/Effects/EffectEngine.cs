namespace Elyndor.Core.Combat.Effects;

public static class EffectEngine
{
    public static IReadOnlyList<CombatEvent> Apply(
        CombatActorState target,
        Guid sourceId,
        EffectDefinition definition,
        DateTimeOffset now)
    {
        Validate(definition);
        List<CombatEvent> events = [];
        List<ActiveEffect> matching = target.ActiveEffects
            .Where(effect =>
                effect.Definition.Id == definition.Id
                && (!definition.SourceSpecific || effect.SourceId == sourceId))
            .OrderBy(effect => effect.Sequence)
            .ToList();

        if (definition.StackPolicy == EffectStackPolicy.Independent || matching.Count == 0)
        {
            AddNew(target, sourceId, definition, now, events);
            return events;
        }

        ActiveEffect current = matching[^1];
        switch (definition.StackPolicy)
        {
            case EffectStackPolicy.Stack:
                current.Stacks = Math.Min(definition.MaxStacks, current.Stacks + 1);
                Refresh(current, now);
                break;
            case EffectStackPolicy.Refresh:
                Refresh(current, now);
                break;
            case EffectStackPolicy.Replace:
                target.ActiveEffects.RemoveAll(effect =>
                    effect.Definition.Id == definition.Id
                    && (!definition.SourceSpecific || effect.SourceId == sourceId));
                AddNew(target, sourceId, definition, now, events);
                return events;
            case EffectStackPolicy.StrongestWins:
                if (definition.Magnitude >= current.RemainingMagnitude)
                {
                    target.ActiveEffects.Remove(current);
                    AddNew(target, sourceId, definition, now, events);
                    return events;
                }

                return events;
        }

        events.Add(new CombatEvent(
            CombatEventType.EffectRefreshed,
            now,
            target.ActorId,
            definition.Id,
            SourceActorId: sourceId,
            TargetActorId: target.ActorId));
        return events;
    }

    public static IReadOnlyList<CombatEvent> Process(CombatActorState target, DateTimeOffset now)
    {
        List<CombatEvent> events = [];
        ActiveEffect[] snapshot = target.ActiveEffects
            .OrderBy(effect => effect.NextTickAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(effect => effect.Definition.ApplicationPriority)
            .ThenBy(effect => effect.Sequence)
            .ToArray();

        foreach (ActiveEffect effect in snapshot)
        {
            if (target.IsDead) break;

            while (effect.NextTickAtUtc is { } tickAt
                   && tickAt <= now
                   && tickAt <= effect.ExpiresAtUtc
                   && !target.IsDead)
            {
                decimal requested = effect.Definition.Magnitude * effect.Stacks;
                decimal actual = 0;

                if (effect.Definition.Kind == EffectKind.DamageOverTime)
                {
                    decimal previousHp = target.CurrentHp;
                    target.ApplyDamage(requested);
                    actual = previousHp - target.CurrentHp;
                }
                else if (effect.Definition.Kind == EffectKind.HealingOverTime)
                {
                    decimal previousHp = target.CurrentHp;
                    target.ApplyHealing(requested);
                    actual = target.CurrentHp - previousHp;
                }

                events.Add(new CombatEvent(
                    CombatEventType.EffectTicked,
                    tickAt,
                    target.ActorId,
                    effect.Definition.Id,
                    actual,
                    SourceActorId: effect.SourceId,
                    TargetActorId: target.ActorId,
                    IsPeriodic: true,
                    AmountBeforeShields: actual));

                if (effect.Definition.Kind == EffectKind.DamageOverTime && actual > 0)
                {
                    events.Add(new CombatEvent(
                        CombatEventType.DamageDealt,
                        tickAt,
                        target.ActorId,
                        effect.Definition.Id,
                        actual,
                        SourceActorId: effect.SourceId,
                        TargetActorId: target.ActorId,
                        IsPeriodic: true,
                        AmountBeforeShields: actual));
                }
                else if (effect.Definition.Kind == EffectKind.HealingOverTime && actual > 0)
                {
                    events.Add(new CombatEvent(
                        CombatEventType.HealingApplied,
                        tickAt,
                        target.ActorId,
                        effect.Definition.Id,
                        actual,
                        SourceActorId: effect.SourceId,
                        TargetActorId: target.ActorId,
                        IsPeriodic: true));
                }

                effect.NextTickAtUtc = tickAt + effect.Definition.TickInterval!.Value;

                if (target.IsDead)
                {
                    events.Add(new CombatEvent(
                        CombatEventType.ActorDied,
                        tickAt,
                        target.ActorId,
                        effect.Definition.Id,
                        SourceActorId: effect.SourceId,
                        TargetActorId: target.ActorId,
                        IsPeriodic: true));
                }
            }
        }

        foreach (ActiveEffect expired in target.ActiveEffects
                     .Where(effect => effect.ExpiresAtUtc <= now)
                     .OrderBy(effect => effect.Sequence)
                     .ToArray())
        {
            target.ActiveEffects.Remove(expired);
            events.Add(new CombatEvent(
                CombatEventType.EffectExpired,
                expired.ExpiresAtUtc,
                target.ActorId,
                expired.Definition.Id,
                SourceActorId: expired.SourceId,
                TargetActorId: target.ActorId));
        }

        return events;
    }

    public static bool HasControl(CombatActorState target, EffectKind kind, DateTimeOffset now) =>
        target.ActiveEffects.Any(effect => effect.Definition.Kind == kind && effect.ExpiresAtUtc > now);

    public static decimal CalculateStat(
        CombatActorState target,
        EffectStat stat,
        decimal baseValue,
        DateTimeOffset now,
        Guid? sourceId = null)
    {
        ActiveEffect[] modifiers = target.ActiveEffects
            .Where(effect => effect.ExpiresAtUtc > now
                && effect.Definition.Kind == EffectKind.StatModifier
                && effect.Definition.ModifiedStat == stat
                && (!effect.Definition.SourceSpecific
                    || sourceId.HasValue && effect.SourceId == sourceId.Value))
            .ToArray();
        decimal flat = modifiers
            .Where(effect => effect.Definition.ModifierMode == EffectModifierMode.Flat)
            .Sum(effect => effect.Definition.Magnitude * effect.Stacks);
        decimal percent = modifiers
            .Where(effect => effect.Definition.ModifierMode == EffectModifierMode.Percent)
            .Sum(effect => effect.Definition.Magnitude * effect.Stacks);
        decimal multiplier = modifiers
            .Where(effect => effect.Definition.ModifierMode == EffectModifierMode.Multiplicative)
            .Aggregate(1m, (current, effect) => current * effect.Definition.Magnitude);
        return Math.Max(0, (baseValue + flat) * (1 + percent) * multiplier);
    }

    public static IReadOnlyList<CombatEvent> Dispel(
        CombatActorState target,
        string dispelCategory,
        DateTimeOffset now)
    {
        ActiveEffect[] removed = target.ActiveEffects
            .Where(effect => string.Equals(
                effect.Definition.DispelCategory,
                dispelCategory,
                StringComparison.Ordinal))
            .ToArray();
        return RemoveEffects(target, removed, now);
    }

    public static IReadOnlyList<CombatEvent> Remove(
        CombatActorState target,
        string definitionId,
        DateTimeOffset now)
    {
        ActiveEffect[] removed = target.ActiveEffects
            .Where(effect => string.Equals(
                effect.Definition.Id,
                definitionId,
                StringComparison.Ordinal))
            .ToArray();
        return RemoveEffects(target, removed, now);
    }

    public static IReadOnlyList<CombatEvent> RemoveByKind(
        CombatActorState target,
        EffectKind kind,
        DateTimeOffset now)
    {
        ActiveEffect[] removed = target.ActiveEffects
            .Where(effect => effect.Definition.Kind == kind)
            .ToArray();
        return RemoveEffects(target, removed, now);
    }

    private static IReadOnlyList<CombatEvent> RemoveEffects(
        CombatActorState target,
        IReadOnlyList<ActiveEffect> removed,
        DateTimeOffset now)
    {
        foreach (ActiveEffect effect in removed)
        {
            target.ActiveEffects.Remove(effect);
        }

        return removed.Select(effect => new CombatEvent(
            CombatEventType.EffectRemoved,
            now,
            target.ActorId,
            effect.Definition.Id,
            SourceActorId: effect.SourceId,
            TargetActorId: target.ActorId)).ToArray();
    }

    private static void AddNew(
        CombatActorState target,
        Guid sourceId,
        EffectDefinition definition,
        DateTimeOffset now,
        List<CombatEvent> events)
    {
        long sequence = target.ActiveEffects.Count == 0
            ? 1
            : target.ActiveEffects.Max(existing => existing.Sequence) + 1;
        ActiveEffect effect = new(sequence, sourceId, target.ActorId, definition, now);
        target.ActiveEffects.Add(effect);
        events.Add(new CombatEvent(
            CombatEventType.EffectApplied,
            now,
            target.ActorId,
            definition.Id,
            SourceActorId: sourceId,
            TargetActorId: target.ActorId));
    }

    private static void Refresh(ActiveEffect effect, DateTimeOffset now)
    {
        effect.AppliedAtUtc = now;
        effect.ExpiresAtUtc = now + effect.Definition.Duration;
        effect.NextTickAtUtc = effect.Definition.TickInterval is { } interval ? now + interval : null;
    }

    private static void Validate(EffectDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id)
            || definition.Duration <= TimeSpan.Zero
            || definition.MaxStacks <= 0
            || definition.Magnitude < 0
            || definition.TickInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Effect definition contains invalid values.", nameof(definition));
        }

        bool periodic = definition.Kind is EffectKind.DamageOverTime or EffectKind.HealingOverTime;
        if (periodic != definition.TickInterval.HasValue)
        {
            throw new ArgumentException("Only periodic effects require a tick interval.", nameof(definition));
        }
    }
}
