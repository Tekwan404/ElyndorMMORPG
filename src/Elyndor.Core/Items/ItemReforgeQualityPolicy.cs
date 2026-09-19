using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Items;

/// <summary>
/// Reforge may adapt one affix, but it is not a second item-generation path.
/// The replacement keeps roughly the same normalized roll quality as the affix it replaces
/// and is never allowed to become a perfect/max roll through reforging.
/// </summary>
public static class ItemReforgeQualityPolicy
{
    public const decimal MaximumQualityDrift = 0.10m;
    public const decimal MaximumReforgeQuality = 0.99m;

    public static GeneratedItemAffix Constrain(
        GeneratedItemAffix previous,
        GeneratedItemAffix candidate,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(random);

        decimal previousQuality = Normalize(
            previous.Value,
            previous.MinAtGeneration,
            previous.MaxAtGeneration);
        decimal drift = ((random.NextUnit() * 2m) - 1m) * MaximumQualityDrift;
        decimal targetQuality = decimal.Clamp(
            previousQuality + drift,
            0m,
            MaximumReforgeQuality);

        decimal value = RollAtQuality(
            candidate.MinAtGeneration,
            candidate.MaxAtGeneration,
            candidate.StepAtGeneration,
            targetQuality);

        // Reforge can never manufacture an exact maximum roll. Perfect remains a birth-only state.
        if (candidate.MaxAtGeneration > candidate.MinAtGeneration
            && value >= candidate.MaxAtGeneration)
        {
            value = decimal.Max(
                candidate.MinAtGeneration,
                candidate.MaxAtGeneration - candidate.StepAtGeneration);
        }

        return candidate with
        {
            Value = value,
            IsReforgeSlot = true
        };
    }

    public static decimal Normalize(decimal value, decimal minimum, decimal maximum)
    {
        if (maximum <= minimum)
            return 0m;
        return decimal.Clamp((value - minimum) / (maximum - minimum), 0m, 1m);
    }

    private static decimal RollAtQuality(
        decimal minimum,
        decimal maximum,
        decimal step,
        decimal quality)
    {
        if (maximum <= minimum)
            return minimum;
        if (step <= 0m)
            throw new InvalidOperationException("Reforge affix step must be positive.");

        decimal target = minimum + ((maximum - minimum) * quality);
        decimal stepped = decimal.Floor(target / step) * step;
        return decimal.Clamp(stepped, minimum, maximum);
    }
}
