using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Elyndor.Infrastructure.Administration;
using Elyndor.Server.Identity;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public interface ITelegramDocumentSender
{
    Task SendDocumentAsync(
        long chatId,
        string fileName,
        string content,
        string? caption,
        CancellationToken cancellationToken);
}

public sealed class TelegramBotMessageSender(
    HttpClient httpClient,
    IOptions<AuthenticationOptions> authenticationOptions) : ITelegramMessageSender, ITelegramDocumentSender
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

    public async Task SendDocumentAsync(
        long chatId,
        string fileName,
        string content,
        string? caption,
        CancellationToken cancellationToken)
    {
        string token = authenticationOptions.Value.Telegram.BotToken;
        using MultipartFormDataContent form = new();
        form.Add(new StringContent(chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id");
        if (!string.IsNullOrWhiteSpace(caption))
            form.Add(new StringContent(caption, Encoding.UTF8), "caption");

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        using ByteArrayContent document = new(bytes);
        document.Headers.ContentType = new MediaTypeHeaderValue("text/plain")
        {
            CharSet = "utf-8"
        };
        form.Add(document, "document", fileName);

        using HttpResponseMessage response = await httpClient.PostAsync(
            $"https://api.telegram.org/bot{token}/sendDocument",
            form,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
