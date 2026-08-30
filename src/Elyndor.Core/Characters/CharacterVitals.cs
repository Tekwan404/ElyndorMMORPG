namespace Elyndor.Core.Characters;

public sealed class CharacterVitals
{
    private CharacterVitals()
    {
    }

    public CharacterVitals(
        Guid characterId,
        decimal currentHp,
        decimal currentResource,
        DateTimeOffset checkpointedAtUtc,
        DateTimeOffset contextStartedAtUtc)
    {
        if (characterId == Guid.Empty)
        {
            throw new ArgumentException("Character ID cannot be empty.", nameof(characterId));
        }

        if (currentHp < 0 || currentResource < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHp), "Vitals cannot be negative.");
        }

        if (checkpointedAtUtc.Offset != TimeSpan.Zero
            || contextStartedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Vital timestamps must be UTC.");
        }

        CharacterId = characterId;
        CurrentHp = currentHp;
        CurrentResource = currentResource;
        CheckpointedAtUtc = checkpointedAtUtc;
        ContextStartedAtUtc = contextStartedAtUtc;
    }

    public Guid CharacterId { get; private set; }

    public decimal CurrentHp { get; private set; }

    public decimal CurrentResource { get; private set; }

    public DateTimeOffset CheckpointedAtUtc { get; private set; }

    public DateTimeOffset ContextStartedAtUtc { get; private set; }

    public void Checkpoint(decimal currentHp, decimal currentResource, DateTimeOffset atUtc)
    {
        if (currentHp < 0 || currentResource < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHp), "Vitals cannot be negative.");
        }

        if (atUtc.Offset != TimeSpan.Zero || atUtc < CheckpointedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(atUtc));
        }

        CurrentHp = currentHp;
        CurrentResource = currentResource;
        CheckpointedAtUtc = atUtc;
    }
}
