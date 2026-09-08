using Elyndor.Core.Characters;
using Elyndor.Core.Identity;

namespace Elyndor.UnitTests.Social;

public sealed class PlayerIdentityTests
{
    [Fact]
    public void CharacterReceivesStablePublicCodeFromItsId()
    {
        Guid id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        Character character = new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Arthas",
            "ARTHAS",
            "HUMAN",
            "MALE",
            "WARRIOR",
            DateTimeOffset.UtcNow);

        Assert.Equal("ELY-6789ABCDEF", character.PublicCode);
    }

    [Fact]
    public void CharactersCreatedInTheSameMillisecondReceiveDifferentPublicCodes()
    {
        DateTimeOffset timestamp = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        Character first = CreateCharacter(Guid.CreateVersion7(timestamp));
        Character second = CreateCharacter(Guid.CreateVersion7(timestamp));

        Assert.NotEqual(first.PublicCode, second.PublicCode);
    }

    private static Character CreateCharacter(Guid id) => new(
        id,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Arthas",
        "ARTHAS",
        "HUMAN",
        "MALE",
        "WARRIOR",
        DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("Arthas", "arthas")]
    [InlineData("@Mage_One", "mage_one")]
    public void TelegramUsernameIsNormalizedForSearch(string value, string expected)
    {
        Assert.Equal(expected, TelegramUsernamePolicy.Normalize(value));
    }

    [Fact]
    public void AccountCanUpdateTelegramUsernameWithoutChangingItsTelegramId()
    {
        Account account = new(
            Guid.NewGuid(),
            42,
            DateTimeOffset.UtcNow);

        account.SetTelegramUsername("@Arthas");

        Assert.Equal(42, account.TelegramUserId);
        Assert.Equal("arthas", account.NormalizedTelegramUsername);
        Assert.Equal("@Arthas", account.TelegramUsername);
    }
}
