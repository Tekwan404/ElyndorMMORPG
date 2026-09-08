namespace Elyndor.Core.Social;

public enum FriendRequestStatus
{
    Pending,
    Accepted,
    Declined
}

public sealed class FriendRequest
{
    private FriendRequest()
    {
        PairKey = null!;
    }

    private FriendRequest(
        Guid id,
        Guid requesterCharacterId,
        Guid targetCharacterId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty
            || requesterCharacterId == Guid.Empty
            || targetCharacterId == Guid.Empty)
        {
            throw new ArgumentException("Friend request identifiers cannot be empty.");
        }

        if (requesterCharacterId == targetCharacterId)
        {
            throw new ArgumentException("A character cannot send a friend request to itself.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Friend request timestamps must be UTC.", nameof(createdAtUtc));
        }

        Id = id;
        RequesterCharacterId = requesterCharacterId;
        TargetCharacterId = targetCharacterId;
        PairKey = Friendship.GetPairKey(requesterCharacterId, targetCharacterId);
        CreatedAtUtc = createdAtUtc;
        Status = FriendRequestStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid RequesterCharacterId { get; private set; }
    public Guid TargetCharacterId { get; private set; }
    public string PairKey { get; private set; }
    public FriendRequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public Guid? DecidedByCharacterId { get; private set; }

    public static FriendRequest Create(
        Guid id,
        Guid requesterCharacterId,
        Guid targetCharacterId,
        DateTimeOffset createdAtUtc) =>
        new(id, requesterCharacterId, targetCharacterId, createdAtUtc);

    public void Accept(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        EnsurePendingTarget(actorCharacterId, decidedAtUtc);
        Status = FriendRequestStatus.Accepted;
        DecidedAtUtc = decidedAtUtc;
        DecidedByCharacterId = actorCharacterId;
    }

    public void Decline(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        EnsurePendingTarget(actorCharacterId, decidedAtUtc);
        Status = FriendRequestStatus.Declined;
        DecidedAtUtc = decidedAtUtc;
        DecidedByCharacterId = actorCharacterId;
    }

    private void EnsurePendingTarget(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        if (Status != FriendRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending friend requests can be decided.");
        }

        if (actorCharacterId != TargetCharacterId)
        {
            throw new UnauthorizedAccessException("Only the target can decide a friend request.");
        }

        if (decidedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Friend request timestamps must be UTC.", nameof(decidedAtUtc));
        }
    }
}

public sealed class Friendship
{
    private Friendship()
    {
        PairKey = null!;
    }

    private Friendship(
        Guid characterAId,
        Guid characterBId,
        DateTimeOffset createdAtUtc)
    {
        if (characterAId == Guid.Empty || characterBId == Guid.Empty)
        {
            throw new ArgumentException("Friendship identifiers cannot be empty.");
        }

        if (characterAId == characterBId)
        {
            throw new ArgumentException("A character cannot be friends with itself.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Friendship timestamps must be UTC.", nameof(createdAtUtc));
        }

        (CharacterAId, CharacterBId) = Canonicalize(characterAId, characterBId);
        PairKey = GetPairKey(CharacterAId, CharacterBId);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid CharacterAId { get; private set; }
    public Guid CharacterBId { get; private set; }
    public string PairKey { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Friendship Create(
        Guid characterAId,
        Guid characterBId,
        DateTimeOffset createdAtUtc) =>
        new(characterAId, characterBId, createdAtUtc);

    public static Friendship FromAcceptedRequest(
        FriendRequest request,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Status != FriendRequestStatus.Accepted)
        {
            throw new InvalidOperationException("Only accepted requests can create friendships.");
        }

        return Create(
            request.RequesterCharacterId,
            request.TargetCharacterId,
            createdAtUtc);
    }

    private static (Guid A, Guid B) Canonicalize(Guid first, Guid second) =>
        first.CompareTo(second) < 0
            ? (first, second)
            : (second, first);

    public static string GetPairKey(Guid first, Guid second)
    {
        (Guid a, Guid b) = Canonicalize(first, second);
        return $"{a:N}:{b:N}";
    }
}
