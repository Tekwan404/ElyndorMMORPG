using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class CharacterTalentStateTests
{
    [Fact]
    public void SwitchingLoadoutPreservesBothIndependentRankMaps()
    {
        DateTimeOffset now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        CharacterTalentState state = new(Guid.NewGuid(), "WARRIOR_TREE", 1, now);
        state.ReplaceRanks(TalentLoadoutIds.Loadout1, new Dictionary<string, int> { ["G-1-1"] = 2 }, now);
        state.ReplaceRanks(TalentLoadoutIds.Loadout2, new Dictionary<string, int> { ["B-1-1"] = 1 }, now);

        state.SwitchLoadout(TalentLoadoutIds.Loadout2, now);

        Assert.Equal(TalentLoadoutIds.Loadout2, state.ActiveLoadoutId);
        Assert.Equal(2, state.GetRanks(TalentLoadoutIds.Loadout1)["G-1-1"]);
        Assert.Equal(1, state.GetRanks(TalentLoadoutIds.Loadout2)["B-1-1"]);
        Assert.Equal(4, state.StateVersion);
    }

    [Fact]
    public void ResetOnlyClearsSelectedLoadout()
    {
        DateTimeOffset now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        CharacterTalentState state = new(Guid.NewGuid(), "WARRIOR_TREE", 1, now);
        state.ReplaceRanks(TalentLoadoutIds.Loadout1, new Dictionary<string, int> { ["G-1-1"] = 1 }, now);
        state.ReplaceRanks(TalentLoadoutIds.Loadout2, new Dictionary<string, int> { ["B-1-1"] = 1 }, now);

        state.Reset(TalentLoadoutIds.Loadout1, now);

        Assert.Empty(state.GetRanks(TalentLoadoutIds.Loadout1));
        Assert.Single(state.GetRanks(TalentLoadoutIds.Loadout2));
    }

    [Fact]
    public void MutationIdentifierMakesAnExactRetryDetectableWithoutAnotherVersionChange()
    {
        DateTimeOffset now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        CharacterTalentState state = new(Guid.NewGuid(), "WARRIOR_TREE", 1, now);
        const string mutationId = "2f113d97-6242-45eb-9054-ab0236a0edca";

        state.ReplaceRanks(
            TalentLoadoutIds.Loadout1,
            new Dictionary<string, int> { ["G-1-1"] = 1 },
            now,
            mutationId);

        Assert.True(state.HasProcessedMutation(mutationId));
        Assert.False(state.HasProcessedMutation("another-mutation"));
        Assert.Equal(2, state.StateVersion);
    }
}
