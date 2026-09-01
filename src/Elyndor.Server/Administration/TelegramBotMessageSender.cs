using System.Net.Http.Json;
using Elyndor.Infrastructure.Administration;
using Elyndor.Server.Identity;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public sealed class TelegramBotMessageSender(
    HttpClient httpClient,
    IOptions<AuthenticationOptions> authenticationOptions) : ITelegramMessageSender
{
    public async Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        string token = authenticationOptions.Value.Telegram.BotToken;
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
