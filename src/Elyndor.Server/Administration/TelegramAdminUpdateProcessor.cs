using Elyndor.Infrastructure.Administration;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public sealed class TelegramAdminUpdateProcessor(
    IOptions<TelegramAdminOptions> configuredOptions,
    TelegramAdministrationService administrationService,
    ITelegramMessageSender messageSender)
{
    internal const string HelpText = """
        Elyndor admin commands:
        help
        char <telegramId>
        level <telegramId> <1-60>
        restore <telegramId>
        location <telegramId> <locationId>
        rename <telegramId> <new name>
        class <telegramId> WARRIOR|ARCHER|MAGE
        race <telegramId> <raceId>
        delete <telegramId> <exact name> CONFIRM
        msg <telegramId> <text>

        Команды также принимаются со слэшем: /help, /char, /level и т.д.
        """;

    public async Task ProcessAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        TelegramAdminOptions options = configuredOptions.Value;
        if (!options.Enabled || !options.IsConfigured)
            return;

        TelegramMessage? message = update.Message;
        if (message?.From is null
            || !string.Equals(message.Chat.Type, "private", StringComparison.Ordinal)
            || message.Chat.Id != message.From.Id
            || !options.IsAllowedUser(message.From.Id))
        {
            return;
        }

        AdminCommandParseResult parsed = TelegramAdminCommandParser.Parse(message.Text);
        if (!parsed.IsSuccess)
        {
            await messageSender.SendAsync(
                message.Chat.Id,
                $"Ошибка: {parsed.ErrorCode}\n\n{HelpText}",
                cancellationToken);
            return;
        }

        AdminCommand command = parsed.Command!;
        if (command.Type == AdminCommandType.Help)
        {
            await messageSender.SendAsync(message.Chat.Id, HelpText, cancellationToken);
            return;
        }

        AdministrationOperation operation = new(
            Map(command.Type),
            command.TargetTelegramUserId!.Value,
            command.Value,
            command.NumericValue);
        AdministrationResult result = await administrationService.ExecuteAsync(
            update.UpdateId,
            message.From.Id,
            operation,
            cancellationToken);
        string prefix = result.IsSuccess ? "✅" : "⚠️";
        await messageSender.SendAsync(
            message.Chat.Id,
            $"{prefix} {result.Message}\nКод: {result.Code}",
            cancellationToken);
    }

    private static AdministrationOperationType Map(AdminCommandType type) => type switch
    {
        AdminCommandType.ShowCharacter => AdministrationOperationType.ShowCharacter,
        AdminCommandType.SetLevel => AdministrationOperationType.SetLevel,
        AdminCommandType.Restore => AdministrationOperationType.Restore,
        AdminCommandType.SetLocation => AdministrationOperationType.SetLocation,
        AdminCommandType.Rename => AdministrationOperationType.Rename,
        AdminCommandType.SetClass => AdministrationOperationType.SetClass,
        AdminCommandType.SetRace => AdministrationOperationType.SetRace,
        AdminCommandType.Delete => AdministrationOperationType.Delete,
        AdminCommandType.Message => AdministrationOperationType.Message,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
