using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Social;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Social;

public static class FriendErrorCodes
{
    public const string InvalidRequest = "friend_request_invalid";
    public const string CharacterNotFound = "friend_character_not_found";
    public const string RequestNotFound = "friend_request_not_found";
    public const string Self = "friend_self";
    public const string AlreadyFriends = "friend_already_friends";
    public const string RequestPending = "friend_request_pending";
    public const string RequestAlreadyDecided = "friend_request_already_decided";
    public const string IdempotencyConflict = "friend_idempotency_conflict";
}

public sealed record PlayerSearchResult(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    string PublicCode,
    string? TelegramUsername);

public sealed record FriendRequestView(
    Guid Id,
    Guid RequesterCharacterId,
    Guid TargetCharacterId,
    FriendRequestStatus Status,
    DateTimeOffset CreatedAtUtc);

public sealed record FriendProfile(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    string PublicCode,
    string? TelegramUsername);

public sealed record FriendSnapshot(
    IReadOnlyList<FriendProfile> Friends,
    IReadOnlyList<FriendRequestView> IncomingRequests,
    IReadOnlyList<FriendRequestView> OutgoingRequests);

public sealed record FriendMutationResult(
    bool IsSuccess,
    string? ErrorCode,
    FriendRequestView? Request = null)
{
    public static FriendMutationResult Success(FriendRequest? request = null) =>
        new(true, null, request is null ? null : ToView(request));

    public static FriendMutationResult Failure(string errorCode) =>
        new(false, errorCode);

    private static FriendRequestView ToView(FriendRequest request) =>
        new(
            request.Id,
            request.RequesterCharacterId,
            request.TargetCharacterId,
            request.Status,
            request.CreatedAtUtc);
}

public sealed class FriendService(
    GameDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(
        Guid accountId,
        string search,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || string.IsNullOrWhiteSpace(search))
            return [];

        Character? current = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(character => character.AccountId == accountId, cancellationToken);
        if (current is null)
            return [];

        string trimmed = search.Trim().TrimStart('@');
        if (trimmed.Length < 2)
            return [];

        string upper = trimmed.ToUpperInvariant();
        string lower = trimmed.ToLowerInvariant();
        return await (
            from character in dbContext.Characters.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on character.AccountId equals account.Id
            where character.Id != current.Id
                && (EF.Functions.ILike(character.NormalizedName, $"%{upper}%")
                    || character.PublicCode.Contains(upper)
                    || (account.NormalizedTelegramUsername != null
                        && EF.Functions.ILike(
                            account.NormalizedTelegramUsername,
                            $"%{lower}%")))
            orderby character.Name
            select new PlayerSearchResult(
                character.Id,
                character.Name,
                character.Level,
                character.ClassId,
                character.PublicCode,
                account.TelegramUsername == null
                    ? null
                    : "@" + account.TelegramUsername.TrimStart('@')))
            .Take(20)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<FriendSnapshot?> GetSnapshotAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? current = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(character => character.AccountId == accountId, cancellationToken);
        if (current is null)
            return null;

        Friendship[] friendships = await dbContext.Friendships
            .AsNoTracking()
            .Where(friendship => friendship.CharacterAId == current.Id
                || friendship.CharacterBId == current.Id)
            .ToArrayAsync(cancellationToken);
        Guid[] friendIds = friendships
            .Select(friendship => friendship.CharacterAId == current.Id
                ? friendship.CharacterBId
                : friendship.CharacterAId)
            .ToArray();

        FriendProfile[] friends = await LoadProfilesAsync(friendIds, cancellationToken);
        FriendRequest[] requests = await dbContext.FriendRequests
            .AsNoTracking()
            .Where(request => request.Status == FriendRequestStatus.Pending
                && (request.RequesterCharacterId == current.Id
                    || request.TargetCharacterId == current.Id))
            .OrderBy(request => request.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return new FriendSnapshot(
            friends,
            requests
                .Where(request => request.TargetCharacterId == current.Id)
                .Select(ToView)
                .ToArray(),
            requests
                .Where(request => request.RequesterCharacterId == current.Id)
                .Select(ToView)
                .ToArray());
    }

    public Task<FriendMutationResult> SendRequestAsync(
        Guid accountId,
        Guid targetCharacterId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => SendRequestCoreAsync(
                accountId,
                targetCharacterId,
                requestId,
                cancellationToken));

    private async Task<FriendMutationResult> SendRequestCoreAsync(
        Guid accountId,
        Guid targetCharacterId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        if (accountId == Guid.Empty || targetCharacterId == Guid.Empty || requestId == Guid.Empty)
            return FriendMutationResult.Failure(FriendErrorCodes.InvalidRequest);

        Character? requester = await GetCharacterAsync(accountId, cancellationToken);
        if (requester is null)
            return FriendMutationResult.Failure(FriendErrorCodes.CharacterNotFound);
        if (requester.Id == targetCharacterId)
            return FriendMutationResult.Failure(FriendErrorCodes.Self);

        string pairKey = Friendship.GetPairKey(requester.Id, targetCharacterId);
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pairKey, cancellationToken);
        dbContext.ChangeTracker.Clear();
        requester = await GetCharacterAsync(accountId, cancellationToken);
        if (requester is null)
            return FriendMutationResult.Failure(FriendErrorCodes.CharacterNotFound);

        FriendRequest? replay = await dbContext.FriendRequests
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);
        if (replay is not null)
        {
            bool matches = replay.RequesterCharacterId == requester.Id
                && replay.TargetCharacterId == targetCharacterId;
            await transaction.CommitAsync(cancellationToken);
            return matches
                ? FriendMutationResult.Success(replay)
                : FriendMutationResult.Failure(FriendErrorCodes.IdempotencyConflict);
        }

        bool targetExists = await dbContext.Characters
            .AnyAsync(character => character.Id == targetCharacterId, cancellationToken);
        if (!targetExists)
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendMutationResult.Failure(FriendErrorCodes.CharacterNotFound);
        }

        if (await dbContext.Friendships.AnyAsync(
                friendship => friendship.PairKey == pairKey,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendMutationResult.Failure(FriendErrorCodes.AlreadyFriends);
        }

        FriendRequest? pending = await dbContext.FriendRequests
            .SingleOrDefaultAsync(request => request.PairKey == pairKey
                && request.Status == FriendRequestStatus.Pending, cancellationToken);
        if (pending is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendMutationResult.Failure(FriendErrorCodes.RequestPending);
        }

        FriendRequest request = FriendRequest.Create(
            requestId,
            requester.Id,
            targetCharacterId,
            timeProvider.GetUtcNow());
        dbContext.FriendRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendMutationResult.Success(request);
    }

    public Task<FriendMutationResult> AcceptRequestAsync(
        Guid accountId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        DecideRequestAsync(accountId, requestId, accept: true, cancellationToken);

    public Task<FriendMutationResult> DeclineRequestAsync(
        Guid accountId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        DecideRequestAsync(accountId, requestId, accept: false, cancellationToken);

    public Task<FriendMutationResult> RemoveFriendAsync(
        Guid accountId,
        Guid friendCharacterId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => RemoveFriendCoreAsync(accountId, friendCharacterId, cancellationToken));

    private async Task<FriendMutationResult> RemoveFriendCoreAsync(
        Guid accountId,
        Guid friendCharacterId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Character? current = await GetCharacterAsync(accountId, cancellationToken);
        if (current is null)
            return FriendMutationResult.Failure(FriendErrorCodes.CharacterNotFound);

        string pairKey = Friendship.GetPairKey(current.Id, friendCharacterId);
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pairKey, cancellationToken);
        dbContext.ChangeTracker.Clear();
        Friendship? friendship = await dbContext.Friendships
            .SingleOrDefaultAsync(candidate => candidate.PairKey == pairKey, cancellationToken);
        if (friendship is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendMutationResult.Failure(FriendErrorCodes.CharacterNotFound);
        }

        dbContext.Friendships.Remove(friendship);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendMutationResult.Success();
    }

    private Task<FriendMutationResult> DecideRequestAsync(
        Guid accountId,
        Guid requestId,
        bool accept,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => DecideRequestCoreAsync(accountId, requestId, accept, cancellationToken));

    private async Task<FriendMutationResult> DecideRequestCoreAsync(
        Guid accountId,
        Guid requestId,
        bool accept,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || requestId == Guid.Empty)
            return FriendMutationResult.Failure(FriendErrorCodes.InvalidRequest);

        Character? target = await GetCharacterAsync(accountId, cancellationToken);
        if (target is null)
            return FriendMutationResult.Failure(FriendErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        FriendRequest? probe = await dbContext.FriendRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        if (probe is null || probe.TargetCharacterId != target.Id)
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendMutationResult.Failure(FriendErrorCodes.RequestNotFound);
        }

        await AcquirePairLockAsync(probe.PairKey, cancellationToken);
        dbContext.ChangeTracker.Clear();
        target = await GetCharacterAsync(accountId, cancellationToken);
        FriendRequest? request = await dbContext.FriendRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        if (target is null || request is null || request.TargetCharacterId != target.Id)
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendMutationResult.Failure(FriendErrorCodes.RequestNotFound);
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendMutationResult.Failure(FriendErrorCodes.RequestAlreadyDecided);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (accept)
        {
            request.Accept(target.Id, now);
            dbContext.Friendships.Add(Friendship.FromAcceptedRequest(request, now));
        }
        else
        {
            request.Decline(target.Id, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendMutationResult.Success(request);
    }

    private async Task<Character?> GetCharacterAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await dbContext.Characters
            .SingleOrDefaultAsync(character => character.AccountId == accountId, cancellationToken);

    private async Task AcquirePairLockAsync(
        string pairKey,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"friend-pair:{pairKey}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private async Task<FriendProfile[]> LoadProfilesAsync(
        Guid[] characterIds,
        CancellationToken cancellationToken)
    {
        if (characterIds.Length == 0)
            return [];

        return await (
            from character in dbContext.Characters.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on character.AccountId equals account.Id
            where characterIds.Contains(character.Id)
            orderby character.Name
            select new FriendProfile(
                character.Id,
                character.Name,
                character.Level,
                character.ClassId,
                character.PublicCode,
                account.TelegramUsername == null
                    ? null
                    : "@" + account.TelegramUsername.TrimStart('@')))
            .ToArrayAsync(cancellationToken);
    }

    private static FriendRequestView ToView(FriendRequest request) =>
        new(
            request.Id,
            request.RequesterCharacterId,
            request.TargetCharacterId,
            request.Status,
            request.CreatedAtUtc);
}
