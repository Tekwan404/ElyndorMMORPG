using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Social;
using Elyndor.Infrastructure.Social;

namespace Elyndor.Server.Social;

public static class SocialEndpoints
{
    public static IEndpointRouteBuilder MapSocialEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1")
            .RequireAuthorization()
            .WithTags("Social");

        group.MapGet("/social/search", SearchAsync);
        group.MapGet("/friends", GetSnapshotAsync);
        group.MapPost("/friends/requests", SendRequestAsync);
        group.MapPost("/friends/requests/{requestId:guid}/accept", AcceptRequestAsync);
        group.MapPost("/friends/requests/{requestId:guid}/decline", DeclineRequestAsync);
        group.MapDelete("/friends/{friendCharacterId:guid}", RemoveFriendAsync);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? query,
        ClaimsPrincipal user,
        FriendService friendService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        IReadOnlyList<PlayerSearchResult> results = await friendService.SearchAsync(
            accountId,
            query ?? string.Empty,
            cancellationToken);
        return Results.Ok(results.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GetSnapshotAsync(
        ClaimsPrincipal user,
        FriendService friendService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        FriendSnapshot? snapshot = await friendService.GetSnapshotAsync(
            accountId,
            cancellationToken);
        return snapshot is null
            ? Problem("friend_character_not_found", StatusCodes.Status404NotFound, httpContext)
            : Results.Ok(ToResponse(snapshot));
    }

    private static Task<IResult> SendRequestAsync(
        SendFriendRequestRequest request,
        ClaimsPrincipal user,
        FriendService friendService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            user,
            httpContext,
            () => friendService.SendRequestAsync(
                GetAccountId(user),
                request.TargetCharacterId,
                request.RequestId,
                cancellationToken));

    private static Task<IResult> AcceptRequestAsync(
        Guid requestId,
        ClaimsPrincipal user,
        FriendService friendService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            user,
            httpContext,
            () => friendService.AcceptRequestAsync(
                GetAccountId(user),
                requestId,
                cancellationToken));

    private static Task<IResult> DeclineRequestAsync(
        Guid requestId,
        ClaimsPrincipal user,
        FriendService friendService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            user,
            httpContext,
            () => friendService.DeclineRequestAsync(
                GetAccountId(user),
                requestId,
                cancellationToken));

    private static Task<IResult> RemoveFriendAsync(
        Guid friendCharacterId,
        ClaimsPrincipal user,
        FriendService friendService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            user,
            httpContext,
            () => friendService.RemoveFriendAsync(
                GetAccountId(user),
                friendCharacterId,
                cancellationToken));

    private static async Task<IResult> ExecuteMutationAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        Func<Task<FriendMutationResult>> operation)
    {
        if (!TryGetAccountId(user, out _))
            return Results.Unauthorized();

        FriendMutationResult result = await operation();
        if (result.IsSuccess)
            return Results.Ok(result.Request is null ? null : ToResponse(result.Request));

        int statusCode = result.ErrorCode switch
        {
            FriendErrorCodes.CharacterNotFound
            or FriendErrorCodes.RequestNotFound => StatusCodes.Status404NotFound,
            FriendErrorCodes.AlreadyFriends
            or FriendErrorCodes.RequestPending
            or FriendErrorCodes.RequestAlreadyDecided
            or FriendErrorCodes.IdempotencyConflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        return Problem(result.ErrorCode!, statusCode, httpContext);
    }

    private static PlayerSearchResponse ToResponse(PlayerSearchResult result) =>
        new(result.CharacterId, result.Name, result.Level, result.ClassId, result.PublicCode, result.TelegramUsername);

    private static FriendsSnapshotResponse ToResponse(FriendSnapshot snapshot) =>
        new(
            snapshot.Friends.Select(friend => new FriendProfileResponse(
                friend.CharacterId,
                friend.Name,
                friend.Level,
                friend.ClassId,
                friend.PublicCode,
                friend.TelegramUsername)).ToArray(),
            snapshot.IncomingRequests.Select(ToResponse).ToArray(),
            snapshot.OutgoingRequests.Select(ToResponse).ToArray());

    private static FriendRequestResponse ToResponse(FriendRequestView request) =>
        new(
            request.Id,
            request.RequesterCharacterId,
            request.TargetCharacterId,
            request.Status.ToString(),
            request.CreatedAtUtc);

    private static IResult Problem(string code, int statusCode, HttpContext context) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier
            });

    private static Guid GetAccountId(ClaimsPrincipal user) =>
        TryGetAccountId(user, out Guid accountId)
            ? accountId
            : throw new InvalidOperationException("Authenticated account claim is missing.");

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out accountId)
        && accountId != Guid.Empty;
}
