namespace Elyndor.Core.World;

public sealed class WorldMap
{
    private readonly Dictionary<string, LocationDefinition> _locations;

    public IReadOnlyCollection<LocationDefinition> Locations => _locations.Values;

    public WorldMap(IEnumerable<LocationDefinition> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);

        LocationDefinition[] configuredLocations = locations.ToArray();
        string[] locationIds = configuredLocations
            .Select(location => location.Id)
            .ToArray();

        _locations = configuredLocations.ToDictionary(
            location => location.Id,
            location => location with
            {
                Transitions = locationIds
                    .Where(targetId => !string.Equals(targetId, location.Id, StringComparison.Ordinal))
                    .ToArray(),
                TravelDurationSeconds = 0
            },
            StringComparer.Ordinal);
    }

    public LocationDefinition GetRequired(string locationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        return _locations.TryGetValue(locationId, out LocationDefinition? location)
            ? location
            : throw new KeyNotFoundException($"Location '{locationId}' does not exist.");
    }

    public bool CanTravel(string sourceId, string targetId)
    {
        if (!_locations.TryGetValue(sourceId, out LocationDefinition? source)
            || !_locations.ContainsKey(targetId))
        {
            return false;
        }

        return source.Transitions.Contains(targetId, StringComparer.Ordinal);
    }
}
