namespace Elyndor.Core.World;

public sealed class CharacterLocation
{
    private CharacterLocation()
    {
        LocationId = null!;
    }

    public CharacterLocation(
        Guid characterId,
        string locationId,
        long version,
        DateTimeOffset updatedAtUtc)
    {
        if (characterId == Guid.Empty)
        {
            throw new ArgumentException("Character ID cannot be empty.", nameof(characterId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be positive.");
        }

        if (updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Location timestamps must be UTC.", nameof(updatedAtUtc));
        }

        CharacterId = characterId;
        LocationId = locationId;
        Version = version;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid CharacterId { get; private set; }

    public string LocationId { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Relocate(string locationId, DateTimeOffset atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        if (atUtc.Offset != TimeSpan.Zero || atUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(atUtc));
        }

        LocationId = locationId;
        Version++;
        UpdatedAtUtc = atUtc;
    }
}
