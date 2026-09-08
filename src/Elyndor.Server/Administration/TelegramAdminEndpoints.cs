using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public static class TelegramAdminEndpoints
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

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
        TelegramAdminUpdateProcessor processor,
        CancellationToken cancellationToken)
    {
        TelegramAdminOptions options = configuredOptions.Value;
        if (!options.Enabled || !options.IsConfigured || options.UseLongPolling)
            return Results.NotFound();

        string receivedSecret = httpContext.Request.Headers[SecretHeader].ToString();
        if (!SecretsMatch(options.WebhookSecret, receivedSecret))
            return Results.Unauthorized();

        await processor.ProcessAsync(update, cancellationToken);
        return Results.Ok();
    }

    internal static bool SecretsMatch(string expected, string actual)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
