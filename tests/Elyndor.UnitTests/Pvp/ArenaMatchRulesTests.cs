using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaMatchRulesTests
{
    private static readonly Guid First = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData(30, 30, true)]
    [InlineData(30, 33, true)]
    [InlineData(30, 34, false)]
    [InlineData(1, 5, false)]
    public void PairingRequiresNearbyLevels(int firstLevel, int secondLevel, bool expected)
    {
        Assert.Equal(expected, ArenaMatchRules.CanPair(First, firstLevel, Second, secondLevel));
    }

    [Fact]
    public void PairingRejectsSameCharacter()
    {
        Assert.False(ArenaMatchRules.CanPair(First, 30, First, 30));
    }

    [Theory]
    [InlineData(true, false, false, ArenaMatchOutcome.WinnerA)]
    [InlineData(true, false, true, ArenaMatchOutcome.WinnerA)]
    [InlineData(false, true, false, ArenaMatchOutcome.WinnerB)]
    [InlineData(false, false, false, ArenaMatchOutcome.Draw)]
    [InlineData(true, true, true, ArenaMatchOutcome.Draw)]
    [InlineData(true, true, false, ArenaMatchOutcome.Active)]
    public void ResolveUsesBothActorDeathsAndTimeout(bool firstAlive, bool secondAlive,
        bool timedOut, ArenaMatchOutcome expected)
    {
        Assert.Equal(expected, ArenaMatchRules.Resolve(ArenaMatchOutcome.Active,
            firstAlive, secondAlive, timedOut));
    }

    [Fact]
    public void TerminalResultCannotChangeOnLateOrDuplicateDeath()
    {
        Assert.Equal(ArenaMatchOutcome.WinnerA,
            ArenaMatchRules.Resolve(ArenaMatchOutcome.WinnerA, false, false, true));
    }
}
