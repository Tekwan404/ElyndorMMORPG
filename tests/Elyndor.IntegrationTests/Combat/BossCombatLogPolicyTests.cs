using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Server.Combat;

namespace Elyndor.IntegrationTests.Combat;

public sealed class BossCombatLogPolicyTests
{
    [Fact]
    public void ArchiveDropsZeroCombatRegenNoise()
    {
        CombatEvent zeroRegen = new(
            CombatEventType.ResourceChanged,
            new DateTimeOffset(2026, 10, 3, 6, 0, 0, TimeSpan.Zero),
            Guid.CreateVersion7(),
            "COMBAT_REGEN",
            0);

        Assert.False(BossCombatLogArchive.ShouldArchiveEvent(zeroRegen));
        Assert.False(BossCombatLogArchive.ShouldArchiveEvent(
            zeroRegen with { Amount = 0.001m }));
        Assert.False(BossCombatLogArchive.ShouldArchiveEvent(
            zeroRegen with { Amount = 0.004m }));
        Assert.True(BossCombatLogArchive.ShouldArchiveEvent(
            zeroRegen with { Amount = 0.01m }));
        Assert.True(BossCombatLogArchive.ShouldArchiveEvent(
            zeroRegen with { Amount = 1 }));
        Assert.True(BossCombatLogArchive.ShouldArchiveEvent(
            zeroRegen with { DefinitionId = "OTHER_REGEN", Amount = 0.001m }));
    }

    [Fact]
    public void AbilitySummaryUsesCumulativeSessionStatistics()
    {
        CombatSessionSnapshot snapshot = Snapshot(
            Monster("WORLD_BOSS_ASH_ARCHON_L60", "Архон Пепла", MonsterRank.Boss))
            with
            {
                Statistics = new CombatSessionStatisticsSnapshot(
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["MAGE_IGNITE"] = 17,
                        ["MAGE_FIREBALL"] = 4
                    })
            };
        CombatEvent[] retainedEvents =
        [
            new CombatEvent(
                CombatEventType.AbilityUsed,
                new DateTimeOffset(2026, 10, 3, 6, 5, 0, TimeSpan.Zero),
                snapshot.Player.ActorId,
                "MAGE_IGNITE",
                SourceActorId: snapshot.Player.ActorId,
                TargetActorId: snapshot.Enemy.ActorId)
            {
                Sequence = 1499
            }
        ];

        string summary = BossCombatLogArchive.FormatAbilitySummary(
            snapshot,
            retainedEvents);

        Assert.Equal("MAGE_IGNITE×17, MAGE_FIREBALL×4", summary);
    }

    [Theory]
    [InlineData("ARCHON_OF_THE_DEAD_STAR", "Обычный босс")]
    [InlineData("WORLD_BOSS_ASH_ARCHON_L60", "Архон Пепла")]
    public void TryResolveAcceptsBossRank(string definitionId, string name)
    {
        CombatSessionSnapshot snapshot = Snapshot(
            Monster(definitionId, name, MonsterRank.Boss));

        bool eligible = BossCombatLogPolicy.TryResolve(
            snapshot,
            out BossCombatLogTarget target);

        Assert.True(eligible);
        Assert.Equal(definitionId, target.DefinitionId);
        Assert.Equal(name, target.DisplayName);
        Assert.False(target.IsTrainingDummy);
    }

    [Fact]
    public void TryResolveKeepsTrainingDummyDiagnostics()
    {
        CombatSessionSnapshot snapshot = Snapshot(
            Monster("TRAINING_DUMMY", "Манекен", null));

        bool eligible = BossCombatLogPolicy.TryResolve(
            snapshot,
            out BossCombatLogTarget target);

        Assert.True(eligible);
        Assert.True(target.IsTrainingDummy);
        Assert.Equal("Тренировочный манекен", target.DisplayName);
    }

    [Fact]
    public void TryResolveRejectsNormalMonster()
    {
        CombatSessionSnapshot snapshot = Snapshot(
            Monster("WHISPERING_FOREST_WOLF", "Волк", MonsterRank.Normal));

        Assert.False(BossCombatLogPolicy.TryResolve(snapshot, out _));
    }

    private static CombatSessionSnapshot Snapshot(CombatActorSnapshot enemy) =>
        new(
            Guid.CreateVersion7(),
            0,
            CombatSessionStatus.Active,
            new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero),
            Player(),
            enemy);

    private static CombatActorSnapshot Player() =>
        new(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "PLAYER",
            "Tester",
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
            []);

    private static CombatActorSnapshot Monster(
        string definitionId,
        string name,
        MonsterRank? rank) =>
        new(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            definitionId,
            name,
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
            [],
            MonsterRank: rank);
}
