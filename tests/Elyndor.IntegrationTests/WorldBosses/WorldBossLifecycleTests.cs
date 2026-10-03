using Elyndor.Core.Characters;
using Elyndor.Core.Content;
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
            personal.AddDamage(73_420m, _time.Now.AddSeconds(1));
            var party = new WorldBossPartyContribution(spawnId, partyId);
            party.AddDamage(73_420m);
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
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_L60", result.Active.MonsterId);
        Assert.Equal("namestnik-tsitadeli", result.Active.ArtId);
        Assert.Equal(1_000_000m, result.Active.MaxHealth);
        Assert.Equal(5_000m, result.Active.MinimumContribution);
        Assert.Equal(73_420m, result.Active.PersonalDamage);
        Assert.Equal(73_420m, result.Active.PartyDamage);
        Assert.True(result.Active.RewardEligible);
        Assert.Equal("Top5", result.Active.RewardTier);
        Assert.Null(result.Active.NextRewardTier);
        Assert.Null(result.Active.NextRewardTierAtDamage);
        Assert.Equal(0m, result.Active.DamageToNextRewardTier);
        Assert.Equal(1, result.Active.EligibleParticipants);
        Assert.Equal(1, result.Active.PersonalRewardRank);
        Assert.Equal(100m, result.Active.RewardPercentile);
        Assert.Equal(0, result.Active.RewardChestCount);
        Assert.Equal(2, result.Active.RewardEnhancedChestCount);
        Assert.Equal(1, result.Active.Participants);
        Assert.Equal("Пробуждение", result.Active.PhaseName);
    }

    [Fact]
    public async Task LeaderboardReadRanksPlayersAndPartiesAndReturnsCurrentPlayerPosition()
    {
        GameContentSnapshot content = await ContentAsync();
        Guid[] accounts = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        Guid[] characters = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        Guid firstPartyId = Guid.CreateVersion7();
        Guid secondPartyId = Guid.CreateVersion7();

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            for (int index = 0; index < accounts.Length; index++)
            {
                db.Accounts.Add(new Account(
                    accounts[index],
                    Random.Shared.NextInt64(1, long.MaxValue),
                    _time.Now));
                db.Characters.Add(new Character(
                    characters[index],
                    accounts[index],
                    Guid.CreateVersion7(),
                    $"Ranker{index + 1}",
                    $"RANK{index}{characters[index]:N}"[..16].ToUpperInvariant(),
                    "HUMAN",
                    "MALE",
                    "WARRIOR",
                    _time.Now));
            }

            db.Parties.Add(Party.Create(
                firstPartyId,
                Guid.CreateVersion7(),
                characters[0],
                _time.Now));
            db.Parties.Add(Party.Create(
                secondPartyId,
                Guid.CreateVersion7(),
                characters[1],
                _time.Now));
            await db.SaveChangesAsync();
        }

        Guid spawnId;
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
            decimal[] damages = [50_000m, 125_000m, 75_000m];
            for (int index = 0; index < characters.Length; index++)
            {
                var contribution = new WorldBossContribution(
                    spawnId,
                    characters[index],
                    _time.Now);
                contribution.AddDamage(damages[index], _time.Now.AddSeconds(index + 1));
                db.WorldBossContributions.Add(contribution);
            }

            var firstParty = new WorldBossPartyContribution(spawnId, firstPartyId);
            firstParty.AddDamage(50_000m);
            db.WorldBossPartyContributions.Add(firstParty);
            var secondParty = new WorldBossPartyContribution(spawnId, secondPartyId);
            secondParty.AddDamage(125_000m);
            db.WorldBossPartyContributions.Add(secondParty);
            await db.SaveChangesAsync();
        }

        await using GameDbContext readDb = postgres.CreateDbContext();
        var reader = new WorldBossReadService(
            readDb,
            new StaticContentSnapshotProvider(content.Package),
            _time);
        WorldBossLeaderboardReadResult result = await reader.GetLeaderboardAsync(
            accounts[1],
            spawnId,
            default);

        Assert.True(result.CharacterFound);
        Assert.True(result.SpawnFound);
        Assert.Equal(characters[1], result.Players[0].CharacterId);
        Assert.Equal(1, result.Players[0].Rank);
        Assert.Equal(characters[2], result.Players[1].CharacterId);
        Assert.Equal(characters[0], result.Players[2].CharacterId);
        Assert.Equal(1, result.PersonalRank);
        Assert.Equal(125_000m, result.PersonalDamage);
        Assert.Equal(secondPartyId, result.PartyId);
        Assert.Equal(1, result.PartyRank);
        Assert.Equal(125_000m, result.PartyDamage);
        Assert.Equal(secondPartyId, result.Parties[0].PartyId);
        Assert.Equal(firstPartyId, result.Parties[1].PartyId);
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
