namespace Elyndor.Core.Characters;

public sealed class Character
{
    private Character()
    {
        Name = null!;
        NormalizedName = null!;
        RaceId = null!;
        GenderId = null!;
        ClassId = null!;
    }

    public Character(
        Guid id,
        Guid accountId,
        Guid creationRequestId,
        string name,
        string normalizedName,
        string raceId,
        string genderId,
        string classId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || accountId == Guid.Empty || creationRequestId == Guid.Empty)
        {
            throw new ArgumentException("Character identifiers cannot be empty.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Character timestamps must be UTC.", nameof(createdAtUtc));
        }

        Id = id;
        AccountId = accountId;
        CreationRequestId = creationRequestId;
        Name = name;
        NormalizedName = normalizedName;
        RaceId = raceId;
        GenderId = genderId;
        ClassId = classId;
        Level = 1;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid CreationRequestId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string RaceId { get; private set; }

    public string GenderId { get; private set; }

    public string ClassId { get; private set; }

    public int Level { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
