using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Arena;
using Elyndor.Contracts.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Administration;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.Pvp;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ArenaInvitationFlowTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly TestTime _time = new();
    private readonly TelegramRecorder _telegram = new();
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InviteReplayNotifiesOnceConcurrentAcceptStartsOneUnrankedMatchAndNoRewards()
    {
        await using var factory = Factory();
        var (first, firstAccount) = await Player(factory, "Inviter", 8801);
        var (second, secondAccount) = await Player(factory, "Target", 8802);
        using (first)
        using (second)
        {
            Presence(factory, firstAccount, secondAccount);
            Guid requestId = Guid.NewGuid();
            var request = new ArenaInviteRequest(requestId, "Target");
            (await first.PostAsJsonAsync("/api/v1/arena/invites", request)).EnsureSuccessStatusCode();
            (await first.PostAsJsonAsync("/api/v1/arena/invites", request)).EnsureSuccessStatusCode();
            Assert.Equal(1, _telegram.Sends);
            Assert.Equal(8802, _telegram.ChatId);
            Assert.Contains("Inviter", _telegram.Text);
            Assert.EndsWith($"?arenaInvite={requestId:D}", _telegram.Url);
            Assert.Single((await second.GetFromJsonAsync<ArenaInvitationResponse[]>("/api/v1/arena/invites"))!);
            var replies = await Task.WhenAll(
                second.PostAsJsonAsync($"/api/v1/arena/invites/{requestId}", new ArenaInviteActionRequest("accept")),
                second.PostAsJsonAsync($"/api/v1/arena/invites/{requestId}", new ArenaInviteActionRequest("accept")));
            foreach (var reply in replies) reply.EnsureSuccessStatusCode();
            var firstReply = await replies[0].Content.ReadFromJsonAsync<ArenaInvitationResponse>();
            var secondReply = await replies[1].Content.ReadFromJsonAsync<ArenaInvitationResponse>();
            Assert.Equal(firstReply!.MatchId, secondReply!.MatchId);
            using var scope = factory.Services.CreateScope();
            var runtime = scope.ServiceProvider.GetRequiredService<ArenaMatchRuntime>();
            Assert.Equal(firstReply.MatchId, runtime.ActiveMatchId(firstAccount));
            Assert.NotNull(runtime.GetState(secondAccount, firstReply.MatchId!.Value, 0));
            runtime.Surrender(firstAccount, firstReply.MatchId.Value);
            await scope.ServiceProvider.GetRequiredService<ArenaSettlementService>().CompleteAndSettleAsync(
                firstReply.MatchId.Value, ArenaMatchOutcome.WinnerB, null, default);
            runtime.MarkFinalized(firstReply.MatchId.Value);
            // Replay after completion cannot resurrect the session.
            (await second.PostAsJsonAsync($"/api/v1/arena/invites/{requestId}", new ArenaInviteActionRequest("accept")))
                .EnsureSuccessStatusCode();
            Assert.Null(runtime.ActiveMatchId(firstAccount));
            await using var db = postgres.CreateDbContext();
            Assert.Single(await db.ArenaMatches.ToArrayAsync());
            Assert.Equal(ArenaQueueMode.Unranked, (await db.ArenaMatches.SingleAsync()).Mode);
            Assert.Empty(await db.ArenaHonorLedgerEntries.ToArrayAsync());
            Assert.All(await db.ArenaStandings.ToArrayAsync(), standing => Assert.Equal(0, standing.Wins + standing.Losses));
        }
    }

    [Fact]
    public async Task AuthorizationExpirationAndOfflineAreServerValidated()
    {
        await using var factory = Factory();
        var (first, _) = await Player(factory, "Inviter", 8801);
        var (second, _) = await Player(factory, "Target", 8802);
        var (outsider, _) = await Player(factory, "Outsider", 8803);
        using (first)
        using (second)
        using (outsider)
        {
            Guid id = Guid.NewGuid();
            (await first.PostAsJsonAsync("/api/v1/arena/invites", new ArenaInviteRequest(id, "Target"))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync(
                $"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest("accept"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await first.PostAsJsonAsync(
                $"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest("accept"))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync(
                $"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest("accept"))).StatusCode);
            _time.Now = _time.Now.AddMinutes(5);
            Assert.Empty((await second.GetFromJsonAsync<ArenaInvitationResponse[]>("/api/v1/arena/invites"))!);
            Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync(
                $"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest("accept"))).StatusCode);
            await using var db = postgres.CreateDbContext();
            Assert.Empty(await db.ArenaMatches.ToArrayAsync());
        }
    }

    [Theory]
    [InlineData("decline")]
    [InlineData("cancel")]
    public async Task ClosedInviteCannotStartAndTelegramFailureDoesNotRollback(string action)
    {
        _telegram.Fail = true;
        await using var factory = Factory();
        var (first, _) = await Player(factory, "Inviter", 8801);
        var (second, _) = await Player(factory, "Target", 8802);
        using (first)
        using (second)
        {
            Guid id = Guid.NewGuid();
            (await first.PostAsJsonAsync("/api/v1/arena/invites", new ArenaInviteRequest(id, "Target"))).EnsureSuccessStatusCode();
            var actor = action == "cancel" ? first : second;
            (await actor.PostAsJsonAsync($"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest(action)))
                .EnsureSuccessStatusCode();
            (await actor.PostAsJsonAsync($"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest(action)))
                .EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync(
                $"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest("accept"))).StatusCode);
            await using var db = postgres.CreateDbContext();
            Assert.Empty(await db.ArenaMatches.ToArrayAsync());
        }
    }

    [Fact]
    public async Task CannotInviteSelfOrMissingPlayerOrAcceptWhileQueued()
    {
        await using var factory = Factory();
        var (first, firstAccount) = await Player(factory, "Inviter", 8801);
        var (second, secondAccount) = await Player(factory, "Target", 8802);
        using (first)
        using (second)
        {
            Assert.Equal(HttpStatusCode.Conflict, (await first.PostAsJsonAsync("/api/v1/arena/invites",
                new ArenaInviteRequest(Guid.NewGuid(), "Inviter"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await first.PostAsJsonAsync("/api/v1/arena/invites",
                new ArenaInviteRequest(Guid.NewGuid(), "Missing"))).StatusCode);
            Guid id = Guid.NewGuid();
            (await first.PostAsJsonAsync("/api/v1/arena/invites", new ArenaInviteRequest(id, "Target"))).EnsureSuccessStatusCode();
            Presence(factory, firstAccount, secondAccount);
            using var scope = factory.Services.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<ArenaQueueService>();
            Assert.True((await queue.JoinAsync(secondAccount, ArenaQueueMode.Ranked, default)).Succeeded);
            Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync(
                $"/api/v1/arena/invites/{id}", new ArenaInviteActionRequest("accept"))).StatusCode);
            await using var db = postgres.CreateDbContext();
            Assert.Empty(await db.ArenaMatches.ToArrayAsync());
        }
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
        builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
        builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
        builder.UseSetting("Authentication:SigningKey", "arena-invite-test-signing-key-with-more-than-32-bytes");
        builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
        builder.UseSetting("Authentication:Development:Enabled", "false");
        builder.UseSetting("Arena:Enabled", "true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_time);
            services.RemoveAll<ITelegramMessageSender>();
            services.AddSingleton<ITelegramMessageSender>(_telegram);
        });
    });

    private async Task<(HttpClient Client, Guid Account)> Player(WebApplicationFactory<Program> factory, string name, long telegramId)
    {
        Guid id = Guid.NewGuid();
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            db.Accounts.Add(new Account(id, telegramId, _time.Now));
            await db.SaveChangesAsync();
        }
        var token = factory.Services.GetRequiredService<JwtTokenIssuer>().Issue(id, telegramId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        (await client.PostAsJsonAsync("/api/v1/character",
            new CreateCharacterRequest(Guid.NewGuid(), name, "HUMAN", "MALE", "WARRIOR"))).EnsureSuccessStatusCode();
        return (client, id);
    }

    private static void Presence(WebApplicationFactory<Program> factory, params Guid[] accounts)
    {
        var presence = factory.Services.GetRequiredService<ArenaPresenceTracker>();
        foreach (Guid account in accounts) presence.Connected(account, account.ToString());
    }

    private sealed class TestTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TelegramRecorder : ITelegramMessageSender, ITelegramWebAppMessageSender
    {
        public int Sends { get; private set; }
        public long ChatId { get; private set; }
        public string Text { get; private set; } = "";
        public string Url { get; private set; } = "";
        public bool Fail { get; set; }
        public Task SendAsync(long chatId, string text, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendWebAppAsync(long chatId, string text, string buttonText, string webAppUrl, CancellationToken cancellationToken)
        {
            Sends++;
            ChatId = chatId;
            Text = text;
            Url = webAppUrl;
            if (Fail) throw new HttpRequestException("Bot blocked by recipient");
            return Task.CompletedTask;
        }
    }
}
