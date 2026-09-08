using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Parties;
using Elyndor.Core.Parties;
using Elyndor.Infrastructure.Parties;

namespace Elyndor.Server.Parties;

public static class PartyEndpoints
{
    public static IEndpointRouteBuilder MapPartyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/party")
            .RequireAuthorization()
            .WithTags("Party");

        group.MapGet("", GetAsync);
        group.MapGet("/invites", GetInvitesAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/invites", InviteAsync);
        group.MapPost("/invites/{inviteId:guid}/accept", AcceptInviteAsync);
        group.MapPost("/invites/{inviteId:guid}/decline", DeclineInviteAsync);
        group.MapPost("/leave", LeaveAsync);
        group.MapPost("/kick/{characterId:guid}", KickAsync);
        group.MapPost("/transfer/{characterId:guid}", TransferAsync);
        group.MapPost("/disband", DisbandAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        PartySnapshot? snapshot = await service.GetAsync(accountId, cancellationToken);
        return snapshot is null ? Results.NoContent() : Results.Ok(ToResponse(snapshot));
    }

    private static async Task<IResult> GetInvitesAsync(
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return Results.Ok((await service.GetInvitesAsync(accountId, cancellationToken))
            .Select(ToResponse)
            .ToArray());
    }

    private static async Task<IResult> CreateAsync(
        CreatePartyRequest request,
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToResult(await service.CreateAsync(accountId, request.RequestId, cancellationToken));
    }

    private static async Task<IResult> InviteAsync(
        InviteToPartyRequest request,
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        if (!Enum.TryParse(request.Mode, true, out PartyInviteMode mode))
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity);
        return ToResult(await service.InviteAsync(
            accountId,
            request.InviteId,
            request.TargetCharacterId,
            mode,
            cancellationToken));
    }

    private static async Task<IResult> AcceptInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToResult(await service.AcceptInviteAsync(accountId, inviteId, cancellationToken));
    }

    private static async Task<IResult> DeclineInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToResult(await service.DeclineInviteAsync(accountId, inviteId, cancellationToken));
    }

    private static async Task<IResult> LeaveAsync(
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken) =>
        await ExecuteMembershipAsync(user, () => service.LeaveAsync(GetAccountId(user), cancellationToken));

    private static async Task<IResult> KickAsync(
        Guid characterId,
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken) =>
        await ExecuteMembershipAsync(user, () => service.KickAsync(GetAccountId(user), characterId, cancellationToken));

    private static async Task<IResult> TransferAsync(
        Guid characterId,
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken) =>
        await ExecuteMembershipAsync(user, () => service.TransferLeadershipAsync(GetAccountId(user), characterId, cancellationToken));

    private static async Task<IResult> DisbandAsync(
        ClaimsPrincipal user,
        PartyService service,
        CancellationToken cancellationToken) =>
        await ExecuteMembershipAsync(user, () => service.DisbandAsync(GetAccountId(user), cancellationToken));

    private static async Task<IResult> ExecuteMembershipAsync(
        ClaimsPrincipal user,
        Func<Task<PartyOperationResult>> operation)
    {
        if (!TryGetAccountId(user, out _)) return Results.Unauthorized();
        return ToResult(await operation());
    }

    private static IResult ToResult(PartyOperationResult result)
    {
        if (result.IsSuccess)
        {
            if (result.Snapshot is not null) return Results.Ok(ToResponse(result.Snapshot));
            if (result.Invite is not null) return Results.Ok(ToResponse(result.Invite));
            return Results.Ok();
        }

        int status = result.ErrorCode switch
        {
            PartyErrorCodes.CharacterNotFound
            or PartyErrorCodes.PartyNotFound
            or PartyErrorCodes.InviteNotFound => StatusCodes.Status404NotFound,
            PartyErrorCodes.NotLeader => StatusCodes.Status403Forbidden,
            PartyErrorCodes.AlreadyInParty
            or PartyErrorCodes.PartyFull
            or PartyErrorCodes.InviteExpired
            or PartyErrorCodes.IdempotencyConflict
            or PartyErrorCodes.InvalidState => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        return Results.Problem(statusCode: status, extensions: new Dictionary<string, object?>
        {
            ["code"] = result.ErrorCode
        });
    }

    private static PartyResponse ToResponse(PartySnapshot snapshot) =>
        new(
            snapshot.PartyId,
            snapshot.LeaderCharacterId,
            snapshot.Version,
            snapshot.Members.Select(member => new PartyMemberResponse(
                member.CharacterId,
                member.Name,
                member.Level,
                member.ClassId,
                member.IsLeader,
                member.JoinedAtUtc)).ToArray());

    private static PartyInviteResponse ToResponse(PartyInviteView invite) =>
        new(
            invite.Id,
            invite.PartyId,
            invite.InviterCharacterId,
            invite.TargetCharacterId,
            invite.Mode.ToString(),
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
