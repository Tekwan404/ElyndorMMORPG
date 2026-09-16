using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(7);
    private readonly ConcurrentDictionary<long, long> migratedChatIds = new();

    public async Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        long resolvedChatId = ResolveChatId(chatId);
        TelegramSendFailure? failure = await TrySendMessageAsync(resolvedChatId, text, cancellationToken);
        if (failure is null)
            return;

        if (failure.MigrateToChatId is long migratedChatId && migratedChatId != 0 && migratedChatId != resolvedChatId)
        {
            migratedChatIds[chatId] = migratedChatId;
            migratedChatIds[resolvedChatId] = migratedChatId;
            TelegramSendFailure? retryFailure = await TrySendMessageAsync(migratedChatId, text, cancellationToken);
            if (retryFailure is null)
                return;

            throw CreateSendException(migratedChatId, retryFailure);
        }

        throw CreateSendException(resolvedChatId, failure);
    }

    public async Task SendDocumentAsync(
        long chatId,
        string fileName,
        string content,
        string? caption,
        CancellationToken cancellationToken)
    {
        long resolvedChatId = ResolveChatId(chatId);
        TelegramSendFailure? failure = await TrySendDocumentAsync(
            resolvedChatId,
            fileName,
            content,
            caption,
            cancellationToken);
        if (failure is null)
            return;

        if (failure.MigrateToChatId is long migratedChatId && migratedChatId != 0 && migratedChatId != resolvedChatId)
        {
            migratedChatIds[chatId] = migratedChatId;
            migratedChatIds[resolvedChatId] = migratedChatId;
            TelegramSendFailure? retryFailure = await TrySendDocumentAsync(
                migratedChatId,
                fileName,
                content,
                caption,
                cancellationToken);
            if (retryFailure is null)
                return;

            throw CreateSendException(migratedChatId, retryFailure);
        }

        throw CreateSendException(resolvedChatId, failure);
    }

    private long ResolveChatId(long chatId) =>
        migratedChatIds.TryGetValue(chatId, out long migratedChatId) ? migratedChatId : chatId;

    private async Task<TelegramSendFailure?> TrySendMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        string token = authenticationOptions.Value.Telegram.BotToken;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);

        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text },
            timeout.Token);
        return await ReadFailureAsync(response, timeout.Token);
    }

    private async Task<TelegramSendFailure?> TrySendDocumentAsync(
        long chatId,
        string fileName,
        string content,
        string? caption,
        CancellationToken cancellationToken)
    {
        string token = authenticationOptions.Value.Telegram.BotToken;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);

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
            timeout.Token);
        return await ReadFailureAsync(response, timeout.Token);
    }

    private static async Task<TelegramSendFailure?> ReadFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return null;

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            TelegramApiResponse? telegram = JsonSerializer.Deserialize<TelegramApiResponse>(responseBody);
            return new TelegramSendFailure(
                (int)response.StatusCode,
                telegram?.Description ?? response.ReasonPhrase ?? "Telegram API request failed.",
                telegram?.Parameters?.MigrateToChatId);
        }
        catch (JsonException)
        {
            return new TelegramSendFailure(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "Telegram API request failed.",
                null);
        }
    }

    private static HttpRequestException CreateSendException(long chatId, TelegramSendFailure failure) =>
        new($"Telegram API send failed for chat {chatId}: HTTP {failure.StatusCode}: {failure.Description}");

    private sealed record TelegramSendFailure(int StatusCode, string Description, long? MigrateToChatId);

    private sealed record TelegramApiResponse(
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("parameters")] TelegramApiResponseParameters? Parameters);

    private sealed record TelegramApiResponseParameters(
        [property: JsonPropertyName("migrate_to_chat_id")] long? MigrateToChatId);
}
