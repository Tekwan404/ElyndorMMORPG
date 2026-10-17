namespace Elyndor.Core.World;

public static class OpenWorldPvpRules
{
    public const string LocationId = "DEAD_REACHES";
    public const int MinimumLevel = 60;
    public const int MaximumVisiblePlayers = 50;
    public static readonly TimeSpan PresenceLifetime = TimeSpan.FromSeconds(45);

    public static bool CanParticipate(string locationId, int level) =>
        locationId == LocationId && level >= MinimumLevel;
}
