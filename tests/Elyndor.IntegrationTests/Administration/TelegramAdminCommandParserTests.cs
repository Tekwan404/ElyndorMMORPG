using Elyndor.Server.Administration;

namespace Elyndor.IntegrationTests.Administration;

public sealed class TelegramAdminCommandParserTests
{
    [Theory]
    [InlineData("/rename 123 Aldor the-Brave", AdminCommandType.Rename, "Aldor the-Brave")]
    [InlineData("/msg 123 Server restart in five minutes", AdminCommandType.Message, "Server restart in five minutes")]
    public void ParsePreservesTrailingText(string text, AdminCommandType type, string value)
    {
        AdminCommandParseResult result = TelegramAdminCommandParser.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.Equal(type, result.Command!.Type);
        Assert.Equal(123, result.Command.TargetTelegramUserId);
        Assert.Equal(value, result.Command.Value);
    }

    [Fact]
    public void DeleteRequiresExactNameAndConfirmation()
    {
        Assert.Equal(
            "admin_delete_confirmation_required",
            TelegramAdminCommandParser.Parse("/delete 123 Aldor").ErrorCode);

        AdminCommandParseResult result =
            TelegramAdminCommandParser.Parse("/delete 123 Aldor the-Brave CONFIRM");

        Assert.True(result.IsSuccess);
        Assert.Equal("Aldor the-Brave", result.Command!.Value);
    }
}
