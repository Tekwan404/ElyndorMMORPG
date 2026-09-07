namespace Elyndor.Core.Combat;

public sealed class CharacterAbilityCooldown
{
    private CharacterAbilityCooldown()
    {
        AbilityId = null!;
    }

    public CharacterAbilityCooldown(
        Guid characterId,
        string abilityId,
        DateTimeOffset readyAtUtc)
    {
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(abilityId);
        if (readyAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Cooldown timestamps must be UTC.", nameof(readyAtUtc));

        CharacterId = characterId;
        AbilityId = abilityId;
        ReadyAtUtc = readyAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public string AbilityId { get; private set; }
    public DateTimeOffset ReadyAtUtc { get; private set; }

    public void SetReadyAt(DateTimeOffset readyAtUtc)
    {
        if (readyAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Cooldown timestamps must be UTC.", nameof(readyAtUtc));
        ReadyAtUtc = readyAtUtc;
    }
}
