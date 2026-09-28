using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaTestRegistryTests
{
    [Fact]
    public void NearbyAccountsGetTheSameMatchAndCannotQueueTwice()
    {
        var registry = new ArenaTestRegistry(TimeProvider.System, () => new SeededGameRandom(42));
        var first = Entrant(30);
        var second = Entrant(32);
        Assert.Equal(ArenaTestStatus.Searching, registry.Join(first).Status);
        Assert.Equal(ArenaTestStatus.Searching, registry.Join(first).Status);
        var matched = registry.Join(second);
        Assert.Equal(ArenaTestStatus.Active, matched.Status);
        Assert.Equal(matched.MatchId, registry.Get(first.Fighter.AccountId)!.MatchId);
        Assert.Equal(CombatActorKind.Player, registry.Get(first.Fighter.AccountId)!.Battle!.Player.Kind);
        Assert.Equal(second.Fighter.CharacterId, registry.Get(first.Fighter.AccountId)!.Battle!.Enemy.ActorId);
        Assert.Equal(2, registry.ActiveParticipantCount);
    }

    [Fact]
    public void FarLevelsWaitForCompatibleOpponent()
    {
        var registry = new ArenaTestRegistry(TimeProvider.System, () => new SeededGameRandom(42));
        var first = Entrant(10);
        var distant = Entrant(30);
        registry.Join(first);
        Assert.Equal(ArenaTestStatus.Searching, registry.Join(distant).Status);
        Assert.Equal(ArenaTestStatus.Searching, registry.Get(first.Fighter.AccountId)!.Status);
    }

    [Fact]
    public void CancellingQueueDoesNotCreateOrCompleteAMatch()
    {
        var registry = new ArenaTestRegistry(TimeProvider.System, () => new SeededGameRandom(42));
        var first = Entrant(30);
        registry.Join(first);
        Assert.True(registry.Leave(first.Fighter.AccountId));
        Assert.Null(registry.Get(first.Fighter.AccountId));
        Assert.Equal(0, registry.ActiveParticipantCount);
    }

    [Fact]
    public void OnlyTheOwnerCanCommandTheirMatchAndReconnectKeepsState()
    {
        var registry = new ArenaTestRegistry(TimeProvider.System, () => new SeededGameRandom(42));
        var first = Entrant(30);
        var second = Entrant(30);
        registry.Join(first);
        Guid matchId = registry.Join(second).MatchId!.Value;
        Assert.False(registry.UseAbility(Guid.NewGuid(), matchId, "invalid", "STRIKE",
            second.Fighter.Actor.ActorId).Succeeded);
        Assert.False(registry.UseAbility(first.Fighter.AccountId, Guid.NewGuid(), "wrong-match",
            "STRIKE", second.Fighter.Actor.ActorId).Succeeded);
        var result = registry.UseAbility(first.Fighter.AccountId, matchId, "hit", "STRIKE",
            second.Fighter.Actor.ActorId);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(matchId, registry.Get(first.Fighter.AccountId)!.MatchId);
        Assert.True(registry.Get(second.Fighter.AccountId)!.PlayerHp < 100);
    }

    [Fact]
    public void SurrenderEndsBothViewsAndDismissOnlyRemovesTheOwnResult()
    {
        var registry = new ArenaTestRegistry(TimeProvider.System, () => new SeededGameRandom(42));
        var first = Entrant(30);
        var second = Entrant(30);
        registry.Join(first);
        registry.Join(second);
        Assert.True(registry.Surrender(first.Fighter.AccountId));
        Assert.False(registry.Surrender(first.Fighter.AccountId));
        Assert.Equal(ArenaMatchOutcome.WinnerB, registry.Get(second.Fighter.AccountId)!.Outcome);
        Assert.True(registry.Leave(first.Fighter.AccountId));
        Assert.Null(registry.Get(first.Fighter.AccountId));
        Assert.NotNull(registry.Get(second.Fighter.AccountId));
    }

    private static ArenaTestEntrant Entrant(int level)
    {
        Guid account = Guid.NewGuid();
        Guid character = Guid.NewGuid();
        var strike = new AbilityDefinition("STRIKE", AbilityType.Instant, AbilityTargetType.SingleEnemy,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "Physical",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10, DamageType.Physical,
                CanMiss: false, CanCrit: false, CanDodge: false)]);
        var fighter = new ArenaFighter(account, character,
            new CombatActorState(character, 100, 100, 100, 100, CombatStats.Default),
            new Dictionary<string, AbilityDefinition> { [strike.Id] = strike },
            new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0));
        return new ArenaTestEntrant(fighter, level, $"Hero-{level}", "WARRIOR", "MALE", null);
    }
}
