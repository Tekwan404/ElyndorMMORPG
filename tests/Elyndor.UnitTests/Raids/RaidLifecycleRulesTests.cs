using Elyndor.Core.Raids;

namespace Elyndor.UnitTests.Raids;

public sealed class RaidLifecycleRulesTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 19, 4, 45, 0, TimeSpan.Zero);

    [Fact]
    public void LeaderCanKickMember()
    {
        Guid leaderId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();
        RaidGroup raid = RaidGroup.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            leaderId,
            RaidGroup.DefaultMaximumMembers,
            Now);
        raid.AddMember(memberId, Now.AddSeconds(1));

        RaidMember removed = raid.Kick(leaderId, memberId, Now.AddSeconds(2));

        Assert.Equal(RaidMemberState.Kicked, removed.State);
        Assert.DoesNotContain(raid.Members, member => member.CharacterId == memberId);
    }

    [Fact]
    public void AssistantCannotKickMember()
    {
        Guid leaderId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();
        RaidGroup raid = RaidGroup.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            leaderId,
            RaidGroup.DefaultMaximumMembers,
            Now);
        raid.AddMember(assistantId, Now.AddSeconds(1));
        raid.AddMember(memberId, Now.AddSeconds(2));
        raid.PromoteAssistant(leaderId, assistantId);

        Assert.Throws<UnauthorizedAccessException>(() =>
            raid.Kick(assistantId, memberId, Now.AddSeconds(3)));
    }

    [Fact]
    public void LeaderLeavingPrefersOldestAssistant()
    {
        Guid leaderId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        RaidGroup raid = RaidGroup.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            leaderId,
            RaidGroup.DefaultMaximumMembers,
            Now);
        raid.AddMember(memberId, Now.AddSeconds(1));
        raid.AddMember(assistantId, Now.AddSeconds(2));
        raid.PromoteAssistant(leaderId, assistantId);

        raid.Leave(leaderId, Now.AddSeconds(3));

        Assert.Equal(assistantId, raid.LeaderCharacterId);
        Assert.Equal(
            RaidMemberRole.Leader,
            raid.Members.Single(member => member.CharacterId == assistantId).Role);
    }

    [Fact]
    public void TransferLeadershipDemotesPreviousLeader()
    {
        Guid leaderId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        RaidGroup raid = RaidGroup.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            leaderId,
            RaidGroup.DefaultMaximumMembers,
            Now);
        raid.AddMember(targetId, Now.AddSeconds(1));

        raid.TransferLeadership(leaderId, targetId);

        Assert.Equal(targetId, raid.LeaderCharacterId);
        Assert.Equal(
            RaidMemberRole.Member,
            raid.Members.Single(member => member.CharacterId == leaderId).Role);
        Assert.Equal(
            RaidMemberRole.Leader,
            raid.Members.Single(member => member.CharacterId == targetId).Role);
    }

    [Fact]
    public void AssistantCanBeginReadyCheckAndResponsesAreReset()
    {
        Guid leaderId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        RaidGroup raid = RaidGroup.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            leaderId,
            RaidGroup.DefaultMaximumMembers,
            Now);
        raid.AddMember(assistantId, Now.AddSeconds(1));
        raid.PromoteAssistant(leaderId, assistantId);
        raid.SetReadyState(leaderId, RaidReadyState.Ready);
        raid.SetReadyState(assistantId, RaidReadyState.NotReady);

        raid.BeginReadyCheck(assistantId);

        Assert.All(raid.Members, member => Assert.Equal(RaidReadyState.NoResponse, member.ReadyState));
    }

    [Fact]
    public void ReadyCheckCanCompleteOrExpireButNeverBoth()
    {
        RaidReadyCheck completed = RaidReadyCheck.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            Now.AddMinutes(1));
        completed.Complete(Now.AddSeconds(10));

        RaidReadyCheck expired = RaidReadyCheck.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            Now.AddMinutes(1));
        expired.Expire(Now.AddMinutes(1));

        Assert.Equal(RaidReadyCheckState.Completed, completed.State);
        Assert.Equal(RaidReadyCheckState.Expired, expired.State);
        Assert.Throws<InvalidOperationException>(() => expired.Complete(Now.AddSeconds(20)));
    }
}
