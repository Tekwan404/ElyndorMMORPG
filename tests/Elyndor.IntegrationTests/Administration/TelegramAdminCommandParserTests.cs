using Elyndor.Server.Administration;

namespace Elyndor.IntegrationTests.Administration;

public sealed class TelegramAdminCommandParserTests
{
    [Fact]
    public void BuildDiffAcceptsTwoFullHashesWithoutTelegramTarget()
    {
        string first = new('a', 64), second = new('B', 64);
        var result = TelegramAdminCommandParser.Parse($"/builddiff@elyndor_bot {first} {second}");
        Assert.True(result.IsSuccess);
        Assert.Equal("BuildDiff", result.Command!.Type.ToString());
        Assert.Null(result.Command.TargetTelegramUserId);
        Assert.Equal(first.ToUpperInvariant(), result.Command.Value);
        Assert.Equal(second, result.Command.ComparisonValue);
        Assert.False(TelegramAdminCommandParser.Parse($"/builddiff {first} {second} extra").IsSuccess);
        Assert.False(TelegramAdminCommandParser.Parse($"/builddiff {new string('G', 64)} {second}").IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCD EFGH")]
    [InlineData("123")]
    public void BuildDiffRejectsMissingOrAbbreviatedHashes(string arguments)
    {
        Assert.Equal("admin_build_hash_invalid", TelegramAdminCommandParser.Parse("/builddiff " + arguments).ErrorCode);
    }

    [Theory]
    [InlineData("/builddump 123")]
    [InlineData("/gear 123")]
    [InlineData("/talents@elyndor_bot 123")]
    public void BuildDiagnosticsAcceptExactlyOnePositiveTarget(string text)
    {
        var result = TelegramAdminCommandParser.Parse(text);
        Assert.True(result.IsSuccess);
        Assert.Equal(123, result.Command!.TargetTelegramUserId);
        Assert.False(TelegramAdminCommandParser.Parse(text + " extra").IsSuccess);
    }
    [Theory]
    [InlineData("/rename 123 Aldor the-Brave", AdminCommandType.Rename, "Aldor the-Brave")]
    [InlineData("rename 123 Aldor the-Brave", AdminCommandType.Rename, "Aldor the-Brave")]
    [InlineData("/msg 123 Server restart in five minutes", AdminCommandType.Message, "Server restart in five minutes")]
    [InlineData("msg 123 Server restart in five minutes", AdminCommandType.Message, "Server restart in five minutes")]
    [InlineData("/giveitem 123 UNIQUE_WARRIOR_BLACKHEART_L25 1 BOSS", AdminCommandType.GiveItem, "UNIQUE_WARRIOR_BLACKHEART_L25 1 BOSS")]
    public void ParsePreservesTrailingText(string text, AdminCommandType type, string value)
    {
        AdminCommandParseResult result = TelegramAdminCommandParser.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.Equal(type, result.Command!.Type);
        Assert.Equal(123, result.Command.TargetTelegramUserId);
        Assert.Equal(value, result.Command.Value);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("help")]
    [InlineData("/help@elyndor_bot")]
    public void HelpAcceptsSlashPlainTextAndBotSuffix(string text)
    {
        AdminCommandParseResult result = TelegramAdminCommandParser.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminCommandType.Help, result.Command!.Type);
    }

    [Theory]
    [InlineData("/level 123 15")]
    [InlineData("level 123 15")]
    public void LevelAcceptsSlashAndPlainText(string text)
    {
        AdminCommandParseResult result = TelegramAdminCommandParser.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminCommandType.SetLevel, result.Command!.Type);
        Assert.Equal(123, result.Command.TargetTelegramUserId);
        Assert.Equal(15, result.Command.NumericValue);
    }

    [Theory]
    [InlineData("/promocode create START100 crystals=100 global=500 per=1")]
    [InlineData("promo create BLACKHEART item=UNIQUE_WARRIOR_BLACKHEART_L25:1 global=10 hours=24")]
    public void PromoCreateDoesNotRequirePlayerTarget(string text)
    {
        AdminCommandParseResult result = TelegramAdminCommandParser.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminCommandType.CreatePromoCode, result.Command!.Type);
        Assert.Null(result.Command.TargetTelegramUserId);
        Assert.NotEmpty(result.Command.Value!);
    }

    [Theory]
    [InlineData("/worldboss spawn")]
    [InlineData("worldboss spawn")]
    [InlineData("/wb spawn")]
    public void WorldBossSpawnDefaultsToAshArchon(string text)
    {
        AdminCommandParseResult result = TelegramAdminCommandParser.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminCommandType.SpawnWorldBoss, result.Command!.Type);
        Assert.Null(result.Command.TargetTelegramUserId);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON", result.Command.Value);
    }

    [Fact]
    public void WorldBossSpawnAcceptsExplicitDefinition()
    {
        AdminCommandParseResult result =
            TelegramAdminCommandParser.Parse("/worldboss spawn world_boss_ash_archon");

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminCommandType.SpawnWorldBoss, result.Command!.Type);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON", result.Command.Value);
    }

    [Fact]
    public void PromoRequiresCreateSubcommand()
    {
        AdminCommandParseResult result = TelegramAdminCommandParser.Parse("/promocode START100 crystals=100");

        Assert.False(result.IsSuccess);
        Assert.Equal("admin_promo_command_invalid", result.ErrorCode);
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
