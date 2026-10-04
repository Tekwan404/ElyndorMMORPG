namespace Elyndor.Core.Combat.Resources;

/// <summary>
/// Mutates authoritative actor resource once and creates its result notification.
/// Publication and reaction routing remain separate: a result is never a command.
/// </summary>
public sealed class CombatResourceRuntime(
    Action<CombatEvent> publish,
    Func<string, decimal, decimal>? scaleGrant = null,
    Func<string, bool>? isTalentGrant = null,
    Func<decimal>? directDamageRage = null)
{
    public static CombatEvent Change(
        CombatActorState actor, decimal amount, DateTimeOffset now,
        string definitionId, Guid sourceActorId) =>
        new(CombatEventType.ResourceChanged, now, actor.ActorId, definitionId,
            actor.AddResource(amount), SourceActorId: sourceActorId, TargetActorId: actor.ActorId);

    public static bool TrySpend(
        CombatActorState actor, decimal cost, DateTimeOffset now,
        string definitionId, out CombatEvent? result)
    {
        result = null;
        if (!actor.TrySpendResource(cost)) return false;
        // Preserve the engine's zero-cost event and later identity normalization.
        result = new(CombatEventType.ResourceChanged, now, actor.ActorId, definitionId, -cost);
        return true;
    }

    public decimal Grant(CombatActorState actor, decimal amount, DateTimeOffset now, string definitionId)
    {
        CombatEvent result = Change(actor, scaleGrant?.Invoke(definitionId, amount) ?? amount,
            now, definitionId, actor.ActorId);
        if (result.Amount == 0) return 0;
        bool talent = isTalentGrant?.Invoke(definitionId) ?? false;
        publish(result with
        {
            IsProc = talent,
            ProcDepth = talent ? 1 : 0,
            ProcOriginId = talent ? definitionId : null
        });
        return result.Amount;
    }

    // Called at the existing router's admitted direct-damage reaction, not on every
    // ResourceChanged event. Class adapters supply the original gain formula.
    public void GenerateDirectDamageTaken(CombatActorState actor, DateTimeOffset now) =>
        Grant(actor, directDamageRage?.Invoke() ?? 0, now, "DIRECT_DAMAGE_TAKEN");

    public void Regenerate(
        CombatActorState actor, DateTimeOffset from, DateTimeOffset to,
        Func<DateTimeOffset, decimal> rateAt,
        IEnumerable<DateTimeOffset>? rateBoundaries = null)
    {
        if (to <= from || actor.IsDead) return;
        var boundaries = new SortedSet<DateTimeOffset> { from, to };
        if (rateBoundaries is not null)
            foreach (DateTimeOffset boundary in rateBoundaries)
                if (boundary > from && boundary < to) boundaries.Add(boundary);

        decimal amount = 0;
        DateTimeOffset start = from;
        foreach (DateTimeOffset end in boundaries.Skip(1))
        {
            amount += rateAt(start) * (end - start).Ticks / TimeSpan.TicksPerSecond;
            start = end;
        }
        if (amount > 0) Grant(actor, amount, to, "COMBAT_REGEN");
    }
}
