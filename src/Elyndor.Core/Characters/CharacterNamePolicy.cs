using System.Globalization;
using System.Text;

namespace Elyndor.Core.Characters;

public sealed record CharacterNameValidationResult(
    bool IsValid,
    string? DisplayName,
    string? NormalizedName,
    string? ErrorCode)
{
    public static CharacterNameValidationResult Success(
        string displayName,
        string normalizedName) =>
        new(true, displayName, normalizedName, null);

    public static CharacterNameValidationResult Failure(string errorCode) =>
        new(false, null, null, errorCode);
}

public static class CharacterNameErrorCodes
{
    public const string Required = "character_name_required";
    public const string InvalidDisplayForm = "character_name_invalid_display_form";
    public const string InvalidLength = "character_name_invalid_length";
    public const string InvalidSeparator = "character_name_invalid_separator";
    public const string InvalidCharacter = "character_name_invalid_character";
    public const string InvalidScript = "character_name_invalid_script";
    public const string MixedScripts = "character_name_mixed_scripts";
}

public static class CharacterNamePolicy
{
    private const int MinimumLength = 3;
    private const int MaximumLength = 16;

    public static CharacterNameValidationResult Validate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return CharacterNameValidationResult.Failure(CharacterNameErrorCodes.Required);
        }

        string displayName = value.Normalize(NormalizationForm.FormKC);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || !string.Equals(value, displayName, StringComparison.Ordinal))
        {
            return CharacterNameValidationResult.Failure(
                CharacterNameErrorCodes.InvalidDisplayForm);
        }

        Rune[] runes = displayName.EnumerateRunes().ToArray();
        if (runes.Length is < MinimumLength or > MaximumLength)
        {
            return CharacterNameValidationResult.Failure(
                CharacterNameErrorCodes.InvalidLength);
        }

        CharacterScript? selectedScript = null;
        bool previousWasSeparator = false;

        for (var index = 0; index < runes.Length; index++)
        {
            Rune rune = runes[index];
            bool isSeparator = rune.Value is ' ' or '-';
            if (isSeparator)
            {
                if (index == 0 || index == runes.Length - 1 || previousWasSeparator)
                {
                    return CharacterNameValidationResult.Failure(
                        CharacterNameErrorCodes.InvalidSeparator);
                }

                previousWasSeparator = true;
                continue;
            }

            previousWasSeparator = false;
            if (!IsLetter(rune))
            {
                return CharacterNameValidationResult.Failure(
                    CharacterNameErrorCodes.InvalidCharacter);
            }

            CharacterScript? runeScript = GetScript(rune);
            if (runeScript is null)
            {
                return CharacterNameValidationResult.Failure(
                    CharacterNameErrorCodes.InvalidScript);
            }

            if (selectedScript is not null && selectedScript != runeScript)
            {
                return CharacterNameValidationResult.Failure(
                    CharacterNameErrorCodes.MixedScripts);
            }

            selectedScript = runeScript;
        }

        return CharacterNameValidationResult.Success(
            displayName,
            displayName.ToUpperInvariant());
    }

    private static bool IsLetter(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter;

    private static CharacterScript? GetScript(Rune rune)
    {
        int value = rune.Value;
        if (value is (>= 0x0041 and <= 0x024F)
            or (>= 0x1D00 and <= 0x1DBF)
            or (>= 0x1E00 and <= 0x1EFF)
            or (>= 0x2C60 and <= 0x2C7F)
            or (>= 0xA720 and <= 0xA7FF)
            or (>= 0xAB30 and <= 0xAB6F)
            or (>= 0x10780 and <= 0x107BF)
            or (>= 0x1DF00 and <= 0x1DFFF))
        {
            return CharacterScript.Latin;
        }

        if (value is (>= 0x0400 and <= 0x052F)
            or (>= 0x1C80 and <= 0x1C8F)
            or (>= 0xA640 and <= 0xA69F))
        {
            return CharacterScript.Cyrillic;
        }

        return null;
    }

    private enum CharacterScript
    {
        Latin,
        Cyrillic
    }
}
