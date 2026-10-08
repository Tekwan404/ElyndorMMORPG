namespace Elyndor.Core.Combat.Abilities;

public static class AbilityResourceCostScaling
{
    public static AbilityDefinition Apply(AbilityDefinition ability, int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        if (ability.ResourceCostByLevel is not { } curve) return ability;
        if (!IsValid(curve)) throw new ArgumentException("Invalid level resource cost curve.", nameof(ability));
        decimal cost = curve[^1].Cost;
        for (int index = 1; index < curve.Count; index++)
        {
            if (level > curve[index].Level) continue;
            var lower = curve[index - 1];
            var upper = curve[index];
            cost = lower.Cost + (upper.Cost - lower.Cost) * (level - lower.Level) / (upper.Level - lower.Level);
            break;
        }
        if (level <= curve[0].Level) cost = curve[0].Cost;
        return ability with { ResourceCost = cost };
    }

    public static bool IsValid(IReadOnlyList<AbilityResourceCostPoint>? curve)
    {
        if (curve is null) return true;
        if (curve.Count == 0 || curve[0].Level != 1) return false;
        int previous = 0;
        foreach (var point in curve)
        {
            if (point.Level <= previous || point.Level > 60 || point.Cost < 0) return false;
            previous = point.Level;
        }
        return true;
    }
}
