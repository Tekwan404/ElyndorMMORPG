namespace Elyndor.Core.Combat.SetPassives;

public sealed class SetPassiveCharges
{
    private readonly Dictionary<string, (SetPassiveActionDefinition Action, DateTimeOffset Expires)> _charges = new(StringComparer.Ordinal);

    public void Arm(SetPassiveActionDefinition action, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action.ReferenceId);
        if (_charges.TryGetValue(action.ReferenceId, out var current) && current.Expires > now
            && current.Action.Magnitude > action.Magnitude) return;
        _charges[action.ReferenceId] = (action, now + (action.Duration ?? TimeSpan.FromSeconds(8)));
    }

    public decimal Consume(string abilityId, bool isSpell, DateTimeOffset now, bool healing = false, bool effects = false, Func<SetPassiveActionDefinition, bool>? filter = null)
    {
        decimal multiplier = 1;
        foreach (var (id, charge) in _charges.ToArray())
        {
            if (charge.Expires <= now)
            {
                _charges.Remove(id);
                continue;
            }
            SetPassiveActionDefinition action = charge.Action;
            if (action.Kind != (effects ? SetPassiveActionKind.EmpowerNextEffect : SetPassiveActionKind.EmpowerNextDirect)
                || !action.HealingOrDamage && action.Healing != healing || action.SpellOnly && !isSpell
                || action.ClassAbilityOnly && abilityId == "AUTO_ATTACK"
                || action.AbilityIds is { Count: > 0 } && !action.AbilityIds.Contains(abilityId, StringComparer.Ordinal))
                continue;
            if (filter is not null && !filter(action)) continue;
            multiplier *= 1 + action.Magnitude;
            _charges.Remove(id);
        }
        return multiplier;
    }

}
