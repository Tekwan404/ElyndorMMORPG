using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Contracts.Dungeons;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.Dungeons;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonCombatAdmissionTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DungeonIsBoundBeforeImmediateAutoAttackAndVictoryAdvancesOnlyOnce()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        string monsterId = package.Dungeons!.Single(item => item.Id == "ANCIENT_MINE").Encounters[0].MonsterId;
        package = package with { Monsters = package.Monsters!.Select(monster => monster.Id == monsterId
            ? monster with { MaxHp = 1, Stats = monster.Stats with { Dodge = 0 } } : monster).ToArray() };
        AdmissionTimeProvider time = new(postgres);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
            builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
            builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
            builder.UseSetting("Authentication:SigningKey", "dungeon-admission-test-signing-key-with-more-than-32-bytes");
            builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
            builder.UseSetting("Authentication:Development:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(time);
                services.RemoveAll<IContentSnapshotProvider>();
                services.AddSingleton<IContentSnapshotProvider>(new StaticContentSnapshotProvider(package));
                services.RemoveAll<IGameRandomFactory>();
                services.AddSingleton<IGameRandomFactory>(new HitRandom());
            });
        });
        Guid accountId = Guid.CreateVersion7();
        await using (var db = postgres.CreateDbContext())
        {
            db.Accounts.Add(new Account(accountId, 987901, time.GetUtcNow()));
            await db.SaveChangesAsync();
        }
        using HttpClient client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<JwtTokenIssuer>().Issue(accountId, 987901);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var created = await client.PostAsJsonAsync("/api/v1/character",
            new CreateCharacterRequest(Guid.CreateVersion7(), "DungeonTimer", "HUMAN", "MALE", "WARRIOR"));
        created.EnsureSuccessStatusCode();
        CharacterResponse character = (await created.Content.ReadFromJsonAsync<CharacterResponse>())!;
        await using (var db = postgres.CreateDbContext())
        {
            Character player = await db.Characters.SingleAsync(item => item.Id == character.Id);
            player.SetLevel(25);
            var location = await db.CharacterLocations.SingleAsync(item => item.CharacterId == character.Id);
            location.Relocate("ANCIENT_MINE", time.GetUtcNow());
            await db.SaveChangesAsync();
        }
        var previews = (await client.GetFromJsonAsync<DungeonPreviewResponse[]>("/api/v1/dungeons"))!;
        var bastion = Assert.Single(previews, preview => preview.Id == "BLACK_BASTION");
        Assert.Equal(6, bastion.Floors!.Count);
        Assert.Equal(12, bastion.Encounters.Count);
        Assert.NotEmpty(bastion.PossibleLoot!);
        Assert.Equal(Enumerable.Range(0, 12), bastion.Floors.SelectMany(floor =>
            Enumerable.Range(floor.FirstEncounterIndex, floor.LastEncounterIndex - floor.FirstEncounterIndex + 1)));
        Assert.All(bastion.Floors, floor => Assert.False(string.IsNullOrWhiteSpace(floor.BossName)));
        using var scope = factory.Services.CreateScope();
        var dungeon = scope.ServiceProvider.GetRequiredService<DungeonService>();
        var combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();
        var run = await dungeon.CreateAsync(accountId, "ANCIENT_MINE", Guid.CreateVersion7(), CancellationToken.None);
        Assert.True(run.Succeeded, run.ErrorCode);
        time.RunId = run.Run!.RunId;
        CombatOperationResult started = await combat.StartDungeonEncounterAsync(accountId, run.Run.RunId, CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal(CombatSessionStatus.Active, started.Snapshot!.Status);
        Assert.Equal(1, started.Snapshot.Enemy.Hp);
        Assert.True(time.CheckedBinding);
        Assert.True(time.WasBoundBeforeTimer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        DungeonRun? persisted;
        do
        {
            await Task.Delay(20, timeout.Token);
            await using var db = postgres.CreateDbContext();
            persisted = await db.DungeonRuns.AsNoTracking().SingleAsync(item => item.Id == run.Run.RunId, timeout.Token);
        } while (persisted.CurrentEncounterIndex == 0);
        Assert.Equal(1, persisted.CurrentEncounterIndex);
        Assert.Equal(CombatSessionStatus.Active, started.Snapshot.Status);
        Assert.Equal(1, started.Snapshot.Enemy.Hp);
        await dungeon.GetCurrentAsync(accountId, CancellationToken.None);
        await dungeon.GetCurrentAsync(accountId, CancellationToken.None);
        await using var verification = postgres.CreateDbContext();
        Assert.Equal(1, (await verification.DungeonRuns.AsNoTracking().SingleAsync()).CurrentEncounterIndex);
        Assert.Equal(DungeonEncounterState.Completed, (await verification.DungeonEncounters.AsNoTracking().SingleAsync(encounter => encounter.EncounterIndex == 0)).State);
        Assert.Single(await verification.CombatRewardGrants.Where(grant => grant.CombatSessionId == started.Snapshot!.SessionId).ToArrayAsync());
        Assert.False(await verification.ActiveCombatSessions.AnyAsync());
        var registry = scope.ServiceProvider.GetRequiredService<CombatSessionRegistry>();
        using var finalizedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!registry.TryGetFinalizedStatus(started.Snapshot!.SessionId, out _))
            await Task.Delay(20, finalizedTimeout.Token);
        // Reproduce an old run that was bound after its runtime had already finalized.
        await verification.DungeonEncounters.Where(encounter => encounter.RunId == run.Run.RunId && encounter.EncounterIndex == 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(encounter => encounter.State, DungeonEncounterState.Active)
                .SetProperty(encounter => encounter.CompletedAtUtc, (DateTimeOffset?)null));
        await verification.DungeonRuns.Where(item => item.Id == run.Run.RunId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CurrentEncounterIndex, 0));
        await dungeon.GetCurrentAsync(accountId, CancellationToken.None);
        await dungeon.GetCurrentAsync(accountId, CancellationToken.None);
        Assert.Equal(1, (await verification.DungeonRuns.AsNoTracking().SingleAsync()).CurrentEncounterIndex);
        Assert.Single(await verification.CombatRewardGrants.Where(grant => grant.CombatSessionId == started.Snapshot.SessionId).ToArrayAsync());
        await verification.DungeonEncounters.Where(encounter => encounter.RunId == run.Run.RunId && encounter.EncounterIndex == 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(encounter => encounter.State, DungeonEncounterState.Active)
                .SetProperty(encounter => encounter.CompletedAtUtc, (DateTimeOffset?)null));
        await dungeon.GetCurrentAsync(accountId, CancellationToken.None);
        Assert.Equal(1, (await verification.DungeonRuns.AsNoTracking().SingleAsync()).CurrentEncounterIndex);
        Assert.Equal(2, await verification.DungeonEncounters.CountAsync());


    }

    private sealed class HitRandom : IGameRandomFactory, IGameRandom
    {
        public IGameRandom Create() => this;
        public decimal NextUnit() => .5m;
    }

    private sealed class AdmissionTimeProvider(PostgresFixture postgres) : TimeProvider
    {
        private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
        public Guid? RunId { get; set; }
        public bool CheckedBinding { get; private set; }
        public bool WasBoundBeforeTimer { get; private set; }
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (RunId is { } runId && !CheckedBinding && period == Timeout.InfiniteTimeSpan && dueTime == TimeSpan.Zero)
            {
                using var db = postgres.CreateDbContext();
                WasBoundBeforeTimer = db.DungeonEncounters.Any(encounter => encounter.RunId == runId
                    && encounter.State == DungeonEncounterState.Active && encounter.CombatSessionId != null);
                CheckedBinding = true;
            }
            return System.CreateTimer(callback, state, dueTime, period);
        }
    }
}
