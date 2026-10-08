using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Server.Combat;

namespace Elyndor.IntegrationTests.Combat;

public sealed class CombatLogRetentionPolicyTests
{
    [Theory]
    [InlineData(MonsterRank.Normal, CombatLogRetentionPolicy.NormalArchiveEvents)]
    [InlineData(MonsterRank.Elite, CombatLogRetentionPolicy.NormalArchiveEvents)]
    [InlineData(MonsterRank.Boss, CombatLogRetentionPolicy.BossOrDungeonArchiveEvents)]
    public void RetentionDependsOnEncounterRank(MonsterRank rank, int expected)
    {
        CombatSessionSnapshot snapshot = Snapshot(rank);
        Assert.Equal(expected, CombatLogRetentionPolicy.ArchiveLimit(snapshot, null));
    }

    [Fact]
    public void ExportLimitIsIndependentFromArchivedCombatAndStatistics()
    {
        Assert.Equal(10_000, CombatLogRetentionPolicy.StandardExportEvents);
        Assert.Equal(5_000, CombatLogRetentionPolicy.NormalArchiveEvents);
        Assert.Equal(10_000, CombatLogRetentionPolicy.BossOrDungeonArchiveEvents);
        Assert.True(CombatLogRetentionPolicy.FullExportMaxBytes > 30 * 1024 * 1024);
        Assert.Equal(500, CombatSession.RetainedEventLimit);
    }

    [Fact]
    public void OrdinaryEncountersCanBeArchivedWithoutEnablingBossAutoReports()
    {
        CombatSessionSnapshot normal = Snapshot(MonsterRank.Normal);
        Assert.False(BossCombatLogPolicy.TryResolve(normal, out _));
        Assert.True(BossCombatLogPolicy.TryResolveArchive(normal, out BossCombatLogTarget target));
        Assert.False(target.IsBoss);
    }

    [Fact]
    public void FullLogIsOptInAndDeduplicatedAcrossPartyPublishers()
    {
        DateTimeOffset now = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
        Guid developerAccount = Guid.NewGuid();
        Guid partyAccount = Guid.NewGuid();
        CombatSessionSnapshot session = Snapshot(MonsterRank.Boss);
        CombatEvent first = new(
            CombatEventType.DamageDealt,
            now,
            session.Player.ActorId,
            "TEST_ATTACK",
            12,
            SourceActorId: session.Player.ActorId,
            TargetActorId: session.Enemy.ActorId)
        { Sequence = 1 };
        CombatEvent second = first with { Sequence = 2, Amount = 8 };

        FullCombatLogArchive.Capture(developerAccount, session, [first], now);
        Assert.Null(FullCombatLogArchive.Read(session.SessionId, now));

        FullCombatLogArchive.Arm(developerAccount, now);
        FullCombatLogArchive.Capture(developerAccount, session, [first, second], now);
        FullCombatLogArchive.Capture(partyAccount, session, [first, second], now);

        FullCombatLogSnapshot? full = FullCombatLogArchive.Read(session.SessionId, now);
        Assert.NotNull(full);
        Assert.Collection(full.Events,
            firstEvent => Assert.Equal(1L, firstEvent.Sequence),
            secondEvent => Assert.Equal(2L, secondEvent.Sequence));
        Assert.False(full.SizeLimitReached);
        Assert.Equal(1, full.FirstCapturedSequence);
    }

    private static CombatSessionSnapshot Snapshot(MonsterRank rank)
    {
        DateTimeOffset now = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
        CombatActorSnapshot player = Actor("PLAYER", "Tester", null);
        CombatActorSnapshot enemy = Actor("ENEMY", "Enemy", rank);
        return new CombatSessionSnapshot(Guid.NewGuid(), 0, CombatSessionStatus.Active, now, player, enemy);
    }

    private static CombatActorSnapshot Actor(string id, string name, MonsterRank? rank) =>
        new(
            Guid.NewGuid(),
            id == "PLAYER" ? CombatActorKind.Player : CombatActorKind.Monster,
            id,
            name,
            100,
            100,
            "MANA",
            100,
            100,
            false,
            null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            [],
            MonsterRank: rank);
}
