using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class AutoAttackFlowTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ArcherEquippedBowDealsImmediateAndScheduledAutoAttackDamageThroughSignalR()
    {
        Guid accountId = Guid.CreateVersion7();
        const long telegramUserId = 9601;
        DateTimeOffset seededAtUtc = DateTimeOffset.UtcNow;

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            seedContext.Accounts.Add(new Account(accountId, telegramUserId, seededAtUtc));
            await seedContext.SaveChangesAsync();
        }

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateAuthenticatedClient(
            factory,
            accountId,
            telegramUserId);
        CharacterResponse character = await CreateCharacterAsync(
            client,
            "AutoArcher",
            "ARCHER");

        Guid bowItemId = Guid.CreateVersion7();
        await using (GameDbContext equipmentContext = postgres.CreateDbContext())
        {
            equipmentContext.CharacterItems.Add(new CharacterItem(
                bowItemId,
                character.Id,
                "HUNTER_SHORTBOW",
                1,
                seededAtUtc));
            equipmentContext.CharacterEquipment.Add(new CharacterEquipment(
                character.Id,
                EquipmentSlot.MainHand,
                bowItemId));
            await equipmentContext.SaveChangesAsync();
        }

        IssuedAccessToken token = IssueToken(factory, accountId, telegramUserId);
        await using HubConnection hub = CreateHubConnection(factory, token);
        await hub.StartAsync();

        CombatUpdateResponse started = await hub.InvokeAsync<CombatUpdateResponse>(
            "StartTraining");
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.NotNull(started.Snapshot);
        Guid sessionId = started.Snapshot!.SessionId;

        CombatUpdateResponse stopped = await hub.InvokeAsync<CombatUpdateResponse>(
            "StopAutoAttack",
            sessionId,
            "archer-auto-stop");
        Assert.True(stopped.Succeeded, stopped.ErrorCode);
        Assert.False(stopped.Snapshot?.Player.AutoAttackEnabled);

        CombatUpdateResponse restarted = await hub.InvokeAsync<CombatUpdateResponse>(
            "StartAutoAttack",
            sessionId,
            "archer-auto-start");
        Assert.True(restarted.Succeeded, restarted.ErrorCode);
        Assert.True(restarted.Snapshot?.Player.AutoAttackEnabled);
        Assert.Contains(
            restarted.Events,
            combatEvent => combatEvent.Type == "DamageDealt"
                && combatEvent.DefinitionId == "AUTO_ATTACK"
                && combatEvent.SourceActorId == character.Id
                && combatEvent.Amount > 0);

        long restartSequence = restarted.Snapshot!.Sequence;
        TaskCompletionSource<CombatUpdateResponse> scheduledDamage =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable subscription = hub.On<CombatUpdateResponse>(
            "CombatUpdated",
            update =>
            {
                if (update.Snapshot?.Sequence > restartSequence
                    && update.Events.Any(combatEvent =>
                        combatEvent.Type == "DamageDealt"
                        && combatEvent.DefinitionId == "AUTO_ATTACK"
                        && combatEvent.SourceActorId == character.Id
                        && combatEvent.Amount > 0))
                {
                    scheduledDamage.TrySetResult(update);
                }
            });

        try
        {
            CombatUpdateResponse tick = await scheduledDamage.Task.WaitAsync(
                TimeSpan.FromSeconds(6));
            Assert.True(tick.Succeeded, tick.ErrorCode);
            Assert.True(tick.Snapshot!.Enemy.Hp < restarted.Snapshot.Enemy.Hp);
        }
        finally
        {
            subscription.Dispose();
            await hub.InvokeAsync<CombatUpdateResponse>(
                "LeaveCombat",
                "archer-auto-cleanup");
        }
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
                builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
                builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
                builder.UseSetting(
                    "Authentication:SigningKey",
                    "autoattack-flow-test-signing-key-with-more-than-32-bytes");
                builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
                builder.UseSetting("Authentication:Development:Enabled", "false");
            });

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        Guid accountId,
        long telegramUserId)
    {
        IssuedAccessToken token = IssueToken(factory, accountId, telegramUserId);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        return client;
    }

    private static async Task<CharacterResponse> CreateCharacterAsync(
        HttpClient client,
        string name,
        string classId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/character",
            new CreateCharacterRequest(
                Guid.CreateVersion7(),
                name,
                "HUMAN",
                "MALE",
                classId));
        response.EnsureSuccessStatusCode();
        CharacterResponse? character =
            await response.Content.ReadFromJsonAsync<CharacterResponse>();
        Assert.NotNull(character);
        return character;
    }

    private static IssuedAccessToken IssueToken(
        WebApplicationFactory<Program> factory,
        Guid accountId,
        long telegramUserId) =>
        factory.Services.GetRequiredService<JwtTokenIssuer>().Issue(
            accountId,
            telegramUserId);

    private static HubConnection CreateHubConnection(
        WebApplicationFactory<Program> factory,
        IssuedAccessToken token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/combat",
                options =>
                {
                    options.AccessTokenProvider = () =>
                        Task.FromResult<string?>(token.AccessToken);
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
}
