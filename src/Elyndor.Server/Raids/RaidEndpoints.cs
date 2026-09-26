using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Raids;
using Elyndor.Core.Raids;
using Elyndor.Infrastructure.Raids;

namespace Elyndor.Server.Raids;

public static class RaidEndpoints
{
    public static IEndpointRouteBuilder MapRaidEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/raid")
            .RequireAuthorization()
            .WithTags("Raid");
        group.AddEndpointFilter<RaidUpdateFilter>();

        group.MapGet("", GetAsync);
        group.MapGet("/invites", GetInvitesAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/invites", InviteAsync);
        group.MapPost("/invites/{inviteId:guid}/accept", AcceptInviteAsync);
        group.MapPost("/invites/{inviteId:guid}/decline", DeclineInviteAsync);
        group.MapPost("/leave", LeaveAsync);
        group.MapPost("/kick/{characterId:guid}", KickAsync);
        group.MapPost("/assistants/{characterId:guid}/promote", PromoteAssistantAsync);
        group.MapPost("/transfer/{characterId:guid}", TransferAsync);
        group.MapPost("/disband", DisbandAsync);
        group.MapPost("/ready-checks", BeginReadyCheckAsync);
        group.MapPost("/ready-checks/{readyCheckId:guid}/state", SetReadyStateAsync);
        group.MapRaidCombatEndpoints();
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        RaidSnapshot? snapshot = await service.GetAsync(accountId, cancellationToken);
        return snapshot is null ? Results.NoContent() : Results.Ok(ToResponse(snapshot));
    }

    private static async Task<IResult> GetInvitesAsync(
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return Results.Ok((await service.GetInvitesAsync(accountId, cancellationToken))
            .Select(ToResponse)
            .ToArray());
    }

    private static async Task<IResult> CreateAsync(
        CreateRaidRequest request,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToResult(await service.CreateAsync(accountId, request.RequestId, cancellationToken));
    }

    private static async Task<IResult> InviteAsync(
        InviteToRaidRequest request,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToResult(await service.InviteAsync(
            accountId,
            request.InviteId,
            request.TargetCharacterId,
            cancellationToken));
    }

    private static async Task<IResult> AcceptInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToResult(await service.AcceptInviteAsync(accountId, inviteId, cancellationToken));
    }

    private static async Task<IResult> DeclineInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToResult(await service.DeclineInviteAsync(accountId, inviteId, cancellationToken));
    }

    private static async Task<IResult> LeaveAsync(
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(user, () => service.LeaveAsync(GetAccountId(user), cancellationToken));

    private static async Task<IResult> KickAsync(
        Guid characterId,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(user, () => service.KickAsync(GetAccountId(user), characterId, cancellationToken));

    private static async Task<IResult> PromoteAssistantAsync(
        Guid characterId,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(user, () => service.PromoteAssistantAsync(GetAccountId(user), characterId, cancellationToken));

    private static async Task<IResult> TransferAsync(
        Guid characterId,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(user, () => service.TransferLeadershipAsync(GetAccountId(user), characterId, cancellationToken));

    private static async Task<IResult> DisbandAsync(
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(user, () => service.DisbandAsync(GetAccountId(user), cancellationToken));

    private static async Task<IResult> BeginReadyCheckAsync(
        BeginRaidReadyCheckRequest request,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(user, () => service.BeginReadyCheckAsync(
            GetAccountId(user),
            request.ReadyCheckId,
            cancellationToken));

    private static async Task<IResult> SetReadyStateAsync(
        Guid readyCheckId,
        SetRaidReadyStateRequest request,
        ClaimsPrincipal user,
        RaidService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        if (!Enum.TryParse(request.State, true, out RaidReadyState readyState)
            || readyState == RaidReadyState.NoResponse)
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?> { ["code"] = RaidErrorCodes.InvalidRequest });
        return ToResult(await service.SetReadyStateAsync(
            accountId,
            readyCheckId,
            readyState,
            cancellationToken));
    }

    private static async Task<IResult> ExecuteAsync(
        ClaimsPrincipal user,
        Func<Task<RaidOperationResult>> operation)
    {
        if (!TryGetAccountId(user, out _)) return Results.Unauthorized();
        return ToResult(await operation());
    }

    private static IResult ToResult(RaidOperationResult result)
    {
        if (result.IsSuccess)
        {
            if (result.Snapshot is not null) return Results.Ok(ToResponse(result.Snapshot));
            if (result.Invite is not null) return Results.Ok(ToResponse(result.Invite));
            return Results.Ok();
        }

        int status = result.ErrorCode switch
        {
            RaidErrorCodes.CharacterNotFound
            or RaidErrorCodes.RaidNotFound
            or RaidErrorCodes.InviteNotFound
            or RaidErrorCodes.ReadyCheckNotFound => StatusCodes.Status404NotFound,
            RaidErrorCodes.NotLeader
            or RaidErrorCodes.NotLeaderOrAssistant => StatusCodes.Status403Forbidden,
            RaidErrorCodes.AlreadyInGroupContext
            or RaidErrorCodes.InviteExpired
            or RaidErrorCodes.RaidFull
            or RaidErrorCodes.ReadyCheckExpired
            or RaidErrorCodes.IdempotencyConflict
            or RaidErrorCodes.InvalidState => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        return Results.Problem(statusCode: status, extensions: new Dictionary<string, object?>
        {
            ["code"] = result.ErrorCode
        });
    }

    private static RaidResponse ToResponse(RaidSnapshot snapshot) =>
        new(
            snapshot.RaidId,
            snapshot.LeaderCharacterId,
            snapshot.MaximumMembers,
            snapshot.State.ToString(),
            snapshot.Version,
            snapshot.Members.Select(member => new RaidMemberResponse(
                member.CharacterId,
                member.Name,
                member.Level,
                member.ClassId,
                member.Role.ToString(),
                member.ReadyState.ToString(),
                member.JoinedAtUtc)).ToArray(),
            snapshot.ReadyCheck is null ? null : new RaidReadyCheckResponse(
                snapshot.ReadyCheck.Id,
                snapshot.ReadyCheck.StartedByCharacterId,
                snapshot.ReadyCheck.State.ToString(),
                snapshot.ReadyCheck.StartedAtUtc,
                snapshot.ReadyCheck.ExpiresAtUtc,
                snapshot.ReadyCheck.CompletedAtUtc));

    private static RaidInviteResponse ToResponse(RaidInviteView invite) =>
        new(
            invite.Id,
            invite.RaidId,
            invite.InviterCharacterId,
            invite.TargetCharacterId,
            invite.Status.ToString(),
            invite.CreatedAtUtc,
            invite.ExpiresAtUtc);

    private static Guid GetAccountId(ClaimsPrincipal user) =>
        TryGetAccountId(user, out Guid accountId)
            ? accountId
            : throw new InvalidOperationException("Authenticated account claim is missing.");

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
