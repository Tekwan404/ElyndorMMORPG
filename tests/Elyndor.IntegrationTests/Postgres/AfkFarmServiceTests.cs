using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.Monsters;
using Elyndor.Core.Parties;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Afk;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Postgres;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class AfkFarmServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
    private const string ForestId = "AFK_TEST_FOREST";
    private const string WolfId = "AFK_TEST_WOLF";

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StartPersistsSessionAndSecondStartIsBlocked()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(dbContext, CreateContent());

        AfkFarmMutationResult first = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);
        AfkFarmMutationResult second = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.NotNull(first.Session);
        Assert.False(second.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.AlreadyActive, second.ErrorCode);

        await using GameDbContext freshContext = postgres.CreateDbContext();
        AfkFarmSession stored = await freshContext.AfkFarmSessions.SingleAsync();
        Assert.Equal(AfkFarmStatus.Active, stored.Status);
        Assert.Equal("0.1.0", stored.ContentVersion);
        Assert.Equal("0.1.0", stored.BalanceVersion);
        Assert.Contains("classId", stored.CharacterSnapshotJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidLocationIsRejected()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(dbContext, CreateContent());

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            "UNKNOWN_LOCATION",
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.InvalidLocation, result.ErrorCode);
    }

    [Fact]
    public async Task LockedLocationIsRejected()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(dbContext, CreateContent(minimumLevel: 10));

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.LockedLocation, result.ErrorCode);
        Assert.Empty(await dbContext.AfkFarmSessions.ToArrayAsync());
    }

    [Fact]
    public async Task RequiredContractLocationIsRejectedUntilUnlocked()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(
            dbContext,
            CreateContent(requiredContractId: "AFK_UNLOCK_CONTRACT"));

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.LockedLocation, result.ErrorCode);
    }

    [Fact]
    public async Task LocationCanExplicitlyDisableAfk()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(dbContext, CreateContent(allowAfk: false));

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.NotAllowed, result.ErrorCode);
    }

    [Fact]
    public async Task DangerousLocationIsRejectedEvenWhenContentEnablesAfk()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(
            dbContext,
            CreateContent(dangerLevel: "DANGEROUS"));

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.NotAllowed, result.ErrorCode);
    }

    [Fact]
    public async Task BossOnlyLocationIsNotEligibleForAfk()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(dbContext, CreateContent(monsterRank: MonsterRank.Boss));

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.NoEligibleEncounters, result.ErrorCode);
    }

    [Fact]
    public async Task DeadCharacterIsRejected()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId, currentHp: 0);
        AfkFarmService service = CreateService(dbContext, CreateContent());

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.CharacterDead, result.ErrorCode);
    }

    [Fact]
    public async Task DurableCombatConflictIsRejected()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        Guid characterId = await dbContext.Characters.Select(character => character.Id).SingleAsync();
        dbContext.ActiveCombatSessions.Add(new ActiveCombatSession(
            Guid.CreateVersion7(),
            characterId,
            Now,
            "0.1.0",
            "0.1.0"));
        await dbContext.SaveChangesAsync();
        AfkFarmService service = CreateService(dbContext, CreateContent());

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.InCombat, result.ErrorCode);
    }

    [Fact]
    public async Task TravelConflictIsRejected()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        Guid characterId = await dbContext.Characters.Select(character => character.Id).SingleAsync();
        dbContext.CharacterTravelStates.Add(new CharacterTravelState(
            characterId,
            Guid.CreateVersion7(),
            ForestId,
            "AFK_TEST_OTHER",
            Now,
            Now.AddMinutes(5)));
        await dbContext.SaveChangesAsync();
        AfkFarmService service = CreateService(dbContext, CreateContent());

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.Traveling, result.ErrorCode);
    }

    [Fact]
    public async Task ActiveDungeonConflictIsRejected()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        Guid characterId = await dbContext.Characters.Select(character => character.Id).SingleAsync();
        Party party = Party.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), characterId, Now);
        DungeonRun run = DungeonRun.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            party.Id,
            "AFK_TEST_DUNGEON",
            Now);
        run.AddMember(characterId, Now);
        dbContext.Parties.Add(party);
        dbContext.DungeonRuns.Add(run);
        await dbContext.SaveChangesAsync();
        AfkFarmService service = CreateService(dbContext, CreateContent());

        AfkFarmMutationResult result = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AfkFarmErrorCodes.InDungeon, result.ErrorCode);
    }

    [Fact]
    public async Task StopIsIdempotent()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext, ForestId);
        AfkFarmService service = CreateService(dbContext, CreateContent());
        AfkFarmMutationResult started = await service.StartAsync(
            accountId,
            ForestId,
            AfkFarmMode.Safe,
            TimeSpan.FromHours(1),
            CancellationToken.None);
        Assert.True(started.Succeeded);

        AfkFarmMutationResult firstStop = await service.StopAsync(accountId, CancellationToken.None);
        AfkFarmMutationResult secondStop = await service.StopAsync(accountId, CancellationToken.None);

        Assert.True(firstStop.Succeeded);
        Assert.True(secondStop.Succeeded);
        Assert.Equal(AfkFarmStatus.Cancelled, firstStop.Session!.Status);
        Assert.Equal(firstStop.Session.Id, secondStop.Session!.Id);
        Assert.Equal(firstStop.Session.Version, secondStop.Session.Version);
        Assert.Single(await dbContext.AfkFarmSessions.ToArrayAsync());
    }

    private static AfkFarmService CreateService(GameDbContext dbContext, GameContentPackage content)
    {
        StaticContentSnapshotProvider provider = new(content);
        CharacterDerivedStateService derived = new(dbContext, provider, null);
        CharacterOperationGuard guard = new(new NoCombatActivity());
        return new AfkFarmService(dbContext, provider, derived, guard, new FixedTimeProvider());
    }

    private static GameContentPackage CreateContent(
        int minimumLevel = 1,
        bool allowAfk = true,
        string? requiredContractId = null,
        MonsterRank monsterRank = MonsterRank.Normal,
        string dangerLevel = "ADVENTURE")
    {
        LocationDefinition location = new(
            ForestId,
            "AFK test forest",
            dangerLevel,
            minimumLevel,
            [],
            [new LocationEncounterDefinition(WolfId, 1m)],
            MinimumLevel: minimumLevel,
            MaximumLevel: 60,
            RequiredContractId: requiredContractId,
            AllowAfk: allowAfk);
        MonsterDefinition wolf = new(
            WolfId,
            "Wolf",
            monsterRank,
            1,
            50,
            new CombatStats(1, 90, 0, 5, 1.5m, 5, 0, 0, 0, 5, 0),
            TimeSpan.FromSeconds(2),
            5,
            [],
            "AFK_TEST_AI");

        return PhaseTwoTestContent.Create(Now, [], [location]) with
        {
            Monsters = [wolf],
            MonsterAiProfiles = [new("AFK_TEST_AI", [])]
        };
    }

    private static async Task<Guid> SeedCharacterAsync(
        GameDbContext dbContext,
        string locationId,
        decimal currentHp = 100)
    {
        Guid accountId = Guid.CreateVersion7();
        Character character = new(
            Guid.CreateVersion7(),
            accountId,
            Guid.CreateVersion7(),
            "AfkServiceTester",
            "AFKSERVICETESTER",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        dbContext.Accounts.Add(new Account(accountId, 99112233, Now));
        dbContext.Characters.Add(character);
        dbContext.CharacterVitals.Add(new CharacterVitals(character.Id, currentHp, 0, Now, Now));
        dbContext.CharacterLocations.Add(new CharacterLocation(character.Id, locationId, 1, Now));
        await dbContext.SaveChangesAsync();
        return accountId;
    }

    private sealed class NoCombatActivity : ICombatActivityReader
    {
        public bool HasActiveCombat(Guid accountId) => false;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
