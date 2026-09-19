using Elyndor.Core.Combat.Participants;

namespace Elyndor.UnitTests.Combat;

public sealed class RaidCombatRosterTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 9, 19, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultRoster_RejectsSixParticipants() =>
        Assert.Throws<ArgumentException>(() =>
            new CombatParticipantRoster(Participants(6), UtcNow));

    [Fact]
    public void RaidRoster_AllowsTwentyParticipants()
    {
        CombatParticipantRoster roster = new(
            Participants(20),
            UtcNow,
            CombatParticipantLimit.MaximumRaid);

        Assert.Equal(20, roster.Participants.Count);
        Assert.Equal(CombatParticipantLimit.MaximumRaid, roster.MaximumParticipants);
    }

    [Fact]
    public void RaidRoster_RejectsTwentyFirstParticipant() =>
        Assert.Throws<ArgumentException>(() =>
            new CombatParticipantRoster(
                Participants(21),
                UtcNow,
                CombatParticipantLimit.MaximumRaid));

    [Fact]
    public void MaximumAboveRaidLimit_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CombatParticipantRoster(
                Participants(1),
                UtcNow,
                CombatParticipantLimit.MaximumRaid + 1));

    [Fact]
    public void FledRaidParticipant_CannotReattach()
    {
        Guid characterId = Guid.NewGuid();
        CombatParticipantRoster roster = new(
            [new CombatParticipantIdentity(Guid.NewGuid(), characterId, characterId)],
            UtcNow,
            CombatParticipantLimit.MaximumRaid);
        Assert.True(roster.TryAttach(characterId, UtcNow, out _));
        Assert.True(roster.TryFlee(characterId, UtcNow.AddSeconds(1), out _));

        Assert.False(roster.TryAttach(characterId, UtcNow.AddSeconds(2), out string? errorCode));
        Assert.Equal(CombatParticipantErrorCodes.AlreadyFled, errorCode);
    }

    private static CombatParticipantIdentity[] Participants(int count) =>
        Enumerable.Range(0, count)
            .Select(_ =>
            {
                Guid characterId = Guid.NewGuid();
                return new CombatParticipantIdentity(
                    Guid.NewGuid(),
                    characterId,
                    characterId);
            })
            .ToArray();
}
