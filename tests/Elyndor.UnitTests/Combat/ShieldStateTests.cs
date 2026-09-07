using Elyndor.Core.Combat.Effects;

namespace Elyndor.UnitTests.Combat;

public sealed class ShieldStateTests
{
    [Fact]
    public void ShieldAbsorbsDamageAndReportsOverkillWithoutGoingNegative()
    {
        DateTimeOffset now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        ShieldState shield = new(Guid.NewGuid(), 30, now.AddSeconds(10));

        ShieldAbsorption first = shield.Absorb(20, now);
        ShieldAbsorption second = shield.Absorb(20, now);

        Assert.Equal(20, first.Absorbed);
        Assert.Equal(0, first.RemainingDamage);
        Assert.Equal(10, second.Absorbed);
        Assert.Equal(10, second.RemainingDamage);
        Assert.Equal(0, shield.RemainingAmount);
    }

    [Fact]
    public void ExpiredShieldDoesNotAbsorbDamage()
    {
        DateTimeOffset now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        ShieldState shield = new(Guid.NewGuid(), 30, now.AddSeconds(10));

        ShieldAbsorption result = shield.Absorb(20, now.AddSeconds(10));

        Assert.Equal(0, result.Absorbed);
        Assert.Equal(20, result.RemainingDamage);
    }
}
