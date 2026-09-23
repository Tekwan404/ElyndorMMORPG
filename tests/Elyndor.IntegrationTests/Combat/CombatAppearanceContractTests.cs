using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Server.Combat;

namespace Elyndor.IntegrationTests.Combat;

public sealed class CombatAppearanceContractTests
{
    [Fact]
    public void ResumeResponsePreservesEachPlayersAppearanceAndEnemyRank()
    {
        CombatActorSnapshot player = Actor(CombatActorKind.Player, "WARRIOR", "MALE");
        CombatActorSnapshot ally = Actor(CombatActorKind.Player, "MAGE", "FEMALE");
        CombatActorSnapshot enemy = Actor(CombatActorKind.Monster, "BOSS", null) with
        {
            MonsterRank = MonsterRank.Boss,
            CurrentAggroTargetActorId = ally.ActorId
        };
        CombatSessionSnapshot snapshot = new(
            Guid.NewGuid(), 2, CombatSessionStatus.Active, DateTimeOffset.UtcNow,
            player, enemy, Players: [player, ally]);
        GameContentPackage content = new("1", "1", DateTimeOffset.UnixEpoch, [], []);

        var response = CombatContractMapper.ToResponse(
            CombatOperationResult.FromSnapshot(snapshot), content).Snapshot!;

        Assert.Equal("MALE", response.Player.GenderId);
        Assert.Equal("FEMALE", response.Players!.Single(item => item.ActorId == ally.ActorId).GenderId);
        Assert.Equal("Boss", response.Enemy.MonsterRank);
        Assert.Equal(ally.ActorId, response.Enemy.CurrentAggroTargetActorId);
    }

    private static CombatActorSnapshot Actor(CombatActorKind kind, string definitionId, string? genderId) =>
        new(Guid.NewGuid(), kind, definitionId, definitionId,
            100, 100, "NONE", 0, 0, false, null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(), [], [], GenderId: genderId);
}
