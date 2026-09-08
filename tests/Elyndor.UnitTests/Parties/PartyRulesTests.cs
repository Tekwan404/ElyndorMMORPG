using Elyndor.Core.Parties;

namespace Elyndor.UnitTests.Parties;

public sealed class PartyRulesTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 8, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PartyStartsWithLeaderAndPromotesEarliestMemberWhenLeaderLeaves()
    {
        Guid partyId = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        Guid leaderId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid firstMemberId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Guid secondMemberId = Guid.Parse("00000000-0000-0000-0000-000000000003");

        Party party = Party.Create(partyId, requestId, leaderId, CreatedAt);
        party.AddMember(firstMemberId, CreatedAt.AddMinutes(1));
        party.AddMember(secondMemberId, CreatedAt.AddMinutes(2));

        party.Leave(leaderId, CreatedAt.AddMinutes(3));

        Assert.Equal(firstMemberId, party.LeaderCharacterId);
        Assert.Equal(PartyState.Active, party.State);
        Assert.DoesNotContain(party.Members, member => member.CharacterId == leaderId);
    }

    [Fact]
    public void PartyDisbandsWhenLastMemberLeaves()
    {
        Guid leaderId = Guid.NewGuid();
        Party party = Party.Create(Guid.NewGuid(), Guid.NewGuid(), leaderId, CreatedAt);

        party.Leave(leaderId, CreatedAt.AddMinutes(1));

        Assert.Equal(PartyState.Disbanded, party.State);
        Assert.Empty(party.Members);
    }

    [Fact]
    public void PartyRejectsSixthMember()
    {
        Party party = Party.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CreatedAt);
        for (var index = 0; index < Party.MaxPartySize - 1; index++)
            party.AddMember(Guid.NewGuid(), CreatedAt.AddMinutes(index + 1));

        Assert.Throws<InvalidOperationException>(() =>
            party.AddMember(Guid.NewGuid(), CreatedAt.AddMinutes(10)));
    }
}
