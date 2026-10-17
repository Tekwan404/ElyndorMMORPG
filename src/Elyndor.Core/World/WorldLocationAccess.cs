using Elyndor.Core.Characters;

namespace Elyndor.Core.World;

public static class WorldLocationAccess
{
    public static bool CanEnter(LocationDefinition location, string raceId) =>
        location.RequiredRaceId is null || location.RequiredRaceId == raceId;

    public static string HomeTown(string raceId) =>
        raceId == RaceIds.Undead ? WorldLocationIds.UndeadStarterTown : WorldLocationIds.StarterTown;

    public static bool IsTown(string? locationId) =>
        locationId is WorldLocationIds.StarterTown or WorldLocationIds.UndeadStarterTown;

    public static string RequiredLocation(string locationId, string raceId) =>
        locationId == WorldLocationIds.StarterTown ? HomeTown(raceId) : locationId;
}
