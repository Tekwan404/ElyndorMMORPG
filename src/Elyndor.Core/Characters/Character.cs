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
        Experience = 0;
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

    public long Experience { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Rename(string name, string normalizedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        Name = name;
        NormalizedName = normalizedName;
    }

    public void SetLevel(int level)
    {
        if (level is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        Level = level;
    }

    public void SetExperience(long experience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        Experience = experience;
    }

    public void AddExperience(long experience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        Experience = checked(Experience + experience);
    }

    public void ChangeClass(string classId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        ClassId = classId;
    }

    public void ChangeRace(string raceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raceId);
        RaceId = raceId;
    }
}
