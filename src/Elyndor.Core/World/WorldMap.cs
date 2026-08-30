namespace Elyndor.Core.World;

public sealed class WorldMap
{
    private readonly Dictionary<string, LocationDefinition> _locations;

    public IReadOnlyCollection<LocationDefinition> Locations => _locations.Values;

    public WorldMap(IEnumerable<LocationDefinition> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);

        _locations = locations.ToDictionary(
            location => location.Id,
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
