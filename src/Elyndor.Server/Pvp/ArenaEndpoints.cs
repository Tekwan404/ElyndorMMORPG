using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Arena;
using Elyndor.Core.Content;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Pvp;

namespace Elyndor.Server.Pvp;

public static class ArenaEndpoints
{
    public static IEndpointRouteBuilder MapArenaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/arena")
            .RequireAuthorization()
            .WithTags("Arena");

        group.MapGet("/status", GetStatusAsync);
        group.MapGet("/shop", GetShopAsync);
        group.MapPost("/shop/purchases", PurchaseFromShopAsync);
        group.MapPost("/queue", JoinAsync);
        group.MapDelete("/queue", LeaveAsync);
        group.MapGet("/leaderboard", GetLeaderboardAsync);
        group.MapGet("/matches/{matchId:guid}", GetMatch);
        group.MapGet("/invites", GetInvitesAsync);
        group.MapPost("/invites", InviteAsync);
        group.MapPost("/invites/{inviteId:guid}", RespondToInviteAsync);
        return endpoints;
    }

    private static async Task<IResult> GetInvitesAsync(ClaimsPrincipal user, ArenaInvitationService invites,
        ArenaLobbyService lobby, HttpContext http, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        if (!lobby.Enabled) return Problem("arena_disabled", 404, http);
        return Results.Ok((await invites.ListAsync(accountId, cancellationToken)).Select(ToInvitation));
    }

    private static async Task<IResult> InviteAsync(ArenaInviteRequest request, ClaimsPrincipal user,
        ArenaInvitationService invites, ArenaInvitationNotifier notifier, HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        ArenaInvitationResult result = await invites.InviteAsync(accountId, request.RequestId, request.TargetName, cancellationToken);
        if (result.Succeeded && result.Created && result.Invitation is { } invite)
            await notifier.NotifyAsync(invite, sendTelegram: true);
        return InvitationResult(result, http);
    }

    private static async Task<IResult> RespondToInviteAsync(Guid inviteId, ArenaInviteActionRequest request,
        ClaimsPrincipal user, ArenaInvitationService invites, ArenaInvitationNotifier notifier,
        HttpContext http, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        ArenaInvitationResult result = await invites.RespondAsync(accountId, inviteId, request.Action, cancellationToken);
        if (result.Succeeded && result.Created && result.Invitation is { } invite)
            await notifier.NotifyAsync(invite, sendTelegram: false);
        return InvitationResult(result, http);
    }

    private static IResult InvitationResult(ArenaInvitationResult result, HttpContext http) =>
        result.Succeeded && result.Invitation is { } invite ? Results.Ok(ToInvitation(invite))
            : Problem(result.ErrorCode ?? "arena_invite_failed",
                result.ErrorCode is "arena_invite_invalid" ? 400
                    : result.ErrorCode is "arena_invite_not_found" or "arena_invite_player_not_found" or "arena_disabled" ? 404 : 409, http);

    private static ArenaInvitationResponse ToInvitation(ArenaInvitationView invite) =>
        new(invite.Id, invite.InviterCharacterId, invite.InviterName, invite.TargetCharacterId, invite.TargetName,
            invite.Status.ToString(), invite.ExpiresAtUtc, invite.MatchId, invite.Incoming);

    private static async Task<IResult> GetStatusAsync(ClaimsPrincipal user, ArenaLobbyService lobby,
        HttpContext http, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        if (!lobby.Enabled) return Results.Ok(ArenaContractMapper.Disabled());
        ArenaLobbyStatus? status = await lobby.StatusAsync(accountId, cancellationToken);
        return status is null
            ? Problem("character_not_found", StatusCodes.Status404NotFound, http)
            : Results.Ok(ArenaContractMapper.ToResponse(status, enabled: true));
    }

    private static async Task<IResult> GetShopAsync(
        ClaimsPrincipal user,
        ArenaHonorShopService shop,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        ArenaHonorShopOperationResult result = await shop.GetAsync(accountId, cancellationToken);
        return result.Succeeded && result.Snapshot is { } snapshot
            ? Results.Ok(ToShopResponse(snapshot))
            : ShopProblem(result.ErrorCode ?? "arena_shop_unavailable", http);
    }

    private static async Task<IResult> PurchaseFromShopAsync(
        ArenaHonorShopPurchaseRequest request,
        ClaimsPrincipal user,
        ArenaHonorShopService shop,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        ArenaHonorShopOperationResult result = await shop.BuyAsync(
            accountId,
            request.ItemId,
            request.MutationId,
            cancellationToken);

        return result.Succeeded && result.Snapshot is { } snapshot
            ? Results.Ok(new ArenaHonorShopPurchaseResponse(
                true,
                null,
                ToShopResponse(snapshot)))
            : ShopProblem(result.ErrorCode ?? "arena_shop_unavailable", http);
    }

    private static ArenaHonorShopResponse ToShopResponse(ArenaHonorShopSnapshot snapshot) =>
        new(
            snapshot.Honor,
            snapshot.Items.Select(item => new ArenaHonorShopItemResponse(
                item.Id,
                item.Name,
                item.Rarity.ToString(),
                item.Slot?.ToString(),
                item.IconId,
                item.SetId,
                item.RequiredLevel,
                item.HonorPrice)).ToArray());

    private static IResult ShopProblem(string code, HttpContext context)
    {
        int statusCode = code switch
        {
            ArenaHonorShopErrorCodes.CharacterNotFound or ArenaHonorShopErrorCodes.ItemNotFound =>
                StatusCodes.Status404NotFound,
            ArenaHonorShopErrorCodes.InvalidMutationId or ArenaHonorShopErrorCodes.ItemNotForSale =>
                StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status409Conflict
        };
        return Problem(code, statusCode, context);
    }

    private static async Task<IResult> JoinAsync(ArenaQueueRequest? request, ClaimsPrincipal user,
        ArenaLobbyService lobby, HttpContext http, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        if (!Enum.TryParse(request?.Mode ?? nameof(ArenaQueueMode.Ranked), ignoreCase: true, out ArenaQueueMode mode))
            return Problem("arena_queue_mode_invalid", StatusCodes.Status400BadRequest, http);
        ArenaQueueMutationResult result = await lobby.JoinAsync(accountId, mode, cancellationToken);
        return await ToQueueResultAsync(result, accountId, lobby, http, cancellationToken);
    }

    private static async Task<IResult> LeaveAsync(ClaimsPrincipal user, ArenaLobbyService lobby,
        HttpContext http, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        ArenaQueueMutationResult result = await lobby.LeaveAsync(accountId, cancellationToken);
        return await ToQueueResultAsync(result, accountId, lobby, http, cancellationToken);
    }

    private static async Task<IResult> GetLeaderboardAsync(ArenaLobbyService lobby, ArenaReadService read,
        CancellationToken cancellationToken)
    {
        if (!lobby.Enabled) return Results.Ok(Array.Empty<ArenaLeaderboardEntryResponse>());
        List<ArenaLeaderboardEntry> entries = await read.LeaderboardAsync(cancellationToken);
        return Results.Ok(entries.Select((entry, index) => new ArenaLeaderboardEntryResponse(index + 1,
            entry.CharacterId, entry.Name, entry.Rating, entry.Wins, entry.Losses, entry.Draws)).ToArray());
    }

    private static IResult GetMatch(Guid matchId, long? afterSequence, ClaimsPrincipal user,
        ArenaLobbyService lobby, ArenaMatchRuntime runtime, IContentSnapshotProvider content, HttpContext http)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        if (!lobby.Enabled) return Problem("arena_disabled", StatusCodes.Status404NotFound, http);
        ArenaTestState? state = runtime.GetState(accountId, matchId, Math.Max(0, afterSequence ?? 0));
        return state is null
            ? Problem("arena_match_not_found", StatusCodes.Status404NotFound, http)
            : Results.Ok(ArenaContractMapper.ToResponse(state, content.GetCurrent().Package));
    }

    private static async Task<IResult> ToQueueResultAsync(ArenaQueueMutationResult result, Guid accountId,
        ArenaLobbyService lobby, HttpContext http, CancellationToken cancellationToken)
    {
        if (!result.Succeeded)
        {
            int statusCode = result.ErrorCode switch
            {
                "arena_disabled" or "character_not_found" => StatusCodes.Status404NotFound,
                "arena_queue_mode_invalid" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status409Conflict
            };
            return Problem(result.ErrorCode ?? "arena_unavailable", statusCode, http);
        }

        ArenaLobbyStatus? status = await lobby.StatusAsync(accountId, cancellationToken);
        return Results.Ok(new ArenaQueueResponse(true, null,
            status is null ? null : ArenaContractMapper.ToResponse(status, enabled: true)));
    }

    private static IResult Problem(string code, int statusCode, HttpContext context) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier
            });

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
