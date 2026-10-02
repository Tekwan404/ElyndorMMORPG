using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Core.Quests;
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
public sealed class AfkFarmCatchUpTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
    private const string ForestId = "AFK_CATCHUP_FOREST";
    private const string WolfId = "AFK_CATCHUP_WOLF";

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FourHourOfflineSessionCatchesUpAllIntervalsInOneProcessCall()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext);
        GameContentPackage content = CreateContent();
        AfkFarmService start = CreateService(dbContext, content);
        AfkFarmMutationResult started = await start.StartAsync(
            accountId,
            ForestId,
            null,
            TimeSpan.FromHours(4),
            CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorCode);

        AfkFarmProgressService progress = new(
            dbContext,
            new StaticContentSnapshotProvider(content),
            new OffsetTimeProvider(Now.AddHours(4)));

        AfkFarmProgressResult result = await progress.ProcessAsync(accountId, CancellationToken.None);

        Assert.True(result.Processed);
        Assert.NotNull(result.Session);
        Assert.Equal(AfkFarmStatus.Completed, result.Session.Status);
        Assert.Equal(Now.AddHours(4), result.Session.LastProcessedAtUtc);

        AfkFarmIntervalGrant[] grants = await dbContext.AfkFarmIntervalGrants
            .OrderBy(grant => grant.IntervalIndex)
            .ToArrayAsync();
        Assert.Equal(16, grants.Length);
        Assert.Equal(Enumerable.Range(0, 16), grants.Select(grant => grant.IntervalIndex));
        Assert.Equal(
            grants.Sum(grant => grant.GoldEarned),
            await dbContext.Characters.Select(character => character.Gold).SingleAsync());
    }

    [Fact]
    public async Task AutomaticHuntAdvancesAcceptedKillQuestAndReplayDoesNotDuplicateProgress()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext);
        Guid characterId = await dbContext.Characters
            .Where(character => character.AccountId == accountId)
            .Select(character => character.Id)
            .SingleAsync();

        GameContentPackage content = CreateContent() with
        {
            Quests =
            [
                new QuestDefinition(
                    "AFK_KILL_QUEST",
                    "AFK hunt",
                    "Kill wolves while away.",
                    QuestType.Side,
                    1,
                    ForestId,
                    [
                        new QuestObjectiveDefinition(
                            "KILL_WOLVES",
                            QuestObjectiveType.KillMonster,
                            WolfId,
                            3)
                    ])
            ]
        };
        dbContext.CharacterQuestStates.Add(
            new CharacterQuestState(characterId, "AFK_KILL_QUEST", Now));
        await dbContext.SaveChangesAsync();

        AfkFarmService start = CreateService(dbContext, content);
        AfkFarmMutationResult started = await start.StartAsync(
            accountId,
            ForestId,
            WolfId,
            TimeSpan.FromMinutes(15),
            CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorCode);

        AfkFarmProgressService progress = new(
            dbContext,
            new StaticContentSnapshotProvider(content),
            new OffsetTimeProvider(Now.AddMinutes(15)));

        AfkFarmProgressResult first = await progress.ProcessAsync(
            accountId,
            CancellationToken.None);
        Assert.True(first.Processed);

        CharacterQuestState state = await dbContext.CharacterQuestStates
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.CharacterId == characterId
                && candidate.QuestId == "AFK_KILL_QUEST");
        Dictionary<string, int> counts = QuestProgressJson.Read(state.ProgressJson);
        Assert.Equal(3, counts["KILL_WOLVES"]);
        Assert.Equal(QuestStateStatuses.ReadyToClaim, state.Status);

        AfkFarmProgressResult replay = await progress.ProcessAsync(
            accountId,
            CancellationToken.None);
        Assert.False(replay.Processed);

        CharacterQuestState afterReplay = await dbContext.CharacterQuestStates
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.CharacterId == characterId
                && candidate.QuestId == "AFK_KILL_QUEST");
        Assert.Equal(
            counts,
            QuestProgressJson.Read(afterReplay.ProgressJson));
        Assert.Equal(QuestStateStatuses.ReadyToClaim, afterReplay.Status);
    }

    [Fact]
    public async Task PartialOfflineSessionCatchesUpOnlyFullyElapsedIntervals()
    {
        await using GameDbContext dbContext = postgres.CreateDbContext();
        Guid accountId = await SeedCharacterAsync(dbContext);
        GameContentPackage content = CreateContent();
        AfkFarmService start = CreateService(dbContext, content);
        AfkFarmMutationResult started = await start.StartAsync(
            accountId,
            ForestId,
            null,
            TimeSpan.FromHours(4),
            CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorCode);

        AfkFarmProgressService progress = new(
            dbContext,
            new StaticContentSnapshotProvider(content),
            new OffsetTimeProvider(Now.AddMinutes(46)));

        AfkFarmProgressResult result = await progress.ProcessAsync(accountId, CancellationToken.None);

        Assert.True(result.Processed);
        Assert.NotNull(result.Session);
        Assert.Equal(AfkFarmStatus.Active, result.Session.Status);
        Assert.Equal(Now.AddMinutes(45), result.Session.LastProcessedAtUtc);
        Assert.Equal(3, await dbContext.AfkFarmIntervalGrants.CountAsync());
    }

    private static AfkFarmService CreateService(GameDbContext dbContext, GameContentPackage content)
    {
        StaticContentSnapshotProvider provider = new(content);
        CharacterDerivedStateService derived = new(dbContext, provider, null);
        CharacterOperationGuard guard = new(new NoCombatActivity());
        return new AfkFarmService(dbContext, provider, derived, guard, new FixedTimeProvider());
    }

    private static GameContentPackage CreateContent()
    {
        LocationDefinition location = new(
            ForestId,
            "AFK catch-up forest",
            "ADVENTURE",
            1,
            [],
            [new LocationEncounterDefinition(WolfId, 1m)],
            MinimumLevel: 1,
            MaximumLevel: 60,
            AllowAfk: true);
        MonsterDefinition wolf = new(
            WolfId,
            "Wolf",
            MonsterRank.Normal,
            1,
            50,
            new CombatStats(1, 90, 0, 5, 1.5m, 5, 0, 0, 0, 5, 0),
            TimeSpan.FromSeconds(2),
            5,
            [],
            "AFK_CATCHUP_AI",
            XpReward: 10,
            GoldRewardMin: 2,
            GoldRewardMax: 4);

        GameContentPackage baseContent = PhaseTwoTestContent.Create(Now, [], [location]);
        return baseContent with
        {
            Monsters = [wolf],
            MonsterAiProfiles = [new("AFK_CATCHUP_AI", [])],
            AfkFarm = new AfkFarmRewardProfile(900, 0.7m, 0.7m, 0.7m),
            LevelProgression = new LevelProgressionDefinition("AFK_CATCHUP", 60, 100, 1.1m),
            ClassProfiles = baseContent.ClassProfiles!
                .Select(profile => profile with
                {
                    CombatAutoAttack = new AutoAttackProfile(
                        TimeSpan.FromSeconds(2), 2, 0.5m, 0, 1, 2)
                })
                .ToArray()
        };
    }

    private static async Task<Guid> SeedCharacterAsync(GameDbContext dbContext)
    {
        Guid accountId = Guid.CreateVersion7();
        Character character = new(
            Guid.CreateVersion7(),
            accountId,
            Guid.CreateVersion7(),
            "AfkCatchUpTester",
            "AFKCATCHUPTESTER",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        dbContext.Accounts.Add(new Account(accountId, 99112234, Now));
        dbContext.Characters.Add(character);
        dbContext.CharacterVitals.Add(new CharacterVitals(character.Id, 100, 0, Now, Now));
        dbContext.CharacterLocations.Add(new CharacterLocation(character.Id, ForestId, 1, Now));
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

    private sealed class OffsetTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
