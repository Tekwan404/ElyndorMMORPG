using Elyndor.Core.Afk;

namespace Elyndor.UnitTests.Afk;

public sealed class AfkFarmSessionTests
{
    [Fact]
    public void CreateInitializesDurableActiveCursor()
    {
        DateTimeOffset now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
        AfkFarmSession session = Create(now);

        Assert.Equal(AfkFarmStatus.Active, session.Status);
        Assert.Equal(now, session.StartedAtUtc);
        Assert.Equal(now, session.LastProcessedAtUtc);
        Assert.Equal(now.AddHours(1), session.EndsAtUtc);
        Assert.Equal(1, session.Version);
        Assert.False(session.IsTerminal);
    }

    [Fact]
    public void CancelIsIdempotent()
    {
        DateTimeOffset now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
        AfkFarmSession session = Create(now);

        Assert.True(session.Cancel(now.AddMinutes(10)));
        long version = session.Version;
        DateTimeOffset? completedAt = session.CompletedAtUtc;

        Assert.False(session.Cancel(now.AddMinutes(20)));
        Assert.Equal(AfkFarmStatus.Cancelled, session.Status);
        Assert.Equal(version, session.Version);
        Assert.Equal(completedAt, session.CompletedAtUtc);
        Assert.Equal("manual_cancel", session.StopReason);
    }

    [Fact]
    public void CreateRejectsNonPositiveDuration()
    {
        DateTimeOffset now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => new AfkFarmSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WHISPERING_FOREST",
            null,
            now,
            now,
            "content-v1",
            "balance-v1",
            "{}",
            now));
    }

    [Fact]
    public void CompleteIntervalAdvancesCursorAndCompletesAtEnd()
    {
        DateTimeOffset now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
        AfkFarmSession session = Create(now);

        session.CompleteInterval(now.AddMinutes(15));
        session.CompleteInterval(now.AddHours(1));

        Assert.Equal(now.AddHours(1), session.LastProcessedAtUtc);
        Assert.Equal(AfkFarmStatus.Completed, session.Status);
        Assert.Equal(now.AddHours(1), session.CompletedAtUtc);
        Assert.Equal("duration_elapsed", session.StopReason);
    }

    private static AfkFarmSession Create(DateTimeOffset now) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "WHISPERING_FOREST",
        null,
        now,
        now.AddHours(1),
        "content-v1",
        "balance-v1",
        "{}",
        now);
}
