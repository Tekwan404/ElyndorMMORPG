using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.WorldBosses;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.WorldBosses;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class WorldBossLifecycleTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly MutableTime _time = new();

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ActivationIsReplaySafeAndOnlyOneSpawnIsActive()
    {
        GameContentSnapshot content = await ContentAsync();

        await using (GameDbContext firstDb = postgres.CreateDbContext())
        {
            var service = new WorldBossLifecycleService(
                firstDb,
                new StaticContentSnapshotProvider(content.Package),
                _time);
            WorldBossActivationResult first = await service.ActivateAsync(
                "WORLD_BOSS_ASH_ARCHON",
                default);
            WorldBossActivationResult replay = await service.ActivateAsync(
                "WORLD_BOSS_ASH_ARCHON",
                default);

            Assert.True(first.Succeeded);
            Assert.True(first.Created);
            Assert.True(replay.Succeeded);
            Assert.False(replay.Created);
            Assert.Equal(first.Spawn!.Id, replay.Spawn!.Id);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(await verify.WorldBossSpawns
            .Where(spawn => spawn.Status == WorldBossSpawnStatus.Active)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentActivationCreatesOneActiveSpawn()
    {
        GameContentSnapshot content = await ContentAsync();

        async Task<Guid> Activate()
        {
            await using GameDbContext db = postgres.CreateDbContext();
            var service = new WorldBossLifecycleService(
                db,
                new StaticContentSnapshotProvider(content.Package),
                _time);
            WorldBossActivationResult result = await service.ActivateAsync(
                "WORLD_BOSS_ASH_ARCHON",
                default);
            Assert.True(result.Succeeded);
            return result.Spawn!.Id;
        }

        Guid[] ids = await Task.WhenAll(Activate(), Activate());

        Assert.Equal(ids[0], ids[1]);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(await verify.WorldBossSpawns
            .Where(spawn => spawn.Status == WorldBossSpawnStatus.Active)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ExpiredSpawnIsClosedBeforeNextActivation()
    {
        GameContentSnapshot content = await ContentAsync();
        Guid firstId;

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var service = new WorldBossLifecycleService(
                db,
                new StaticContentSnapshotProvider(content.Package),
                _time);
            firstId = (await service.ActivateAsync(
                "WORLD_BOSS_ASH_ARCHON",
                default)).Spawn!.Id;
        }

        _time.Now = _time.Now.AddMinutes(30);

        Guid secondId;
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var service = new WorldBossLifecycleService(
                db,
                new StaticContentSnapshotProvider(content.Package),
                _time);
            secondId = (await service.ActivateAsync(
                "WORLD_BOSS_ASH_ARCHON",
                default)).Spawn!.Id;
        }

        Assert.NotEqual(firstId, secondId);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(WorldBossSpawnStatus.Expired,
            (await verify.WorldBossSpawns.SingleAsync(spawn => spawn.Id == firstId)).Status);
        Assert.Equal(WorldBossSpawnStatus.Active,
            (await verify.WorldBossSpawns.SingleAsync(spawn => spawn.Id == secondId)).Status);
    }

    [Fact]
    public async Task ActiveReadIncludesPersonalAndPartyContribution()
    {
        GameContentSnapshot content = await ContentAsync();
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid spawnId;
        Guid partyId = Guid.CreateVersion7();

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            db.Accounts.Add(new Account(
                accountId,
                Random.Shared.NextInt64(1, long.MaxValue),
                _time.Now));
            db.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Reader",
                $"READER{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                _time.Now));
            db.Parties.Add(Party.Create(
                partyId,
                Guid.CreateVersion7(),
                characterId,
                _time.Now));
            await db.SaveChangesAsync();
        }

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var lifecycle = new WorldBossLifecycleService(
                db,
                new StaticContentSnapshotProvider(content.Package),
                _time);
            spawnId = (await lifecycle.ActivateAsync(
                "WORLD_BOSS_ASH_ARCHON",
                default)).Spawn!.Id;
        }

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var personal = new WorldBossContribution(spawnId, characterId, _time.Now);
            personal.AddDamage(12_500m, _time.Now.AddSeconds(1));
            var party = new WorldBossPartyContribution(spawnId, partyId);
            party.AddDamage(12_500m);
            db.WorldBossContributions.Add(personal);
            db.WorldBossPartyContributions.Add(party);
            await db.SaveChangesAsync();
        }

        await using GameDbContext readDb = postgres.CreateDbContext();
        var reader = new WorldBossReadService(
            readDb,
            new StaticContentSnapshotProvider(content.Package),
            _time);
        WorldBossActiveReadResult result = await reader.GetActiveAsync(accountId, default);

        Assert.True(result.CharacterFound);
        Assert.NotNull(result.Active);
        Assert.Equal("Архон Пепла", result.Active!.Name);
        Assert.Equal(1_000_000m, result.Active.MaxHealth);
        Assert.Equal(12_500m, result.Active.PersonalDamage);
        Assert.Equal(12_500m, result.Active.PartyDamage);
        Assert.Equal(1, result.Active.Participants);
        Assert.Equal("Пробуждение", result.Active.PhaseName);
    }

    private static async Task<GameContentSnapshot> ContentAsync()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        return GameContentSnapshot.Create(package);
    }

    private sealed class MutableTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } =
            new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
