using Elyndor.Infrastructure.World;

namespace Elyndor.UnitTests.World;

public sealed class WorldEncounterRegistryTests
{
    [Fact]
    public void EncounterTokenIsSingleUseAndWrongTokenDoesNotConsumeIt()
    {
        TestTimeProvider time = new(new DateTimeOffset(2026, 9, 3, 17, 30, 0, TimeSpan.Zero));
        WorldEncounterRegistry registry = new(time);
        Guid accountId = Guid.CreateVersion7();
        PendingWorldEncounter pending = registry.Register(accountId, "WHISPERING_FOREST", "WOLF");

        Assert.False(registry.TryConsume(accountId, Guid.CreateVersion7(), out _));
        Assert.True(registry.TryConsume(accountId, pending.EncounterId, out PendingWorldEncounter consumed));
        Assert.Equal("WOLF", consumed.MonsterId);
        Assert.False(registry.TryConsume(accountId, pending.EncounterId, out _));
    }

    [Fact]
    public void EncounterTokenExpiresAfterFiveMinutes()
    {
        TestTimeProvider time = new(new DateTimeOffset(2026, 9, 3, 17, 30, 0, TimeSpan.Zero));
        WorldEncounterRegistry registry = new(time);
        Guid accountId = Guid.CreateVersion7();
        PendingWorldEncounter pending = registry.Register(accountId, "WHISPERING_FOREST", "WOLF");

        time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.False(registry.TryConsume(accountId, pending.EncounterId, out _));
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
