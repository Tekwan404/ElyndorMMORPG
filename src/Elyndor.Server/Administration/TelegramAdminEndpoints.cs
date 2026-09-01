using System.Security.Cryptography;
using System.Text;
using Elyndor.Infrastructure.Administration;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public static class TelegramAdminEndpoints
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    private const string HelpText = """
        Elyndor admin commands:
        /char <telegramId>
        /level <telegramId> <1-60>
        /restore <telegramId>
        /location <telegramId> <locationId>
        /rename <telegramId> <new name>
        /class <telegramId> WARRIOR|ARCHER|MAGE
        /race <telegramId> <raceId>
        /delete <telegramId> <exact name> CONFIRM
        /msg <telegramId> <text>
        """;

    public static IEndpointRouteBuilder MapTelegramAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/administration/telegram/webhook",
                HandleAsync)
            .AllowAnonymous()
            .WithName("TelegramAdminWebhook")
            .WithTags("Administration");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        TelegramUpdate update,
        IOptions<TelegramAdminOptions> configuredOptions,
        TelegramAdministrationService administrationService,
        ITelegramMessageSender messageSender,
        CancellationToken cancellationToken)
    {
        TelegramAdminOptions options = configuredOptions.Value;
        if (!options.Enabled || !options.IsConfigured)
        {
            return Results.NotFound();
        }

        string receivedSecret = httpContext.Request.Headers[SecretHeader].ToString();
        if (!SecretsMatch(options.WebhookSecret, receivedSecret))
        {
            return Results.Unauthorized();
        }

        TelegramMessage? message = update.Message;
        if (message?.From is null
            || !string.Equals(message.Chat.Type, "private", StringComparison.Ordinal)
            || message.Chat.Id != message.From.Id
            || !options.AllowedUserIds.Contains(message.From.Id))
        {
            return Results.Ok();
        }

        AdminCommandParseResult parsed = TelegramAdminCommandParser.Parse(message.Text);
        if (!parsed.IsSuccess)
        {
            await messageSender.SendAsync(
                message.Chat.Id,
                $"Ошибка: {parsed.ErrorCode}\n\n{HelpText}",
                cancellationToken);
            return Results.Ok();
        }

        AdminCommand command = parsed.Command!;
        if (command.Type == AdminCommandType.Help)
        {
            await messageSender.SendAsync(message.Chat.Id, HelpText, cancellationToken);
            return Results.Ok();
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
        return Results.Ok();
    }

    internal static bool SecretsMatch(string expected, string actual)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
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
