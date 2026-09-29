using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Pvp;
using Microsoft.Extensions.Options;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaMatchRuntimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AccountCannotBeRegisteredInTwoMatches()
    {
        var h = new Harness();
        ArenaTestEntrant a = Entrant(), b = Entrant(), c = Entrant();
        h.Runtime.Register(Guid.NewGuid(), a, b);
        Assert.True(h.Runtime.IsInMatch(a.Fighter.AccountId));
        Assert.Throws<InvalidOperationException>(() => h.Runtime.Register(Guid.NewGuid(), a, c));
    }

    [Fact]
    public void NonParticipantCannotReadOrCommandTheMatch()
    {
        var h = new Harness();
        Guid matchId = Guid.NewGuid();
        ArenaTestEntrant a = Entrant(), b = Entrant();
        h.Runtime.Register(matchId, a, b);
        Assert.Null(h.Runtime.GetState(Guid.NewGuid(), matchId, 0));
        Assert.Equal("arena_match_not_found", h.Runtime.UseAbility(Guid.NewGuid(), matchId, "x", "STRIKE",
            b.Fighter.Actor.ActorId).ErrorCode);
    }

    [Fact]
    public void PlayerGoneLongerThanGraceForfeitsAndOpponentWins()
    {
        var h = new Harness();
        Guid matchId = Guid.NewGuid();
        ArenaTestEntrant a = Entrant(), b = Entrant();
        h.Runtime.Register(matchId, a, b);
        h.Presence.Connected(a.Fighter.AccountId, "a1");
        h.Presence.Connected(b.Fighter.AccountId, "b1");
        h.Time.Advance(TimeSpan.FromSeconds(10));
        h.Presence.Disconnected(a.Fighter.AccountId, "a1");

        h.Time.Advance(TimeSpan.FromSeconds(19));
        Assert.Empty(h.Runtime.Tick().Finished);

        h.Time.Advance(TimeSpan.FromSeconds(2));
        ArenaFinishedMatch finished = Assert.Single(h.Runtime.Tick().Finished);
        Assert.Equal(matchId, finished.MatchId);
        Assert.Equal(ArenaMatchOutcome.WinnerB, finished.Outcome);
    }

    [Fact]
    public void ReconnectWithinGraceKeepsTheMatchAlive()
    {
        var h = new Harness();
        ArenaTestEntrant a = Entrant(), b = Entrant();
        h.Runtime.Register(Guid.NewGuid(), a, b);
        h.Presence.Connected(a.Fighter.AccountId, "a1");
        h.Presence.Connected(b.Fighter.AccountId, "b1");
        h.Presence.Disconnected(a.Fighter.AccountId, "a1");
        h.Time.Advance(TimeSpan.FromSeconds(15));
        h.Presence.Connected(a.Fighter.AccountId, "a2");
        h.Time.Advance(TimeSpan.FromSeconds(40));
        Assert.Empty(h.Runtime.Tick().Finished);
    }

    [Fact]
    public void BothPlayersGoneCancelsWithoutAWinner()
    {
        var h = new Harness();
        ArenaTestEntrant a = Entrant(), b = Entrant();
        h.Runtime.Register(Guid.NewGuid(), a, b);
        h.Time.Advance(TimeSpan.FromSeconds(21));
        Assert.Equal(ArenaMatchOutcome.Cancelled, Assert.Single(h.Runtime.Tick().Finished).Outcome);
    }

    [Fact]
    public void FinalizingFreesPlayersAndRetentionPurgesState()
    {
        var h = new Harness();
        Guid matchId = Guid.NewGuid();
        ArenaTestEntrant a = Entrant(), b = Entrant();
        h.Runtime.Register(matchId, a, b);
        Assert.True(h.Runtime.Surrender(a.Fighter.AccountId, matchId).Succeeded);
        h.Runtime.MarkFinalized(matchId);
        Assert.False(h.Runtime.IsInMatch(a.Fighter.AccountId));
        Assert.NotNull(h.Runtime.GetState(b.Fighter.AccountId, matchId, 0));

        h.Time.Advance(TimeSpan.FromMinutes(3));
        h.Runtime.PurgeFinalized();
        Assert.Null(h.Runtime.GetState(b.Fighter.AccountId, matchId, 0));
    }

    [Fact]
    public void ConfiguredDurationEndsTheMatchAsDraw()
    {
        var h = new Harness(TimeSpan.FromSeconds(30));
        ArenaTestEntrant a = Entrant(), b = Entrant();
        h.Runtime.Register(Guid.NewGuid(), a, b);
        h.Presence.Connected(a.Fighter.AccountId, "a1");
        h.Presence.Connected(b.Fighter.AccountId, "b1");
        h.Time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(ArenaMatchOutcome.Draw, Assert.Single(h.Runtime.Tick().Finished).Outcome);
    }

    private static ArenaTestEntrant Entrant()
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
        return new ArenaTestEntrant(fighter, 30, "Hero", "WARRIOR", "MALE", null);
    }

    private sealed class Harness
    {
        public Harness(TimeSpan? duration = null)
        {
            Presence = new ArenaPresenceTracker(Time);
            Runtime = new ArenaMatchRuntime(Time, new SeededFactory(), Presence,
                Options.Create(new ArenaOptions
                {
                    MatchDuration = duration ?? TimeSpan.FromSeconds(60),
                    ReconnectGrace = TimeSpan.FromSeconds(20),
                    CompletedMatchRetention = TimeSpan.FromMinutes(2)
                }));
        }

        public ManualTime Time { get; } = new(Start);
        public ArenaPresenceTracker Presence { get; }
        public ArenaMatchRuntime Runtime { get; }
    }

    private sealed class SeededFactory : IGameRandomFactory
    {
        public IGameRandom Create() => new SeededGameRandom(42);
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
