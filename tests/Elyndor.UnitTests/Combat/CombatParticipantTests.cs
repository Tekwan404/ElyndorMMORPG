using Elyndor.Core.Combat.Participants;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatParticipantTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FrozenRosterAllowsOriginalLateMemberButRejectsNewPartyMember()
    {
        Guid first = Guid.NewGuid();
        Guid late = Guid.NewGuid();
        CombatParticipantRoster roster = new(
        [
            new(Guid.NewGuid(), first, Guid.NewGuid()),
            new(Guid.NewGuid(), late, Guid.NewGuid())
        ],
        Start);

        Assert.True(roster.TryAttach(first, Start, out _));
        Assert.True(roster.TryAttach(late, Start.AddSeconds(10), out _));
        Assert.False(roster.TryAttach(Guid.NewGuid(), Start.AddSeconds(10), out string? error));
        Assert.Equal(CombatParticipantErrorCodes.NotInRoster, error);
    }

    [Fact]
    public void FledParticipantCannotRejoinAndOtherParticipantsKeepTheFightAlive()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        CombatParticipantRoster roster = new(
        [
            new(Guid.NewGuid(), first, Guid.NewGuid()),
            new(Guid.NewGuid(), second, Guid.NewGuid())
        ],
        Start);

        Assert.True(roster.TryAttach(first, Start, out _));
        Assert.True(roster.TryAttach(second, Start, out _));
        Assert.True(roster.TryFlee(first, Start.AddSeconds(5), out _));
        Assert.False(roster.TryAttach(first, Start.AddSeconds(6), out string? error));
        Assert.Equal(CombatParticipantErrorCodes.AlreadyFled, error);
        Assert.True(roster.HasActiveParticipants());
        Assert.True(roster.TryFlee(second, Start.AddSeconds(7), out _));
        Assert.False(roster.HasActiveParticipants());
    }

    [Fact]
    public void DeadParticipantRemainsAnEncounterParticipant()
    {
        Guid characterId = Guid.NewGuid();
        CombatParticipantRoster roster = new(
            [new(Guid.NewGuid(), characterId, Guid.NewGuid())],
            Start);

        Assert.True(roster.TryAttach(characterId, Start, out _));
        Assert.True(roster.TryMarkDead(characterId, Start.AddSeconds(20)));
        Assert.True(roster.IsEligibleRosterMember(characterId));
        Assert.False(roster.HasActiveParticipants());
    }
}
