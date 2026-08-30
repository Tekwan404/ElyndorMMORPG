using Elyndor.Core.Characters;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterNamePolicyTests
{
    [Theory]
    [InlineData("Arthas", "ARTHAS")]
    [InlineData("Артас", "АРТАС")]
    [InlineData("Анна-Мария", "АННА-МАРИЯ")]
    [InlineData("Dark Wolf", "DARK WOLF")]
    public void ValidateAcceptsFormalDisplayNames(string value, string normalizedName)
    {
        CharacterNameValidationResult result = CharacterNamePolicy.Validate(value);

        Assert.True(result.IsValid);
        Assert.Equal(value, result.DisplayName);
        Assert.Equal(normalizedName, result.NormalizedName);
        Assert.Null(result.ErrorCode);
    }

    [Theory]
    [InlineData(" Arthas", CharacterNameErrorCodes.InvalidDisplayForm)]
    [InlineData("Arthas ", CharacterNameErrorCodes.InvalidDisplayForm)]
    [InlineData("Dark  Wolf", CharacterNameErrorCodes.InvalidSeparator)]
    [InlineData("Dark--Wolf", CharacterNameErrorCodes.InvalidSeparator)]
    [InlineData("Dark -Wolf", CharacterNameErrorCodes.InvalidSeparator)]
    [InlineData("-Arthas", CharacterNameErrorCodes.InvalidSeparator)]
    [InlineData("Arthas-", CharacterNameErrorCodes.InvalidSeparator)]
    [InlineData("Arth4s", CharacterNameErrorCodes.InvalidCharacter)]
    [InlineData("Arth😀s", CharacterNameErrorCodes.InvalidCharacter)]
    [InlineData("Αρης", CharacterNameErrorCodes.InvalidScript)]
    [InlineData("Aртас", CharacterNameErrorCodes.MixedScripts)]
    [InlineData("Ab", CharacterNameErrorCodes.InvalidLength)]
    [InlineData("Abcdefghijklmnopq", CharacterNameErrorCodes.InvalidLength)]
    [InlineData("Ａrthas", CharacterNameErrorCodes.InvalidDisplayForm)]
    [InlineData("Cafe\u0301", CharacterNameErrorCodes.InvalidDisplayForm)]
    public void ValidateRejectsInvalidNames(string value, string expectedErrorCode)
    {
        CharacterNameValidationResult result = CharacterNamePolicy.Validate(value);

        Assert.False(result.IsValid);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Null(result.DisplayName);
        Assert.Null(result.NormalizedName);
    }
}
