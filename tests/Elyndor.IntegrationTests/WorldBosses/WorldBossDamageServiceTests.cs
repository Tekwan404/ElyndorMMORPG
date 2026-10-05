using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.WorldBosses;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.WorldBosses;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class WorldBossDamageServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DefeatPublishesRealtimeEventAfterDurableCommit()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 2_000m);
        var publisher = new CapturingPublisher();

        await using var db = postgres.CreateDbContext();
        var service = new WorldBossDamageService(
            db,
            new FixedTime(Now.AddMinutes(1)),
            updatePublisher: publisher);
        WorldBossDamageCommitResult result = await service.ApplyDamageAsync(
            spawnId,
            characterId,
            Guid.NewGuid(),
            partyId: null,
            requestedDamage: 2_000m,
            mutationId: Guid.NewGuid(),
            cancellationToken: default);

        Assert.True(result.DefeatedNow);
        Assert.Equal(spawnId, publisher.DefeatedSpawnId);
        Assert.Equal(Now.AddMinutes(1), publisher.DefeatedAtUtc);
        Assert.Single(publisher.ParticipantAccountIds);

        await using var verify = postgres.CreateDbContext();
        WorldBossSpawn spawn = await verify.WorldBossSpawns.SingleAsync(
            candidate => candidate.Id == spawnId);
        Assert.Equal(WorldBossSpawnStatus.Defeated, spawn.Status);
        Assert.Equal(0m, spawn.CurrentHealth);
    }

    [Fact]
    public async Task OverkillPersistsOnlyActualHealthRemovedAndDefeatsOnce()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 2_000m);

        await using var db = postgres.CreateDbContext();
        var service = new WorldBossDamageService(db, new FixedTime(Now.AddMinutes(1)));
        WorldBossDamageCommitResult result = await service.ApplyDamageAsync(
            spawnId,
            characterId,
            Guid.NewGuid(),
            partyId: null,
            requestedDamage: 15_000m,
            mutationId: Guid.NewGuid(),
            cancellationToken: default);

        Assert.True(result.Succeeded);
        Assert.True(result.DefeatedNow);
        Assert.Equal(2_000m, result.AppliedDamage);
        Assert.Equal(0m, result.CurrentHealth);
        Assert.Equal(WorldBossSpawnStatus.Defeated, result.Status);

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2_000m, (await verify.WorldBossContributions.SingleAsync()).Damage);
        Assert.Equal(2_000m, (await verify.WorldBossDamageMutations.SingleAsync()).AppliedDamage);
    }

    [Fact]
    public async Task ExactMutationReplayDoesNotDamageOrContributeTwice()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 10_000m);
        Guid mutationId = Guid.NewGuid();
        Guid combatSessionId = Guid.NewGuid();

        await using (var db = postgres.CreateDbContext())
        {
            var service = new WorldBossDamageService(db, new FixedTime(Now.AddMinutes(1)));
            WorldBossDamageCommitResult first = await service.ApplyDamageAsync(
                spawnId, characterId, combatSessionId, null, 4_000m, mutationId, default);
            WorldBossDamageCommitResult replay = await service.ApplyDamageAsync(
                spawnId, characterId, combatSessionId, null, 4_000m, mutationId, default);

            Assert.True(first.Succeeded);
            Assert.False(first.Replayed);
            Assert.True(replay.Succeeded);
            Assert.True(replay.Replayed);
            Assert.Equal(4_000m, replay.AppliedDamage);
        }

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(6_000m, (await verify.WorldBossSpawns.SingleAsync()).CurrentHealth);
        Assert.Equal(4_000m, (await verify.WorldBossContributions.SingleAsync()).Damage);
        Assert.Single(await verify.WorldBossDamageMutations.ToListAsync());
    }

    [Fact]
    public async Task ReusedMutationWithDifferentPayloadIsRejected()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 10_000m);
        Guid mutationId = Guid.NewGuid();
        Guid combatSessionId = Guid.NewGuid();

        await using var db = postgres.CreateDbContext();
        var service = new WorldBossDamageService(db, new FixedTime(Now.AddMinutes(1)));
        Assert.True((await service.ApplyDamageAsync(
            spawnId, characterId, combatSessionId, null, 1_000m, mutationId, default)).Succeeded);

        WorldBossDamageCommitResult conflict = await service.ApplyDamageAsync(
            spawnId, characterId, combatSessionId, null, 2_000m, mutationId, default);

        Assert.False(conflict.Succeeded);
        Assert.Equal(WorldBossErrorCodes.MutationConflict, conflict.ErrorCode);

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(9_000m, (await verify.WorldBossSpawns.SingleAsync()).CurrentHealth);
        Assert.Equal(1_000m, (await verify.WorldBossContributions.SingleAsync()).Damage);
    }

    [Fact]
    public async Task PartyDamageUpdatesPersonalAndPartyLedgersWithoutMultiplyingContribution()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 20_000m);
        Guid partyId;

        await using (var db = postgres.CreateDbContext())
        {
            Party party = Party.Create(Guid.NewGuid(), Guid.NewGuid(), characterId, Now);
            partyId = party.Id;
            db.Parties.Add(party);
            await db.SaveChangesAsync();
        }

        await using (var db = postgres.CreateDbContext())
        {
            var service = new WorldBossDamageService(db, new FixedTime(Now.AddMinutes(1)));
            WorldBossDamageCommitResult result = await service.ApplyDamageAsync(
                spawnId,
                characterId,
                Guid.NewGuid(),
                partyId,
                requestedDamage: 7_500m,
                mutationId: Guid.NewGuid(),
                cancellationToken: default);
            Assert.True(result.Succeeded);
            Assert.Equal(7_500m, result.AppliedDamage);
        }

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(7_500m, (await verify.WorldBossContributions.SingleAsync()).Damage);
        Assert.Equal(7_500m, (await verify.WorldBossPartyContributions.SingleAsync()).Damage);
    }

    [Theory]
    [InlineData(260_000, 2, true)]
    [InlineData(250_000, 2, true)]
    [InlineData(240_000, 1, false)]
    public async Task DamageCrossesGlobalPhaseThresholdInsideSameTransaction(
        decimal requestedDamage,
        int expectedPhase,
        bool expectedPhaseChanged)
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 1_000_000m);
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        await using var db = postgres.CreateDbContext();
        var service = new WorldBossDamageService(
            db,
            new FixedTime(Now.AddMinutes(1)),
            new StaticContentSnapshotProvider(package));

        WorldBossDamageCommitResult result = await service.ApplyDamageAsync(
            spawnId,
            characterId,
            Guid.NewGuid(),
            partyId: null,
            requestedDamage,
            mutationId: Guid.NewGuid(),
            cancellationToken: default);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedPhase, result.Phase);
        Assert.Equal(expectedPhaseChanged, result.PhaseChanged);

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(expectedPhase, (await verify.WorldBossSpawns.SingleAsync()).CurrentPhase);
    }

    [Fact]
    public async Task OneLargeHitCanAdvanceAcrossMultipleThresholdsDirectlyToCurrentGlobalPhase()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 1_000_000m);
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        await using var db = postgres.CreateDbContext();
        var service = new WorldBossDamageService(
            db,
            new FixedTime(Now.AddMinutes(1)),
            new StaticContentSnapshotProvider(package));

        WorldBossDamageCommitResult result = await service.ApplyDamageAsync(
            spawnId,
            characterId,
            Guid.NewGuid(),
            partyId: null,
            requestedDamage: 760_000m,
            mutationId: Guid.NewGuid(),
            cancellationToken: default);

        Assert.True(result.Succeeded);
        Assert.True(result.PhaseChanged);
        Assert.Equal(4, result.Phase);
        Assert.Equal(240_000m, result.CurrentHealth);
    }

    [Fact]
    public async Task ConcurrentHitsNeverDriveHealthNegativeAndOnlyOneMutationDefeatsBoss()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 5_000m);

        async Task<WorldBossDamageCommitResult> Hit(decimal damage)
        {
            await using var db = postgres.CreateDbContext();
            return await new WorldBossDamageService(db, new FixedTime(Now.AddMinutes(1))).ApplyDamageAsync(
                spawnId,
                characterId,
                Guid.NewGuid(),
                partyId: null,
                requestedDamage: damage,
                mutationId: Guid.NewGuid(),
                cancellationToken: default);
        }

        WorldBossDamageCommitResult[] results = await Task.WhenAll(Hit(4_000m), Hit(7_000m));

        Assert.Single(results, result => result.DefeatedNow);
        Assert.True(results.Sum(result => result.AppliedDamage) <= 5_000m);

        await using var verify = postgres.CreateDbContext();
        WorldBossSpawn spawn = await verify.WorldBossSpawns.SingleAsync();
        Assert.Equal(0m, spawn.CurrentHealth);
        Assert.Equal(WorldBossSpawnStatus.Defeated, spawn.Status);
        Assert.Equal(5_000m, (await verify.WorldBossContributions.SingleAsync()).Damage);
    }

    [Fact]
    public async Task ConcurrentExactMutationIsAppliedOnce()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 10_000m);
        Guid mutationId = Guid.NewGuid();
        Guid combatSessionId = Guid.NewGuid();

        async Task<WorldBossDamageCommitResult> Hit()
        {
            await using var db = postgres.CreateDbContext();
            return await new WorldBossDamageService(db, new FixedTime(Now.AddMinutes(1))).ApplyDamageAsync(
                spawnId,
                characterId,
                combatSessionId,
                partyId: null,
                requestedDamage: 4_000m,
                mutationId,
                cancellationToken: default);
        }

        WorldBossDamageCommitResult[] results = await Task.WhenAll(Hit(), Hit());

        Assert.Single(results, result => result.Replayed);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(6_000m, (await verify.WorldBossSpawns.SingleAsync()).CurrentHealth);
        Assert.Equal(4_000m, (await verify.WorldBossContributions.SingleAsync()).Damage);
        Assert.Single(await verify.WorldBossDamageMutations.ToListAsync());
    }

    [Fact]
    public async Task DamageAtOrAfterExpiryAtomicallyExpiresSpawnAndIsNotCounted()
    {
        (Guid spawnId, Guid characterId) = await SeedAsync(maxHealth: 10_000m);

        await using var db = postgres.CreateDbContext();
        var service = new WorldBossDamageService(db, new FixedTime(Now.AddMinutes(30)));
        WorldBossDamageCommitResult result = await service.ApplyDamageAsync(
            spawnId,
            characterId,
            Guid.NewGuid(),
            null,
            1_000m,
            Guid.NewGuid(),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal(WorldBossErrorCodes.Expired, result.ErrorCode);
        Assert.Equal(WorldBossSpawnStatus.Expired, result.Status);

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(10_000m, (await verify.WorldBossSpawns.SingleAsync()).CurrentHealth);
        Assert.Empty(await verify.WorldBossContributions.ToListAsync());
        Assert.Empty(await verify.WorldBossDamageMutations.ToListAsync());
    }

    private async Task<(Guid SpawnId, Guid CharacterId)> SeedAsync(decimal maxHealth)
    {
        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        Guid spawnId = Guid.NewGuid();

        await using var db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        db.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.NewGuid(),
            "WorldBossTester",
            $"WB{characterId:N}"[..16].ToUpperInvariant(),
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        db.WorldBossSpawns.Add(new WorldBossSpawn(
            spawnId,
            "WORLD_BOSS_ASH_ARCHON",
            maxHealth,
            initialPhase: 1,
            spawnedAtUtc: Now,
            expiresAtUtc: Now.AddMinutes(30),
            contentVersion: "test-content",
            balanceVersion: "test-balance"));
        await db.SaveChangesAsync();
        return (spawnId, characterId);
    }

    private sealed class CapturingPublisher : IWorldBossUpdatePublisher
    {
        public Guid? DefeatedSpawnId { get; private set; }
        public DateTimeOffset? DefeatedAtUtc { get; private set; }
        public IReadOnlyCollection<Guid> ParticipantAccountIds { get; private set; } = [];

        public Task PublishActivatedAsync(
            Guid spawnId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishProgressAsync(
            Guid spawnId,
            decimal currentHealth,
            decimal maxHealth,
            int currentPhase,
            bool phaseChanged,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishDefeatedAsync(
            Guid spawnId,
            DateTimeOffset defeatedAtUtc,
            IReadOnlyCollection<Guid> participantAccountIds,
            CancellationToken cancellationToken)
        {
            DefeatedSpawnId = spawnId;
            DefeatedAtUtc = defeatedAtUtc;
            ParticipantAccountIds = participantAccountIds;
            return Task.CompletedTask;
        }

        public Task PublishSettledAsync(
            Guid spawnId,
            DateTimeOffset settledAtUtc,
            IReadOnlyCollection<WorldBossRewardDelivery> deliveries,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
