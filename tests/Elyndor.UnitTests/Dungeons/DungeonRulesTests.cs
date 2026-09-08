using Elyndor.Core.Dungeons;

namespace Elyndor.UnitTests.Dungeons;

public sealed class DungeonRulesTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WipeKeepsEncounterIndexAndAllowsFreshRoster()
    {
        Guid runId = Guid.NewGuid();
        DungeonRun run = DungeonRun.Create(
            runId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ANCIENT_MINE",
            Start);
        DungeonEncounter encounter = DungeonEncounter.Create(
            Guid.NewGuid(),
            runId,
            0,
            "DEEP_WOLF_L6",
            Start);
        run.Encounters.Add(encounter);
        encounter.Members.Add(DungeonEncounterMember.Create(encounter.Id, Guid.NewGuid(), Start));
        encounter.Activate(Guid.NewGuid());

        encounter.MarkWiped();
        encounter.ResetForRetry();

        Assert.Equal(0, run.CurrentEncounterIndex);
        Assert.Equal(DungeonEncounterState.Pending, encounter.State);
        Assert.Equal(1, encounter.WipeCount);
        Assert.Empty(encounter.Members);
    }

    [Fact]
    public void RunCanAdvanceAndComplete()
    {
        DungeonRun run = DungeonRun.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ANCIENT_MINE",
            Start);

        run.AdvanceEncounter(Start.AddMinutes(1));
        run.Complete(Start.AddMinutes(2));

        Assert.Equal(DungeonRunState.Completed, run.State);
        Assert.Equal(1, run.CurrentEncounterIndex);
        Assert.NotNull(run.CompletedAtUtc);
    }
}
