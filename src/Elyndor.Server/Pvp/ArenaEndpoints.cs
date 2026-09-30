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
        group.MapPost("/queue", JoinAsync);
        group.MapDelete("/queue", LeaveAsync);
        group.MapGet("/leaderboard", GetLeaderboardAsync);
        group.MapGet("/matches/{matchId:guid}", GetMatch);
        return endpoints;
    }

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
