using Elyndor.Core.Content;

namespace Elyndor.Core.Characters;

public sealed record ProfileCosmeticDefinition(string Id, string Name, string Description,
    string Kind, string StyleId, long CrystalPrice, bool Enabled = true);
public sealed record CharacterRenameOffer(long CrystalPrice, bool Enabled = true);

public sealed class CharacterCosmeticOwnership
{
    private CharacterCosmeticOwnership() { CosmeticId = null!; }
    public CharacterCosmeticOwnership(Guid characterId, string cosmeticId, long crystalPrice, DateTimeOffset purchasedAtUtc)
    {
        if (characterId == Guid.Empty) throw new ArgumentException("Character id is required.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(cosmeticId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(crystalPrice);
        CharacterId = characterId;
        CosmeticId = cosmeticId;
        CrystalPrice = crystalPrice;
        PurchasedAtUtc = purchasedAtUtc;
    }
    public Guid CharacterId { get; private set; }
    public string CosmeticId { get; private set; }
    public long CrystalPrice { get; private set; }
    public DateTimeOffset PurchasedAtUtc { get; private set; }
}

public static class CharacterProfilePresentation
{
    public static string? FrameStyle(Character character, GameContentPackage content) => FrameStyle(character.ActiveProfileFrameId, content);
    public static string? FrameStyle(string? id, GameContentPackage content) =>
        content.ProfileCosmetics?.FirstOrDefault(item => item.Id == id && item.Kind == "Frame")?.StyleId;
    public static string? TitleName(Character character, GameContentPackage content) => TitleName(character.ActiveTitleId, content);
    public static string? TitleName(string? id, GameContentPackage content) =>
        content.ProfileCosmetics?.FirstOrDefault(item => item.Id == id && item.Kind == "Title")?.Name;
}
