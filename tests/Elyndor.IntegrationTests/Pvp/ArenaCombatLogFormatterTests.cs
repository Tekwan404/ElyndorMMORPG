using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Server.Pvp;

namespace Elyndor.IntegrationTests.Pvp;

public sealed class ArenaCombatLogFormatterTests
{
    [Fact]
    public void CompletedArenaLogPreservesServerSequenceAndSourceTargetMetadata()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Guid matchId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 10, 17, 9, 38, 0, TimeSpan.Zero);

        CombatActorSnapshot first = Actor(firstId, "tekwan");
        CombatActorSnapshot second = Actor(secondId, "mini tekwan");
        CombatSessionSnapshot battle = new(matchId, 3, CombatSessionStatus.Victory, now, first, second);
        CombatEvent[] events =
        [
            new(CombatEventType.DamageDealt, now.AddSeconds(2), firstId,
                "STRIKE", 3301, firstId, secondId, Sequence: 2),
            new(CombatEventType.EffectApplied, now.AddSeconds(1), secondId,
                "BERSERKER_RECKLESSNESS_INCOMING", 0, firstId, secondId, Sequence: 1),
            new(CombatEventType.ActorDied, now.AddSeconds(3), secondId,
                SourceActorId: firstId, TargetActorId: secondId, Sequence: 3)
        ];
        ArenaTestState match = new(ArenaTestStatus.Completed, matchId,
            Guid.NewGuid(), Guid.NewGuid(), "mini tekwan", 100, 0,
            ArenaMatchOutcome.WinnerA, 3, events, battle);

        string report = ArenaCombatLogEndpoints.FormatLog(match);

        Assert.Contains("tekwan vs mini tekwan", report);
        Assert.Contains("Всего серверных событий: 3", report);
        Assert.True(report.IndexOf("#1 ", StringComparison.Ordinal)
            < report.IndexOf("#2 ", StringComparison.Ordinal));
        Assert.Contains("tekwan → mini tekwan", report);
        Assert.Contains("amount=3301", report);
        Assert.Contains("BERSERKER_RECKLESSNESS_INCOMING", report);
        Assert.Contains("09:38:02.000", report);
    }

    private static CombatActorSnapshot Actor(Guid id, string name) =>
        new(id, CombatActorKind.Player, "WARRIOR", name, 100, 100, "RAGE", 0, 100, false,
            null, new Dictionary<string, DateTimeOffset>(), new HashSet<string>(),
            Array.Empty<CombatAbilitySnapshot>(), Array.Empty<CombatEffectSnapshot>());
}
