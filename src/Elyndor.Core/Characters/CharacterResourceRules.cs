using Elyndor.Core.Content;

namespace Elyndor.Core.Characters;

public static class CharacterResourceRules
{
    public static decimal Spend(ResourceProfile profile, decimal current, decimal amount) =>
        Clamp(profile, current - Math.Max(0, amount));

    public static decimal Restore(ResourceProfile profile, decimal current, decimal amount) =>
        Clamp(profile, current + Math.Max(0, amount));

    public static decimal Respawn(ResourceProfile profile) => Clamp(profile, profile.RespawnValue);

    public static decimal ApplyElapsed(
        ResourceProfile profile,
        decimal current,
        TimeSpan elapsed,
        bool isInCombat,
        TimeSpan elapsedSinceContextStart)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return Clamp(profile, current);
        }

        decimal elapsedSeconds = (decimal)elapsed.TotalSeconds;
        if (isInCombat)
        {
            return Restore(profile, current, elapsedSeconds * profile.CombatRegenPerSecond);
        }

        if (profile.OutOfCombatDecayPerSecond > 0)
        {
            decimal contextEnd = (decimal)elapsedSinceContextStart.TotalSeconds;
            decimal contextStart = Math.Max(0, contextEnd - elapsedSeconds);
            decimal decayStart = Math.Max(contextStart, profile.OutOfCombatDelaySeconds);
            decimal decaySeconds = Math.Max(0, contextEnd - decayStart);
            return Spend(profile, current, decaySeconds * profile.OutOfCombatDecayPerSecond);
        }

        return Restore(
            profile,
            current,
            elapsedSeconds * profile.OutOfCombatRegenPerSecond);
    }

    public static decimal Clamp(ResourceProfile profile, decimal value) =>
        decimal.Clamp(value, 0, profile.MaxValue);
}
