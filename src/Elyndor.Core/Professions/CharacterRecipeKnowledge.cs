namespace Elyndor.Core.Professions;

public sealed class CharacterRecipeKnowledge
{
    private CharacterRecipeKnowledge() { ProfessionId = null!; RecipeId = null!; }

    public CharacterRecipeKnowledge(Guid characterId, string professionId, string recipeId, DateTimeOffset learnedAtUtc)
    {
        if (characterId == Guid.Empty) throw new ArgumentException("Character identifier cannot be empty.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(professionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        if (learnedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(learnedAtUtc));
        CharacterId = characterId;
        ProfessionId = professionId;
        RecipeId = recipeId;
        LearnedAtUtc = learnedAtUtc;
    }
    public Guid CharacterId { get; private set; }
    public string ProfessionId { get; private set; }
    public string RecipeId { get; private set; }
    public DateTimeOffset LearnedAtUtc { get; private set; }
}
