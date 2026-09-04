namespace Elyndor.Core.Characters;

public static class CharacterVitalsScaler
{
    public static void ScaleToDerivedMaximums(
        CharacterVitals vitals,
        decimal oldMaxHp,
        decimal newMaxHp,
        decimal oldMaxResource,
        decimal newMaxResource,
        DateTimeOffset atUtc)
    {
        ArgumentNullException.ThrowIfNull(vitals);
        vitals.Checkpoint(
            Scale(vitals.CurrentHp, oldMaxHp, newMaxHp),
            Scale(vitals.CurrentResource, oldMaxResource, newMaxResource),
            atUtc);
    }

    public static decimal Scale(decimal current, decimal oldMax, decimal newMax) =>
        oldMax <= 0
            ? decimal.Min(current, newMax)
            : decimal.Clamp(
                decimal.Round(
                    current / oldMax * newMax,
                    3,
                    MidpointRounding.AwayFromZero),
                0,
                newMax);
}
