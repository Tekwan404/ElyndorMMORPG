using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Administration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.IntegrationTests.Administration;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class BuildDumpWebhookTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Secret = "builddump-webhook-test-secret-32-characters";
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/builddump 123456", 2)]
    [InlineData("/gear 123456", 0)]
    [InlineData("/talents 123456", 0)]
    public async Task AuthorizedCommandsDeliverBuildDocumentsOrShortViews(string command, int documentCount)
    {
        await SeedAsync();
        var sender = new RecordingSender();
        await using var factory = CreateFactory(sender);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", Secret);
        var response = await client.PostAsJsonAsync("/api/v1/administration/telegram/webhook",
            new TelegramUpdate(1, new TelegramMessage(1, new TelegramUser(777), new TelegramChat(777, "private"), command)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(documentCount, sender.Documents.Count);
        if (documentCount > 0)
        {
            Assert.Contains("BUILD SNAPSHOT", sender.Documents[0]);
            using var json = JsonDocument.Parse(sender.Documents[1]);
            Assert.Equal("MageTester", json.RootElement.GetProperty("Snapshot").GetProperty("Name").GetString());
            Assert.Equal(64, json.RootElement.GetProperty("BuildHash").GetString()!.Length);
        }
        else Assert.Contains("BUILD SNAPSHOT", Assert.Single(sender.Messages));
    }

    [Theory]
    [InlineData(778, 778, "private", true)]
    [InlineData(777, -42, "group", true)]
    [InlineData(777, 777, "private", false)]
    public async Task UnauthorizedUserChatOrWebhookSecretCannotDumpAnotherPlayer(long senderId, long chatId, string chatType, bool validSecret)
    {
        await SeedAsync();
        var sender = new RecordingSender();
        await using var factory = CreateFactory(sender);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", validSecret ? Secret : "wrong");
        var response = await client.PostAsJsonAsync("/api/v1/administration/telegram/webhook",
            new TelegramUpdate(2, new TelegramMessage(2, new TelegramUser(senderId), new TelegramChat(chatId, chatType), "/builddump 123456")));
        Assert.Equal(validSecret ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(sender.Documents);
        Assert.Empty(sender.Messages);
        await using var context = postgres.CreateDbContext();
        Assert.Empty(await context.CharacterBuildArchives.ToArrayAsync());
    }

    private WebApplicationFactory<Program> CreateFactory(RecordingSender sender) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
            builder.UseSetting("Authentication:SigningKey", "builddump-integration-test-key-over-32-characters");
            builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
            builder.UseSetting("Administration:Telegram:Enabled", "true");
            builder.UseSetting("Administration:Telegram:AllowedUserIds:0", "777");
            builder.UseSetting("Administration:Telegram:WebhookSecret", Secret);
            builder.UseSetting("Administration:Telegram:RegisterWebhookOnStartup", "false");
            builder.UseSetting("Administration:Telegram:UseLongPolling", "false");
            builder.ConfigureServices(services => services.AddSingleton<ITelegramMessageSender>(sender));
        });

    private async Task SeedAsync()
    {
        await using var context = postgres.CreateDbContext();
        Guid accountId = Guid.NewGuid(), characterId = Guid.NewGuid();
        context.Accounts.Add(new Account(accountId, 123456, DateTimeOffset.UtcNow));
        context.Characters.Add(new Character(characterId, accountId, Guid.NewGuid(), "MageTester", "MAGETESTER", "HUMAN", "FEMALE", "MAGE", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    private sealed class RecordingSender : ITelegramMessageSender, ITelegramDocumentSender
    {
        public List<string> Messages { get; } = [];
        public List<string> Documents { get; } = [];
        public Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            Assert.Equal(777, chatId);
            Messages.Add(text);
            return Task.CompletedTask;
        }
        public Task SendDocumentAsync(long chatId, string fileName, string content, string? caption, CancellationToken cancellationToken)
        {
            Assert.Equal(777, chatId);
            Documents.Add(content);
            return Task.CompletedTask;
        }
    }
}
