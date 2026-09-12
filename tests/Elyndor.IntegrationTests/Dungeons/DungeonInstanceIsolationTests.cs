using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Dungeons;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonInstanceIsolationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 6, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TwoPartiesStartingSameDungeonConcurrentlyStayIsolated()
    {
        Guid accountA = Guid.Parse("81000000-0000-0000-0000-000000000001");
        Guid accountB = Guid.Parse("81000000-0000-0000-0000-000000000002");
        Guid characterA = Guid.Parse("81111111-1111-1111-1111-111111111111");
        Guid characterB = Guid.Parse("82222222-2222-2222-2222-222222222222");

        await SeedCharactersAsync(accountA, accountB, characterA, characterB);

        FixedTimeProvider time = new(Now);
        await using (GameDbContext setupContext = postgres.CreateDbContext())
        {
            PartyService parties = new(setupContext, time);
            Assert.True((await parties.CreateAsync(
                accountA,
                Guid.NewGuid(),
                CancellationToken.None)).IsSuccess);
            Assert.True((await parties.CreateAsync(
                accountB,
                Guid.NewGuid(),
                CancellationToken.None)).IsSuccess);
        }

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using GameDbContext contextA = postgres.CreateDbContext();
        await using GameDbContext contextB = postgres.CreateDbContext();
        DungeonService dungeonsA = new(
            contextA,
            new PartyService(contextA, time),
            contentProvider,
            time);
        DungeonService dungeonsB = new(
            contextB,
            new PartyService(contextB, time),
            contentProvider,
            time);

        Task<DungeonOperationResult> createATask = dungeonsA.CreateAsync(
            accountA,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        Task<DungeonOperationResult> createBTask = dungeonsB.CreateAsync(
            accountB,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);

        DungeonOperationResult[] created = await Task.WhenAll(createATask, createBTask);
        Assert.All(created, result => Assert.True(result.Succeeded, result.ErrorCode));
        Assert.NotNull(created[0].Run);
        Assert.NotNull(created[1].Run);
        Assert.NotEqual(created[0].Run!.RunId, created[1].Run!.RunId);

        (DungeonPreparation? preparationA, string? errorA) =
            await dungeonsA.PrepareEncounterAsync(
                accountA,
                created[0].Run!.RunId,
                CancellationToken.None);
        (DungeonPreparation? preparationB, string? errorB) =
            await dungeonsB.PrepareEncounterAsync(
                accountB,
                created[1].Run!.RunId,
                CancellationToken.None);

        Assert.Null(errorA);
        Assert.Null(errorB);
        Assert.NotNull(preparationA);
        Assert.NotNull(preparationB);
        Assert.NotEqual(preparationA!.RunId, preparationB!.RunId);
        Assert.NotEqual(preparationA.EncounterId, preparationB.EncounterId);
        Assert.Single(preparationA.Participants);
        Assert.Single(preparationB.Participants);
        Assert.Equal(characterA, preparationA.Participants[0].CharacterId);
        Assert.Equal(characterB, preparationB.Participants[0].CharacterId);

        Guid sessionA = Guid.Parse("83333333-3333-3333-3333-333333333333");
        Guid sessionB = Guid.Parse("84444444-4444-4444-4444-444444444444");
        Assert.True(await dungeonsA.BindCombatAsync(
            preparationA.RunId,
            preparationA.EncounterId,
            sessionA,
            CancellationToken.None));
        Assert.True(await dungeonsB.BindCombatAsync(
            preparationB.RunId,
            preparationB.EncounterId,
            sessionB,
            CancellationToken.None));

        await dungeonsA.HandleCombatFinishedAsync(
            VictorySnapshot(sessionA),
            CancellationToken.None);

        contextB.ChangeTracker.Clear();
        DungeonRunView? stillActiveB = await dungeonsB.GetCurrentAsync(
            accountB,
            CancellationToken.None);
        Assert.NotNull(stillActiveB);
        Assert.Equal(DungeonRunState.Active, stillActiveB!.State);
        Assert.Equal(0, stillActiveB.CurrentEncounterIndex);
        Assert.Contains(
            stillActiveB.Encounters,
            encounter => encounter.EncounterIndex == 0
                && encounter.State == DungeonEncounterState.Active);

        await using GameDbContext verificationContext = postgres.CreateDbContext();
        DungeonRun persistedA = await verificationContext.DungeonRuns
            .Include(run => run.Encounters)
            .SingleAsync(run => run.Id == created[0].Run!.RunId);
        DungeonRun persistedB = await verificationContext.DungeonRuns
            .Include(run => run.Encounters)
            .SingleAsync(run => run.Id == created[1].Run!.RunId);

        Assert.NotEqual(persistedA.Id, persistedB.Id);
        Assert.NotEqual(persistedA.PartyId, persistedB.PartyId);
        Assert.Equal(1, persistedA.CurrentEncounterIndex);
        Assert.Equal(0, persistedB.CurrentEncounterIndex);
        Assert.Equal(DungeonRunState.Active, persistedB.State);
        Assert.Equal(
            sessionB,
            persistedB.Encounters.Single(encounter => encounter.EncounterIndex == 0).CombatSessionId);
    }

    private async Task SeedCharactersAsync(
        Guid accountA,
        Guid accountB,
        Guid characterA,
        Guid characterB)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(accountA, 8101, Now),
            new Account(accountB, 8102, Now));

        Character first = CreateCharacter(characterA, accountA, "PartyA");
        Character second = CreateCharacter(characterB, accountB, "PartyB");
        first.SetLevel(15);
        second.SetLevel(15);
        context.Characters.AddRange(first, second);
        context.CharacterVitals.AddRange(
            new CharacterVitals(characterA, 200, 0, Now, Now),
            new CharacterVitals(characterB, 200, 0, Now, Now));
        context.CharacterLocations.AddRange(
            new CharacterLocation(characterA, "ANCIENT_MINE", 1, Now),
            new CharacterLocation(characterB, "ANCIENT_MINE", 1, Now));
        await context.SaveChangesAsync();
    }

    private static Character CreateCharacter(Guid id, Guid accountId, string name) => new(
        id,
        accountId,
        Guid.NewGuid(),
        name,
        name.ToUpperInvariant(),
        "HUMAN",
        "MALE",
        "WARRIOR",
        Now);

    private static CombatSessionSnapshot VictorySnapshot(Guid sessionId)
    {
        CombatActorSnapshot player = new(
            Guid.Parse("85555555-5555-5555-5555-555555555555"),
            CombatActorKind.Player,
            "WARRIOR",
            "Winner",
            200,
            200,
            "RAGE",
            0,
            100,
            false,
            null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);
        CombatActorSnapshot enemy = new(
            Guid.Parse("86666666-6666-6666-6666-666666666666"),
            CombatActorKind.Monster,
            "DEEP_WOLF_L6",
            "Wolf",
            0,
            100,
            "NONE",
            0,
            0,
            false,
            null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);
        return new CombatSessionSnapshot(
            sessionId,
            1,
            CombatSessionStatus.Victory,
            Now,
            player,
            enemy);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
