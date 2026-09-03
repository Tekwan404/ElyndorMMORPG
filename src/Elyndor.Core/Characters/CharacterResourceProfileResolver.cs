using Elyndor.Core.Content;

namespace Elyndor.Core.Characters;

public static class CharacterResourceProfileResolver
{
    private const string ManaResourceId = "MANA";

    public static ResourceProfile Resolve(
        ResourceProfile profile,
        ResourceScalingProfile? scaling,
        CharacterStats stats,
        decimal maxResourceFlat = 0)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(stats);

        decimal maxValue = profile.MaxValue;
        if (string.Equals(profile.Id, ManaResourceId, StringComparison.Ordinal)
            && scaling is not null)
        {
            if (scaling.ManaBase < 0 || scaling.ManaPerIntellect < 0)
            {
                throw new InvalidOperationException(
                    "Mana resource scaling values cannot be negative.");
            }

            maxValue = scaling.ManaBase + (stats.Intellect * scaling.ManaPerIntellect);
        }

        maxValue = Math.Max(0, maxValue + maxResourceFlat);
        bool startsFull = profile.StartValue == profile.MaxValue;
        bool respawnsFull = profile.RespawnValue == profile.MaxValue;

        return profile with
        {
            MaxValue = maxValue,
            StartValue = startsFull
                ? maxValue
                : decimal.Clamp(profile.StartValue, 0, maxValue),
            RespawnValue = respawnsFull
                ? maxValue
                : decimal.Clamp(profile.RespawnValue, 0, maxValue)
        };
    }
}
