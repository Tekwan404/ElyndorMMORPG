using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatDurabilityServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 9, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InterruptedCombatRefundsJournaledConsumable()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.ActiveCombatSessions.Add(new ActiveCombatSession(
                sessionId,
                characterId,
                Now,
                "0.13.3",
                "0.11.0"));
            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "SMALL_HEALING_POTION",
                2,
                Now));
            await setup.SaveChangesAsync();
        }

        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        GameContentSnapshot snapshot = GameContentSnapshot.Create(package);

        await using GameDbContext context = postgres.CreateDbContext();
        CombatDurabilityService service = new(
            context,
            NullLogger<CombatDurabilityService>.Instance);

        string? reserveError = await service.ReserveConsumableAsync(
            accountId,
            sessionId,
            "combat-potion-1",
            "SMALL_HEALING_POTION",
            snapshot,
            Now,
            CancellationToken.None);

        Assert.Null(reserveError);
        Assert.Equal(
            1,
            await context.CharacterItems
                .Where(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == "SMALL_HEALING_POTION")
                .SumAsync(item => item.Quantity));
        Assert.Single(await context.CombatConsumableUses.ToArrayAsync());

        int recovered = await service.RecoverInterruptedAsync(
            CancellationToken.None);

        Assert.Equal(1, recovered);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            2,
            await verify.CharacterItems
                .Where(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == "SMALL_HEALING_POTION")
                .SumAsync(item => item.Quantity));
        Assert.Empty(await verify.ActiveCombatSessions.ToArrayAsync());
        Assert.Empty(await verify.CombatConsumableUses.ToArrayAsync());
    }

    [Fact]
    public async Task LateParticipantDurabilityJournalIsCreatedIdempotently()
    {
        (_, Guid leaderCharacterId) = await CreateCharacterAsync("RecoveryLeader");
        (_, Guid lateCharacterId) = await CreateCharacterAsync("RecoveryLate");
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.ActiveCombatSessions.Add(new ActiveCombatSession(
                sessionId,
                leaderCharacterId,
                Now,
                "0.13.3",
                "0.11.0"));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CombatDurabilityService service = new(
            context,
            NullLogger<CombatDurabilityService>.Instance);

        CombatDurabilityBeginResult first = await service.BeginParticipantAsync(
            lateCharacterId,
            ActiveSnapshot(sessionId),
            CancellationToken.None);
        CombatDurabilityBeginResult replay = await service.BeginParticipantAsync(
            lateCharacterId,
            ActiveSnapshot(sessionId),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(first.Created);
        Assert.True(replay.Succeeded);
        Assert.False(replay.Created);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            2,
            await verify.ActiveCombatSessions
                .CountAsync(state => state.SessionId == sessionId));
    }

    [Fact]
    public async Task CompletingOneParticipantKeepsTheOtherParticipantJournal()
    {
        (_, Guid firstCharacterId) = await CreateCharacterAsync("CompletionFirst");
        (_, Guid secondCharacterId) = await CreateCharacterAsync("CompletionSecond");
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.ActiveCombatSessions.AddRange(
                new ActiveCombatSession(
                    sessionId,
                    firstCharacterId,
                    Now,
                    "0.13.3",
                    "0.11.0"),
                new ActiveCombatSession(
                    sessionId,
                    secondCharacterId,
                    Now,
                    "0.13.3",
                    "0.11.0"));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CombatDurabilityService service = new(
            context,
            NullLogger<CombatDurabilityService>.Instance);

        await service.CompleteParticipantAsync(
            sessionId,
            firstCharacterId,
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.DoesNotContain(
            await verify.ActiveCombatSessions.ToArrayAsync(),
            state => state.CharacterId == firstCharacterId);
        Assert.Contains(
            await verify.ActiveCombatSessions.ToArrayAsync(),
            state => state.SessionId == sessionId
                && state.CharacterId == secondCharacterId);
    }

    [Fact]
    public async Task TerminalSnapshotIsRecordedForEveryParticipantBeforeRewardsAreFinalized()
    {
        (_, Guid firstCharacterId) = await CreateCharacterAsync("TerminalFirst");
        (_, Guid secondCharacterId) = await CreateCharacterAsync("TerminalSecond");
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.ActiveCombatSessions.AddRange(
                new ActiveCombatSession(
                    sessionId,
                    firstCharacterId,
                    Now,
                    "0.13.3",
                    "0.11.0"),
                new ActiveCombatSession(
                    sessionId,
                    secondCharacterId,
                    Now,
                    "0.13.3",
                    "0.11.0"));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CombatDurabilityService service = new(
            context,
            NullLogger<CombatDurabilityService>.Instance);
        CombatSessionSnapshot terminal = ActiveSnapshot(sessionId) with
        {
            Status = CombatSessionStatus.Victory
        };

        await service.RecordTerminalSnapshotAsync(
            sessionId,
            terminal,
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        ActiveCombatSession[] persisted = await verify.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId)
            .ToArrayAsync();
        Assert.Equal(2, persisted.Length);
        Assert.All(persisted, state => Assert.False(string.IsNullOrWhiteSpace(state.TerminalSnapshotJson)));
    }

    [Fact]
    public async Task RecoveryFinalizesEveryParticipantFromTheDurableTerminalSnapshot()
    {
        (_, Guid firstCharacterId) = await CreateCharacterAsync("RecoveryFirst");
        (_, Guid secondCharacterId) = await CreateCharacterAsync("RecoverySecond");
        Guid firstActorId = Guid.CreateVersion7();
        Guid secondActorId = Guid.CreateVersion7();
        Guid sessionId = Guid.CreateVersion7();
        CombatActorSnapshot basePlayer = ActiveSnapshot(sessionId).Player;
        CombatSessionSnapshot terminal = ActiveSnapshot(sessionId) with
        {
            Status = CombatSessionStatus.Victory,
            Player = basePlayer with { ActorId = firstActorId },
            Players =
            [
                basePlayer with { ActorId = firstActorId },
                basePlayer with { ActorId = secondActorId }
            ],
            ParticipantRoster =
            [
                new(
                    Guid.CreateVersion7(),
                    firstCharacterId,
                    firstActorId,
                    CombatParticipantStatus.Active,
                    Now,
                    Now,
                    null,
                    null),
                new(
                    Guid.CreateVersion7(),
                    secondCharacterId,
                    secondActorId,
                    CombatParticipantStatus.Active,
                    Now,
                    Now,
                    null,
                    null)
            ]
        };
        string snapshotJson = JsonSerializer.Serialize(terminal);

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            ActiveCombatSession first = new(
                sessionId,
                firstCharacterId,
                Now,
                "0.13.3",
                "0.11.0");
            ActiveCombatSession second = new(
                sessionId,
                secondCharacterId,
                Now,
                "0.13.3",
                "0.11.0");
            first.RecordTerminalSnapshot(snapshotJson);
            second.RecordTerminalSnapshot(snapshotJson);
            setup.ActiveCombatSessions.AddRange(first, second);
            await setup.SaveChangesAsync();
        }

        ServiceCollection services = new();
        RecoveryFinalizerCalls calls = new();
        services.AddLogging();
        services.AddSingleton(calls);
        services.AddScoped<GameDbContext>(_ => postgres.CreateDbContext());
        services.AddScoped<CombatDurabilityService>();
        services.AddScoped<ICombatSessionFinalizer, RecordingRecoveryFinalizer>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<ICombatSessionFinalizer>());
        CombatDurabilityService service = scope.ServiceProvider
            .GetRequiredService<CombatDurabilityService>();

        int recovered = await service.RecoverInterruptedAsync(CancellationToken.None);

        Assert.Equal(2, recovered);
        Assert.Equal(
            [firstCharacterId, secondCharacterId],
            calls.Values.Select(call => call.CharacterId).ToArray());
        Assert.Equal(
            [firstActorId, secondActorId],
            calls.Values.Select(call => call.Snapshot.Player.ActorId).ToArray());

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId)
            .ToArrayAsync());
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(
        string name = "Recovery")
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            name,
            $"{name.ToUpperInvariant()}{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static CombatSessionSnapshot ActiveSnapshot(Guid sessionId) =>
        new(
            sessionId,
            0,
            CombatSessionStatus.Active,
            Now,
            new CombatActorSnapshot(
                Guid.CreateVersion7(),
                CombatActorKind.Player,
                "WARRIOR",
                "Leader",
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
                []),
            new CombatActorSnapshot(
                Guid.CreateVersion7(),
                CombatActorKind.Monster,
                "DEEP_WOLF_L6",
                "Wolf",
                100,
                100,
                "NONE",
                0,
                0,
                false,
                null,
                new Dictionary<string, DateTimeOffset>(),
                new HashSet<string>(),
                [],
                []));

    private sealed class RecoveryFinalizerCalls
    {
        public List<RecoveryFinalizerCall> Values { get; } = [];
    }

    private sealed record RecoveryFinalizerCall(
        Guid CharacterId,
        CombatSessionSnapshot Snapshot);

    private sealed class RecordingRecoveryFinalizer(
        GameDbContext dbContext,
        RecoveryFinalizerCalls calls) : ICombatSessionFinalizer
    {
        public async Task<CombatRewardApplicationResult?> FinalizeAsync(
            Guid characterId,
            CombatSessionSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            calls.Values.Add(new(characterId, snapshot));
            await dbContext.ActiveCombatSessions
                .Where(state => state.SessionId == snapshot.SessionId
                    && state.CharacterId == characterId)
                .ExecuteDeleteAsync(cancellationToken);
            return null;
        }
    }
}
