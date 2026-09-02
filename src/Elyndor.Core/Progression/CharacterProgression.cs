using Elyndor.Core.Characters;

namespace Elyndor.Core.Progression;

public sealed record CharacterProgressionResult(
    int PreviousLevel,
    int CurrentLevel,
    long PreviousExperience,
    long CurrentExperience,
    int XpEarned,
    int XpToNextLevel)
{
    public bool LeveledUp => CurrentLevel > PreviousLevel;
}

public static class CharacterProgression
{
    public static CharacterProgressionResult GrantExperience(
        Character character,
        int xpEarned,
        LevelProgressionDefinition progression)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentOutOfRangeException.ThrowIfNegative(xpEarned);

        int previousLevel = character.Level;
        long previousExperience = character.Experience;

        if (character.Level >= progression.MaxLevel)
        {
            character.SetExperience(0);
            return new CharacterProgressionResult(
                previousLevel,
                character.Level,
                previousExperience,
                character.Experience,
                xpEarned,
                0);
        }

        character.AddExperience(xpEarned);
        while (character.Level < progression.MaxLevel)
        {
            int required = progression.XpToNext(character.Level);
            if (required <= 0 || character.Experience < required)
                break;

            character.SetExperience(character.Experience - required);
            character.SetLevel(character.Level + 1);
        }

        if (character.Level >= progression.MaxLevel)
            character.SetExperience(0);

        return new CharacterProgressionResult(
            previousLevel,
            character.Level,
            previousExperience,
            character.Experience,
            xpEarned,
            progression.XpToNext(character.Level));
    }
}
