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
            .Where(effect => effect.Definition.Id == definition.Id)
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
                target.ActiveEffects.RemoveAll(effect => effect.Definition.Id == definition.Id);
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

        events.Add(new CombatEvent(CombatEventType.EffectRefreshed, now, target.ActorId, definition.Id));
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
            while (effect.NextTickAtUtc is { } tickAt
                   && tickAt <= now
                   && tickAt <= effect.ExpiresAtUtc)
            {
                decimal amount = effect.Definition.Magnitude * effect.Stacks;
                if (effect.Definition.Kind == EffectKind.DamageOverTime)
                {
                    target.ApplyDamage(amount);
                }
                else if (effect.Definition.Kind == EffectKind.HealingOverTime && !target.IsDead)
                {
                    target.ApplyHealing(amount);
                }

                events.Add(new CombatEvent(
                    CombatEventType.EffectTicked, tickAt, target.ActorId, effect.Definition.Id, amount));
                effect.NextTickAtUtc = tickAt + effect.Definition.TickInterval!.Value;
            }
        }

        foreach (ActiveEffect expired in target.ActiveEffects
                     .Where(effect => effect.ExpiresAtUtc <= now)
                     .OrderBy(effect => effect.Sequence)
                     .ToArray())
        {
            target.ActiveEffects.Remove(expired);
            events.Add(new CombatEvent(
                CombatEventType.EffectExpired, expired.ExpiresAtUtc, target.ActorId, expired.Definition.Id));
        }

        return events;
    }

    public static bool HasControl(CombatActorState target, EffectKind kind, DateTimeOffset now) =>
        target.ActiveEffects.Any(effect => effect.Definition.Kind == kind && effect.ExpiresAtUtc > now);

    public static IReadOnlyList<CombatEvent> Dispel(
        CombatActorState target,
        string dispelCategory,
        DateTimeOffset now)
    {
        ActiveEffect[] removed = target.ActiveEffects
            .Where(effect => string.Equals(effect.Definition.DispelCategory, dispelCategory, StringComparison.Ordinal))
            .ToArray();
        foreach (ActiveEffect effect in removed)
        {
            target.ActiveEffects.Remove(effect);
        }

        return removed.Select(effect => new CombatEvent(
            CombatEventType.EffectRemoved, now, target.ActorId, effect.Definition.Id)).ToArray();
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
        events.Add(new CombatEvent(CombatEventType.EffectApplied, now, target.ActorId, definition.Id));
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
