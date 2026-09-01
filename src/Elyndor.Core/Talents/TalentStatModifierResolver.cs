namespace Elyndor.Core.Talents;

public sealed record TalentPrimaryStatPercentages(
    decimal Strength,
    decimal Agility,
    decimal Intellect,
    decimal Stamina)
{
    public static TalentPrimaryStatPercentages Empty { get; } = new(0, 0, 0, 0);
}

public static class TalentStatModifierResolver
{
    public static TalentPrimaryStatPercentages ResolvePrimaryPercentages(
        TalentTreeDefinition tree,
        IReadOnlyDictionary<string, int> selectedRanks)
    {
        decimal strength = 0;
        decimal agility = 0;
        decimal intellect = 0;
        decimal stamina = 0;
        foreach (TalentDefinition node in tree.Nodes)
        {
            int rank = selectedRanks.GetValueOrDefault(node.Id);
            if (rank <= 0) continue;
            foreach (TalentModifierDefinition modifier in node.Modifiers ?? [])
            {
                if (modifier.Type != TalentModifierType.StatModifier
                    || modifier.RuntimeStatus != TalentModifierRuntimeStatus.Supported
                    || modifier.Values.Count < rank) continue;
                decimal value = modifier.Values[rank - 1];
                switch (modifier.Key)
                {
                    case "STRENGTH_PERCENT": strength += value; break;
                    case "AGILITY_PERCENT": agility += value; break;
                    case "INTELLECT_PERCENT": intellect += value; break;
                    case "STAMINA_PERCENT": stamina += value; break;
                }
            }
        }
        return new(strength, agility, intellect, stamina);
    }
}
