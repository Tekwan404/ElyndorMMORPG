using Elyndor.Core.Content;
using Elyndor.Infrastructure.World;

namespace Elyndor.UnitTests.World;

public sealed class OutOfCombatRecoveryOptionsTests
{
    [Fact]
    public void ManaRecoveryScalesWithMaximumManaAndLocation()
    {
        OutOfCombatRecoveryOptions options = new();
        ResourceProfile mana = new("MANA", 2_000, 2_000, 2_000, 4, 12, 0, 0);

        Assert.Equal(240m, options.ResolveResourceRegenPerSecond(mana, isTown: true));
        Assert.Equal(160m, options.ResolveResourceRegenPerSecond(mana, isTown: false));
    }

    [Fact]
    public void NonManaResourcesKeepTheirConfiguredFlatRecovery()
    {
        OutOfCombatRecoveryOptions options = new();
        ResourceProfile focus = new("FOCUS", 250, 250, 250, 8, 12, 0, 0);

        Assert.Equal(12m, options.ResolveResourceRegenPerSecond(focus, isTown: true));
        Assert.Equal(12m, options.ResolveResourceRegenPerSecond(focus, isTown: false));
    }
}
