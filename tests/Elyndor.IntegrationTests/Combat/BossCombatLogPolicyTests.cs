using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Server.Combat;

namespace Elyndor.IntegrationTests.Combat;

public sealed class BossCombatLogPolicyTests
{
    [Theory]
    [InlineData("ARCHON_OF_THE_DEAD_STAR", "Обычный босс")]
    [InlineData("WORLD_BOSS_ASH_ARCHON_L30", "Архон Пепла")]
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
