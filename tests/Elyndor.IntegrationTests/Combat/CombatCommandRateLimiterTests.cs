using Elyndor.Server;

namespace Elyndor.IntegrationTests.Combat;

public sealed class CombatCommandRateLimiterTests
{
    [Fact]
    public void EnforcesPermitLimitPerAccountWithoutCrossAccountInterference()
    {
        ServerRateLimitingOptions settings = new()
        {
            CombatCommandPermitLimit = 2,
            CombatCommandWindowSeconds = 60
        };
        using CombatCommandRateLimiter limiter = new(settings);
        Guid firstAccount = Guid.CreateVersion7();
        Guid secondAccount = Guid.CreateVersion7();

        Assert.True(limiter.TryAcquire(firstAccount));
        Assert.True(limiter.TryAcquire(firstAccount));
        Assert.False(limiter.TryAcquire(firstAccount));

        Assert.True(limiter.TryAcquire(secondAccount));
    }
}
