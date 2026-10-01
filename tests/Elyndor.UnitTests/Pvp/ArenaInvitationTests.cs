using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptIsTerminalAndCannotCreateASecondMatch()
    {
        var invite = new ArenaInvitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Guid matchId = Guid.NewGuid();
        Assert.True(invite.Accept(matchId, Now.AddMinutes(1)));
        Assert.False(invite.Accept(Guid.NewGuid(), Now.AddMinutes(2)));
        Assert.False(invite.Close(ArenaInvitationStatus.Cancelled, Now.AddMinutes(2)));
        Assert.Equal(matchId, invite.MatchId);
    }

    [Fact]
    public void ExpiredInvitationCannotBeAccepted()
    {
        var invite = new ArenaInvitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.False(invite.Accept(Guid.NewGuid(), Now.AddMinutes(5)));
        Assert.Equal(ArenaInvitationStatus.Expired, invite.Status);
    }

    [Theory]
    [InlineData(ArenaInvitationStatus.Declined)]
    [InlineData(ArenaInvitationStatus.Cancelled)]
    public void ClosedInvitationCannotBeAccepted(ArenaInvitationStatus status)
    {
        var invite = new ArenaInvitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True(invite.Close(status, Now));
        Assert.False(invite.Accept(Guid.NewGuid(), Now));
    }
}
