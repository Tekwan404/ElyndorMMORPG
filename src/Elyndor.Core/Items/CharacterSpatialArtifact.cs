namespace Elyndor.Core.Items;

public sealed class CharacterSpatialArtifact
{
    private CharacterSpatialArtifact()
    {
    }

    public CharacterSpatialArtifact(Guid characterId, Guid characterItemId)
    {
        if (characterId == Guid.Empty || characterItemId == Guid.Empty)
            throw new ArgumentException("Spatial artifact identifiers cannot be empty.");

        CharacterId = characterId;
        CharacterItemId = characterItemId;
    }

    public Guid CharacterId { get; private set; }
    public Guid CharacterItemId { get; private set; }

    public void Equip(Guid characterItemId)
    {
        if (characterItemId == Guid.Empty)
            throw new ArgumentException("Item identifier cannot be empty.", nameof(characterItemId));
        CharacterItemId = characterItemId;
    }
}
