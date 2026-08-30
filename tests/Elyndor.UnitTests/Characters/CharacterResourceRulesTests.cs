using Elyndor.Core.Characters;
using Elyndor.Core.Content;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterResourceRulesTests
{
    [Fact]
    public void AppliesClassSpecificElapsedRulesAndClampsValues()
    {
        ResourceProfile mana = new("MANA", 100, 100, 100, 4, 12, 0, 0);
        ResourceProfile focus = new("FOCUS", 100, 100, 100, 8, 12, 0, 0);
        ResourceProfile rage = new("RAGE", 100, 0, 0, 0, 0, 5, 5);

        Assert.Equal(70, CharacterResourceRules.ApplyElapsed(
            mana, 10, TimeSpan.FromSeconds(5), isInCombat: false, TimeSpan.FromSeconds(5)));
        Assert.Equal(50, CharacterResourceRules.ApplyElapsed(
            focus, 10, TimeSpan.FromSeconds(5), isInCombat: true, TimeSpan.FromSeconds(5)));
        Assert.Equal(75, CharacterResourceRules.ApplyElapsed(
            rage, 100, TimeSpan.FromSeconds(10), isInCombat: false, TimeSpan.FromSeconds(10)));
        Assert.False(CharacterResourceRules.TrySpend(mana, 20, 30, out decimal unchanged));
        Assert.Equal(20, unchanged);
        Assert.True(CharacterResourceRules.TrySpend(mana, 20, 12, out decimal remaining));
        Assert.Equal(8, remaining);
        Assert.Equal(100, CharacterResourceRules.Restore(mana, 95, 20));
        Assert.Equal(100, CharacterResourceRules.Respawn(mana));
        Assert.Equal(0, CharacterResourceRules.Respawn(rage));
    }
}
