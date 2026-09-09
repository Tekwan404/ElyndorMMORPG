using Elyndor.Core.Social;

namespace Elyndor.UnitTests.Social;

public sealed class FriendRulesTests
{
    [Fact]
    public void AcceptingRequestCreatesAcceptedRequestAndCanonicalFriendship()
    {
        Guid requesterId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid targetId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        DateTimeOffset createdAt = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset acceptedAt = createdAt.AddMinutes(1);

        FriendRequest request = FriendRequest.Create(
            Guid.NewGuid(),
            requesterId,
            targetId,
            createdAt);

        request.Accept(targetId, acceptedAt);
        Friendship friendship = Friendship.FromAcceptedRequest(request, acceptedAt);

        Assert.Equal(FriendRequestStatus.Accepted, request.Status);
        Assert.Equal(targetId, request.DecidedByCharacterId);
        Assert.Equal(requesterId, friendship.CharacterAId);
        Assert.Equal(targetId, friendship.CharacterBId);
        Assert.Equal(acceptedAt, friendship.CreatedAtUtc);
    }

    [Fact]
    public void RequesterCanCancelPendingRequest()
    {
        Guid requesterId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid targetId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        DateTimeOffset createdAt = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
        FriendRequest request = FriendRequest.Create(
            Guid.NewGuid(),
            requesterId,
            targetId,
            createdAt);

        request.Cancel(requesterId, createdAt.AddMinutes(1));

        Assert.Equal(FriendRequestStatus.Cancelled, request.Status);
        Assert.Equal(requesterId, request.DecidedByCharacterId);
        Assert.Equal(createdAt.AddMinutes(1), request.DecidedAtUtc);
    }

    [Fact]
    public void FriendshipPairIsCanonicalRegardlessOfInputOrder()
    {
        Guid lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        DateTimeOffset createdAt = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

        Friendship friendship = Friendship.Create(higherId, lowerId, createdAt);

        Assert.Equal(lowerId, friendship.CharacterAId);
        Assert.Equal(higherId, friendship.CharacterBId);
    }

    [Fact]
    public void SelfFriendRequestIsRejected()
    {
        Guid characterId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => FriendRequest.Create(
            Guid.NewGuid(),
            characterId,
            characterId,
            DateTimeOffset.UtcNow));
    }
}
