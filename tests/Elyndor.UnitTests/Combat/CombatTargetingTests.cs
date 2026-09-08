using Elyndor.Core.Combat.Targeting;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatTargetingTests
{
    [Fact]
    public void ThreatTableSelectsHighestThreatAndUsesCandidateOrderForTies()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        ThreatTable table = new();

        table.AddThreat(first, 100);
        table.AddThreat(second, 100);

        Assert.Equal(first, table.SelectTarget([first, second]));

        table.AddThreat(second, 1);

        Assert.Equal(second, table.SelectTarget([first, second]));
    }

    [Fact]
    public void ThreatTableSupportsMultipliersAndCannotCreateNegativeThreat()
    {
        Guid actor = Guid.NewGuid();
        ThreatTable table = new();

        table.AddThreat(actor, 100, 1.5m);
        table.AddThreat(actor, -1_000);

        Assert.Equal(0, table.GetThreat(actor));
    }

    [Fact]
    public void ForcedTargetExpiresAndCanBeCleared()
    {
        Guid target = Guid.NewGuid();
        DateTimeOffset now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        ForcedTargetState state = new();

        state.Set(target, now, TimeSpan.FromSeconds(5));

        Assert.Equal(target, state.GetActive(now.AddSeconds(4)));
        Assert.Null(state.GetActive(now.AddSeconds(5)));

        state.Set(target, now, TimeSpan.FromSeconds(5));
        state.Clear();

        Assert.Null(state.GetActive(now));
    }

    [Fact]
    public void TargetSelectionPrefersActiveForcedTargetOverThreat()
    {
        Guid forced = Guid.NewGuid();
        Guid higherThreat = Guid.NewGuid();
        DateTimeOffset now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        ThreatTable threats = new();
        threats.AddThreat(higherThreat, 100);
        threats.AddThreat(forced, 1);
        ForcedTargetState forcedTarget = new();
        forcedTarget.Set(forced, now, TimeSpan.FromSeconds(5));

        Guid? selected = TargetSelectionPolicy.SelectForcedOrThreatTarget(
            forcedTarget,
            threats,
            [new(forced, CombatActorSide.Friendly), new(higherThreat, CombatActorSide.Friendly)],
            now);

        Assert.Equal(forced, selected);
    }
}
