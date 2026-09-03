namespace Elyndor.Core.World;

public static class WorldEncounterSelector
{
    public static LocationEncounterDefinition Select(
        IReadOnlyList<LocationEncounterDefinition> encounters,
        decimal roll)
    {
        ArgumentNullException.ThrowIfNull(encounters);
        if (encounters.Count == 0)
            throw new ArgumentException("At least one encounter is required.", nameof(encounters));
        if (roll is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(roll), "Encounter roll must be in [0, 1). ");

        decimal totalWeight = encounters.Sum(encounter => encounter.Weight);
        if (totalWeight <= 0)
            throw new ArgumentException("Encounter weights must have a positive total.", nameof(encounters));

        decimal target = roll * totalWeight;
        decimal cumulative = 0;
        foreach (LocationEncounterDefinition encounter in encounters)
        {
            if (encounter.Weight <= 0)
                throw new ArgumentException("Encounter weights must be positive.", nameof(encounters));

            cumulative += encounter.Weight;
            if (target < cumulative)
                return encounter;
        }

        return encounters[^1];
    }
}
