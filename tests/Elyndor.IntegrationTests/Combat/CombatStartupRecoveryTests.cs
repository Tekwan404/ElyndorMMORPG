using System.Text.Json;
using System.Data.Common;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Contribution;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatStartupRecoveryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 6, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrphanReconciliationUsesCommittedVictoryOtherwiseWipesOnlyOnce(bool victory)
    {
        Seed seed = await SeedAsync(journal: false, reward: victory);
        await using ServiceProvider provider = await CreateProviderAsync();
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        DungeonEncounter encounter = await verify.DungeonEncounters.SingleAsync(candidate => candidate.Id == seed.EncounterId);
        DungeonRun run = await verify.DungeonRuns.SingleAsync();
        Assert.Equal(victory ? DungeonEncounterState.Completed : DungeonEncounterState.Wiped, encounter.State);
        Assert.Equal(victory ? 0 : 1, encounter.WipeCount);
        Assert.Equal(victory ? 1 : 0, run.CurrentEncounterIndex);
        Assert.Equal(victory ? 2 : 1, await verify.DungeonEncounters.CountAsync());
        Assert.Equal(DungeonRunState.Active, run.State);
        Assert.Equal(victory ? 1 : 0, await verify.CombatRewardGrants.CountAsync());
        Assert.Equal(0, (await verify.Characters.SingleAsync()).Experience);
    }

    [Fact]
    public async Task CommittedVictoryWithoutSnapshotCompletesEncounterWithoutReissuingRewards()
    {
        Seed seed = await SeedAsync(journal: true, reward: true);
        await using ServiceProvider provider = await CreateProviderAsync();
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.ActiveCombatSessions.ToArrayAsync());
        CombatRewardGrant reward = await verify.CombatRewardGrants.SingleAsync();
        Assert.Equal(5, reward.XpEarned);
        Assert.Equal(3, reward.GoldEarned);
        Assert.Equal(DungeonEncounterState.Completed,
            (await verify.DungeonEncounters.SingleAsync(candidate => candidate.Id == seed.EncounterId)).State);
        Assert.Equal(1, (await verify.DungeonRuns.SingleAsync()).CurrentEncounterIndex);
        Assert.Empty(await verify.CharacterItems.ToArrayAsync());
    }

    [Fact]
    public async Task TerminalJournalIsPreservedUntilFinalizationAndStartupRetriesFailure()
    {
        Seed seed = await SeedAsync(journal: true, terminal: true);
        await using ServiceProvider provider = await CreateProviderAsync(failures: 1);
        using (IServiceScope scope = provider.CreateScope())
        {
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DungeonService>()
                .ReconcileOrphanedEncountersAtStartupAsync(CancellationToken.None));
        }
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.ActiveCombatSessions.ToArrayAsync());
        DungeonEncounter encounter = await verify.DungeonEncounters.SingleAsync(candidate => candidate.Id == seed.EncounterId);
        Assert.Equal(DungeonEncounterState.Completed, encounter.State);
        Assert.Equal(0, encounter.WipeCount);
        Assert.Equal(1, (await verify.DungeonRuns.SingleAsync()).CurrentEncounterIndex);
        Assert.Empty(await verify.CombatRewardGrants.ToArrayAsync());
    }

    [Fact]
    public async Task ExhaustedStartupRetriesPreserveTerminalVictoryEvidence()
    {
        Seed seed = await SeedAsync(journal: true, terminal: true, reward: true);
        await using ServiceProvider provider = await CreateProviderAsync(failures: int.MaxValue);
        await Assert.ThrowsAsync<AggregateException>(() => CombatStartupRecovery.RecoverAsync(
            provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.NotNull((await verify.ActiveCombatSessions.SingleAsync()).TerminalSnapshotJson);
        Assert.Single(await verify.CombatRewardGrants.ToArrayAsync());
        Assert.Equal(DungeonEncounterState.Active,
            (await verify.DungeonEncounters.SingleAsync(candidate => candidate.Id == seed.EncounterId)).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredLiveCombatIsPreservedWithOrWithoutJournal(bool journal)
    {
        Seed seed = await SeedAsync(journal: journal);
        CombatSession session = CreateSession(seed.SessionId, seed.CharacterId);
        using CombatSessionRegistry registry = new(new FixedTimeProvider(), new NullPublisher(),
            new NullFinalizer(), NullLogger<CombatSessionRegistry>.Instance);
        Assert.True(registry.TryAdd(seed.AccountId, seed.CharacterId, session));
        await using ServiceProvider provider = await CreateProviderAsync(registry: registry);
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(DungeonEncounterState.Active, (await verify.DungeonEncounters.SingleAsync()).State);
        Assert.Equal(journal ? 1 : 0, await verify.ActiveCombatSessions.CountAsync());
        Assert.True(registry.HasActiveCombat(seed.AccountId));
        using IServiceScope scope = provider.CreateScope();
        DungeonService dungeons = scope.ServiceProvider.GetRequiredService<DungeonService>();
        DungeonRunView? current = await dungeons.GetCurrentAsync(seed.AccountId, CancellationToken.None);
        Assert.NotNull(current);
        Assert.Equal(DungeonEncounterState.Active, current.Encounters.Single().State);
        Assert.Equal(DungeonErrorCodes.EncounterActive,
            (await dungeons.PrepareEncounterAsync(seed.AccountId, current.RunId, CancellationToken.None)).ErrorCode);
    }

    [Fact]
    public async Task ReentryHealsMissingCombatAndLeaderCanImmediatelyRetry()
    {
        Seed seed = await SeedAsync(journal: false);
        using CombatSessionRegistry registry = new(new FixedTimeProvider(), new NullPublisher(),
            new NullFinalizer(), NullLogger<CombatSessionRegistry>.Instance);
        await using ServiceProvider provider = await CreateProviderAsync(registry: registry, lifetime: new StartedLifetime());
        using IServiceScope scope = provider.CreateScope();
        DungeonService dungeons = scope.ServiceProvider.GetRequiredService<DungeonService>();
        DungeonRunView? current = await dungeons.GetCurrentAsync(seed.AccountId, CancellationToken.None);
        Assert.NotNull(current);
        Assert.Equal(DungeonEncounterState.Wiped, current.Encounters.Single().State);
        Assert.Equal(1, current.Encounters.Single().WipeCount);
        (DungeonPreparation? preparation, string? error) =
            await dungeons.PrepareEncounterAsync(seed.AccountId, current.RunId, CancellationToken.None);
        Assert.Null(error);
        Assert.NotNull(preparation);
        await using GameDbContext verify = postgres.CreateDbContext();
        DungeonEncounter encounter = await verify.DungeonEncounters.SingleAsync();
        Assert.Equal(DungeonEncounterState.Pending, encounter.State);
        Assert.Equal(1, encounter.WipeCount);
        Assert.Equal(0, (await verify.DungeonRuns.SingleAsync()).CurrentEncounterIndex);
    }

    [Fact]
    public async Task ReentryWithoutRegistryCannotProveRuntimeIsMissing()
    {
        Seed seed = await SeedAsync(journal: false);
        await using ServiceProvider provider = await CreateProviderAsync(lifetime: new StartedLifetime());
        using IServiceScope scope = provider.CreateScope();
        DungeonService dungeons = scope.ServiceProvider.GetRequiredService<DungeonService>();
        DungeonRunView? current = await dungeons.GetCurrentAsync(seed.AccountId, CancellationToken.None);
        Assert.NotNull(current);
        Assert.Equal(DungeonEncounterState.Active, current.Encounters.Single().State);
        Assert.Equal(DungeonErrorCodes.EncounterActive,
            (await dungeons.PrepareEncounterAsync(seed.AccountId, current.RunId, CancellationToken.None)).ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        DungeonEncounter encounter = await verify.DungeonEncounters.SingleAsync();
        Assert.Equal(DungeonEncounterState.Active, encounter.State);
        Assert.Equal(0, encounter.WipeCount);
        Assert.Equal(seed.SessionId, encounter.CombatSessionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReentryPreservesDurableActiveOrTerminalJournalWithoutRegistry(bool terminal)
    {
        Seed seed = await SeedAsync(journal: true, terminal: terminal);
        await using ServiceProvider provider = await CreateProviderAsync(lifetime: new StartedLifetime());
        using IServiceScope scope = provider.CreateScope();
        DungeonRunView? current = await scope.ServiceProvider.GetRequiredService<DungeonService>()
            .GetCurrentAsync(seed.AccountId, CancellationToken.None);
        Assert.NotNull(current);
        Assert.Equal(DungeonEncounterState.Active, current.Encounters.Single().State);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(await verify.ActiveCombatSessions.ToArrayAsync());
        Assert.Equal(0, (await verify.DungeonEncounters.SingleAsync()).WipeCount);
    }

    [Fact]
    public async Task RecoveryAfterAdmissionCannotEraseNewStartBeforeRegistryAdmission()
    {
        Seed seed = await SeedAsync(journal: true);
        StartedLifetime lifetime = new();
        await using ServiceProvider provider = await CreateProviderAsync(lifetime: lifetime);
        using IServiceScope scope = provider.CreateScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<CombatDurabilityService>().RecoverInterruptedAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<DungeonService>().ReconcileOrphanedEncountersAtStartupAsync(CancellationToken.None));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(seed.SessionId, (await verify.ActiveCombatSessions.SingleAsync()).SessionId);
        Assert.Equal(DungeonEncounterState.Active, (await verify.DungeonEncounters.SingleAsync()).State);
    }

    [Fact]
    public async Task FailedRefundTransactionRollsBackAndRetryRefundsExactlyOnce()
    {
        Seed seed = await SeedAsync(journal: true);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), seed.CharacterId,
                "SMALL_HEALING_POTION", 2, Now));
            await setup.SaveChangesAsync();
            CombatDurabilityService durability = new(setup, NullLogger<CombatDurabilityService>.Instance);
            Assert.Null(await durability.ReserveConsumableAsync(seed.AccountId, seed.SessionId,
                "refund-once", "SMALL_HEALING_POTION", GameContentSnapshot.Create(content), Now, CancellationToken.None));
        }
        FailFirstCommit fault = new();
        await using ServiceProvider provider = await CreateProviderAsync(interceptor: fault);
        using (IServiceScope scope = provider.CreateScope())
        {
            await Assert.ThrowsAsync<AggregateException>(() => scope.ServiceProvider
                .GetRequiredService<CombatDurabilityService>().RecoverInterruptedAsync(CancellationToken.None));
        }
        await using (GameDbContext afterFailure = postgres.CreateDbContext())
        {
            Assert.Equal(1, (await afterFailure.CharacterItems.SingleAsync()).Quantity);
            Assert.Single(await afterFailure.ActiveCombatSessions.ToArrayAsync());
            Assert.Single(await afterFailure.CombatConsumableUses.ToArrayAsync());
            Assert.Equal(DungeonEncounterState.Active, (await afterFailure.DungeonEncounters.SingleAsync()).State);
        }
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(2, (await verify.CharacterItems.SingleAsync()).Quantity);
        Assert.Empty(await verify.ActiveCombatSessions.ToArrayAsync());
        Assert.Empty(await verify.CombatConsumableUses.ToArrayAsync());
        Assert.Equal(1, (await verify.DungeonEncounters.SingleAsync()).WipeCount);
    }

    [Fact]
    public async Task MissingParticipantRewardWithoutSnapshotIsPreservedWhileCommittedVictoryAdvances()
    {
        Seed seed = await SeedAsync(journal: true, reward: true);
        Guid secondCharacterId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            Guid secondAccountId = Guid.CreateVersion7();
            setup.Accounts.Add(new Account(secondAccountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(secondCharacterId, secondAccountId, Guid.CreateVersion7(),
                "Pending", $"PENDING{secondCharacterId:N}"[..16], "HUMAN", "MALE", "WARRIOR", Now));
            setup.ActiveCombatSessions.Add(new ActiveCombatSession(seed.SessionId, secondCharacterId, Now, "test", "test"));
            await setup.SaveChangesAsync();
        }
        await using ServiceProvider provider = await CreateProviderAsync();
        await Assert.ThrowsAsync<AggregateException>(() => CombatStartupRecovery.RecoverAsync(
            provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None));
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(secondCharacterId, (await verify.ActiveCombatSessions.SingleAsync()).CharacterId);
        Assert.Single(await verify.CombatRewardGrants.ToArrayAsync());
        Assert.Equal(1, (await verify.DungeonRuns.SingleAsync()).CurrentEncounterIndex);
        Assert.Equal(0, (await verify.DungeonEncounters.SingleAsync(candidate => candidate.Id == seed.EncounterId)).WipeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthoredBossVictoryReproducesLegacyVarchar64AndFinalizesAfterTargetedWidening(bool nextEncounter)
    {
        const string bossId = "HEART_OF_BLIGHTED_GROVE_BOSS_SERDTSE_CHASHCHI_OSKVERNIONNYI_DUKH_L20";
        Assert.Equal(68, bossId.Length);
        const string precedingBossId = "HEART_OF_BLIGHTED_GROVE_BOSS_PROPOVEDNIK_GNILI_SAL_VER_L20";
        string defeatedBossId = nextEncounter ? precedingBossId : bossId;
        Seed seed = await SeedAsync(journal: true, monsterId: defeatedBossId, dungeonId: "HEART_OF_BLIGHTED_GROVE",
            encounterIndex: nextEncounter ? 6 : 7);
        await using ServiceProvider provider = await CreateProviderAsync();
        CombatSessionSnapshot baseSnapshot = CreateSession(seed.SessionId, seed.CharacterId).Snapshot();
        CombatActorSnapshot boss = baseSnapshot.Enemy with { DefinitionId = defeatedBossId, Hp = 0 };
        ContributionSnapshot contribution = new(seed.CharacterId, Now.AddSeconds(-10), null, null, 1, 100, 0, 0, 0);
        CombatSessionSnapshot terminal = baseSnapshot with
        {
            Status = CombatSessionStatus.Victory,
            Enemy = boss,
            Enemies = [boss],
            PlayerContributionEligible = true,
            PlayerContribution = contribution,
            ParticipantContributions = [new ContributionEligibilityResult(true, "qualified", 100, contribution)]
        };
        ICombatSessionFinalizer finalizer = provider.GetRequiredService<ICombatSessionFinalizer>();
        await using GameDbContext schema = postgres.CreateDbContext();
        string legacySchemaSql = nextEncounter
            ? "ALTER TABLE game.dungeon_encounters ALTER COLUMN \"MonsterId\" TYPE character varying(64)"
            : "ALTER TABLE game.combat_reward_grants ALTER COLUMN \"MonsterId\" TYPE character varying(64)";
        await schema.Database.ExecuteSqlRawAsync(legacySchemaSql);
        try
        {
            DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
                finalizer.FinalizeAsync(seed.CharacterId, terminal, CancellationToken.None));
            Assert.Equal("22001", Assert.IsType<PostgresException>(failure.InnerException).SqlState);
            await using GameDbContext afterFailure = postgres.CreateDbContext();
            Assert.NotNull((await afterFailure.ActiveCombatSessions.SingleAsync()).TerminalSnapshotJson);
            Assert.Equal(nextEncounter ? 1 : 0, await afterFailure.CombatRewardGrants.CountAsync());
            Assert.Equal(DungeonEncounterState.Active, (await afterFailure.DungeonEncounters.SingleAsync()).State);
        }
        finally
        {
            await schema.Database.ExecuteSqlRawAsync(nextEncounter
                ? "ALTER TABLE game.dungeon_encounters ALTER COLUMN \"MonsterId\" TYPE character varying(128)"
                : "ALTER TABLE game.combat_reward_grants ALTER COLUMN \"MonsterId\" TYPE character varying(128)");
        }
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);
        await using GameDbContext first = postgres.CreateDbContext();
        CombatRewardGrant grant = await first.CombatRewardGrants.SingleAsync();
        Assert.Equal(defeatedBossId, grant.PrimaryMonsterId);
        long experience = (await first.Characters.SingleAsync()).Experience;
        long gold = (await first.Characters.SingleAsync()).Gold;
        int lootCount = await first.PendingLootItems.CountAsync();
        await CombatStartupRecovery.RecoverAsync(provider.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);
        await using GameDbContext replay = postgres.CreateDbContext();
        Assert.Empty(await replay.ActiveCombatSessions.ToArrayAsync());
        Assert.Single(await replay.CombatRewardGrants.ToArrayAsync());
        Assert.Equal(experience, (await replay.Characters.SingleAsync()).Experience);
        Assert.Equal(gold, (await replay.Characters.SingleAsync()).Gold);
        Assert.Equal(lootCount, await replay.PendingLootItems.CountAsync());
        DungeonEncounter completed = await replay.DungeonEncounters.SingleAsync(candidate => candidate.Id == seed.EncounterId);
        Assert.Equal(defeatedBossId, completed.MonsterId);
        Assert.Equal(DungeonEncounterState.Completed, completed.State);
        Assert.Equal(0, completed.WipeCount);
        Assert.Equal(nextEncounter ? 7 : 8, (await replay.DungeonRuns.SingleAsync()).CurrentEncounterIndex);
        if (nextEncounter)
        {
            DungeonEncounter pending = await replay.DungeonEncounters.SingleAsync(candidate => candidate.EncounterIndex == 7);
            Assert.Equal(bossId, pending.MonsterId);
            Assert.Equal(DungeonEncounterState.Pending, pending.State);
        }
    }

    private async Task<Seed> SeedAsync(bool journal, bool terminal = false, bool reward = false,
        string monsterId = "FOREST_WOLF_L1", string dungeonId = "ANCIENT_MINE", int encounterIndex = 0)
    {
        Seed seed = new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(seed.AccountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        Character character = new(seed.CharacterId, seed.AccountId, Guid.CreateVersion7(),
            "Recovery", $"RECOVERY{seed.CharacterId:N}"[..16], "HUMAN", "MALE", "WARRIOR", Now);
        character.SetLevel(15);
        db.Characters.Add(character);
        db.CharacterVitals.Add(new CharacterVitals(seed.CharacterId, 100, 0, Now, Now));
        db.CharacterLocations.Add(new CharacterLocation(seed.CharacterId, "ANCIENT_MINE", 1, Now));
        DungeonRun run = DungeonRun.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), seed.CharacterId, dungeonId, Now);
        for (int index = 0; index < encounterIndex; index++) run.AdvanceEncounter(Now);
        run.AddMember(seed.CharacterId, Now);
        DungeonEncounter encounter = DungeonEncounter.Create(seed.EncounterId, run.Id, encounterIndex, monsterId, Now);
        encounter.Activate(seed.SessionId);
        encounter.Members.Add(DungeonEncounterMember.Create(encounter.Id, seed.CharacterId, Now));
        run.Encounters.Add(encounter);
        db.DungeonRuns.Add(run);
        if (journal)
        {
            ActiveCombatSession state = new(seed.SessionId, seed.CharacterId, Now, "test", "test");
            if (terminal)
                state.RecordTerminalSnapshot(JsonSerializer.Serialize(CreateSession(seed.SessionId, seed.CharacterId).Snapshot() with
                {
                    Status = CombatSessionStatus.Victory,
                    PlayerContributionEligible = false,
                    PlayerContribution = new ContributionSnapshot(seed.CharacterId, Now, null, null, 0, 0, 0, 0, 0),
                    ParticipantContributions = null
                }));
            db.ActiveCombatSessions.Add(state);
        }
        if (reward)
            db.CombatRewardGrants.Add(new CombatRewardGrant(seed.SessionId, seed.CharacterId, "FOREST_WOLF_L1", 5, 3, Now));
        await db.SaveChangesAsync();
        return seed;
    }

    private async Task<ServiceProvider> CreateProviderAsync(
        int failures = 0, CombatSessionRegistry? registry = null, IHostApplicationLifetime? lifetime = null,
        IInterceptor? interceptor = null)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped<GameDbContext>(_ => interceptor is null ? postgres.CreateDbContext() : new GameDbContext(
            new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(postgres.ConnectionString,
                options => options.EnableRetryOnFailure()).AddInterceptors(interceptor).Options));
        services.AddSingleton<IContentSnapshotProvider>(new StaticContentSnapshotProvider(content));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        services.AddScoped<PartyService>();
        services.AddScoped<DungeonService>();
        services.AddScoped<CombatDurabilityService>();
        services.AddScoped<CharacterAbilityCooldownStore>();
        services.AddScoped<CharacterDerivedStateService>();
        services.AddScoped<InventoryEquipmentService>();
        services.AddScoped<CombatRewardService>();
        services.AddSingleton<IGameRandomFactory>(new FixedRandomFactory());
        if (registry is not null) services.AddSingleton(registry);
        if (lifetime is not null) services.AddSingleton(lifetime);
        services.AddSingleton<ICombatSessionFinalizer>(provider => new FailingFinalizer(
            new CombatSessionFinalizer(provider.GetRequiredService<IServiceScopeFactory>()), failures));
        return services.BuildServiceProvider();
    }

    private static CombatSession CreateSession(Guid sessionId, Guid characterId)
    {
        CombatStats stats = new(1, 100, 0, 0, 1, 0, 0, 0, 0, 10, 0);
        CombatParticipantDefinition player = new(new CombatActorState(characterId, 100, 100, 100, 0, stats),
            CombatActorKind.Player, "WARRIOR", "Recovery", "RAGE", new AutoAttackProfile(TimeSpan.FromSeconds(2), 0, 1, 0), new HashSet<string>());
        CombatParticipantDefinition enemy = new(new CombatActorState(Guid.CreateVersion7(), 100, 100, 0, 0, stats),
            CombatActorKind.Monster, "FOREST_WOLF_L1", "Wolf", "NONE", new AutoAttackProfile(TimeSpan.FromSeconds(2), 0, 1, 0), new HashSet<string>());
        return new CombatSession(sessionId, player, enemy, new Dictionary<string, Elyndor.Core.Combat.Abilities.AbilityDefinition>(),
            new MonsterAiProfile("test", []), ResolvedTalentModifiers.Empty, new SequenceGameRandom([0.99m]), Now);
    }

    private sealed record Seed(Guid AccountId, Guid CharacterId, Guid SessionId, Guid EncounterId);
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            base.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
    private sealed class NullPublisher : ICombatUpdatePublisher
    {
        public Task PublishAsync(Guid accountId, CombatOperationResult update, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class NullFinalizer : ICombatSessionFinalizer
    {
        public Task<CombatRewardApplicationResult?> FinalizeAsync(Guid characterId, CombatSessionSnapshot snapshot,
            CancellationToken cancellationToken) => Task.FromResult<CombatRewardApplicationResult?>(null);
    }
    private sealed class FailingFinalizer(ICombatSessionFinalizer inner, int failures) : ICombatSessionFinalizer
    {
        private int _remainingFailures = failures;
        public Task<CombatRewardApplicationResult?> FinalizeAsync(Guid characterId, CombatSessionSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            if (_remainingFailures-- > 0) throw new InvalidOperationException("Injected finalization failure.");
            return inner.FinalizeAsync(characterId, snapshot, cancellationToken);
        }
    }
    private sealed class StartedLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => new(true);
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
    private sealed class FailFirstCommit : DbTransactionInterceptor
    {
        private bool _failed;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new InvalidOperationException("Injected failure before refund commit.");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class FixedRandomFactory : IGameRandomFactory
    {
        public IGameRandom Create() => new SequenceGameRandom(Enumerable.Repeat(0.99m, 512).ToArray());
    }
}
