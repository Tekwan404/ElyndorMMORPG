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
        /help
        /status
        /health
        /resources
        /char <telegramId>
        /level <telegramId> <1-60>
        /restore <telegramId>
        /location <telegramId> <locationId>
        /rename <telegramId> <new name>
        /class <telegramId> WARRIOR|ARCHER|MAGE
        /race <telegramId> <raceId>
        /giveitem <telegramId> <itemId> [quantity] [NORMAL|ELITE|BOSS]
        /promocode create <CODE> crystals=<amount> [item=<ITEM_ID>:<qty>] [global=<N>] [per=<N>] [hours=<N>]
        /delete <telegramId> <exact name> CONFIRM
        /msg <telegramId> <text>

        Команды также принимаются без слэша.
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
            || !options.IsAllowedUser(message.From.Id)
            || !IsAuthorizedChat(message, options))
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

        if (command.Type is AdminCommandType.ShowCharacter or AdminCommandType.SetLevel or AdminCommandType.Restore
            or AdminCommandType.SetLocation or AdminCommandType.Rename or AdminCommandType.SetClass
            or AdminCommandType.SetRace or AdminCommandType.Delete or AdminCommandType.Message
            or AdminCommandType.GiveItem or AdminCommandType.CreatePromoCode)
        {
            AdministrationOperation operation = new(
                Map(command.Type),
                command.TargetTelegramUserId,
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
    }

    private static bool IsAuthorizedChat(TelegramMessage message, TelegramAdminOptions options)
    {
        if (options.IsAllowedChat(message.Chat.Id))
            return true;

        // Keep existing private-chat compatibility when no group chat is configured.
        return options.ChatId == 0
            && string.Equals(message.Chat.Type, "private", StringComparison.Ordinal)
            && message.Chat.Id == message.From!.Id;
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
        AdminCommandType.GiveItem => AdministrationOperationType.GiveItem,
        AdminCommandType.CreatePromoCode => AdministrationOperationType.CreatePromoCode,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
