using Elyndor.Infrastructure.World;

namespace Elyndor.UnitTests.World;

public sealed class OpenWorldPresenceTests
{
    [Fact]
    public async Task RosterIsSharedForThreeSecondsAcrossViewers()
    {
        var clock = new MovingClock(new DateTimeOffset(2026, 10, 17, 14, 0, 0, TimeSpan.Zero));
        var presence = new OpenWorldPresence(clock);
        int loads = 0;
        Task<IReadOnlyList<OpenWorldRosterEntry>> Load(CancellationToken _)
        {
            loads++;
            return Task.FromResult<IReadOnlyList<OpenWorldRosterEntry>>([]);
        }

        await presence.GetRosterAsync(Load, CancellationToken.None);
        await presence.GetRosterAsync(Load, CancellationToken.None);
        Assert.Equal(1, loads);

        clock.Current = clock.Current.AddSeconds(4);
        await presence.GetRosterAsync(Load, CancellationToken.None);
        Assert.Equal(2, loads);
    }

    private sealed class MovingClock(DateTimeOffset at) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = at;
        public override DateTimeOffset GetUtcNow() => Current;
    }
}
