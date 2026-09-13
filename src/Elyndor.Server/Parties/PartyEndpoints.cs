using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Dungeons;
using Elyndor.Contracts.Parties;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Parties;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Parties;

public static class PartyEndpoints
{
    public static IEndpointRouteBuilder MapPartyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/party")
            .RequireAuthorization()
            .WithTags("Party");
        group.AddEndpointFilter<PartyUpdateFilter>();

        group.MapGet("", GetAsync);
        group.MapGet("/state", GetStateAsync);
        group.MapGet("/invites", GetInvitesAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/invites", InviteAsync);
        group.MapPost("/invites/{inviteId:guid}/accept", AcceptInviteAsync);
        group.MapPost("/invites/{inviteId:guid}/decline", DeclineInviteAsync);
        group.MapPost("/leave", LeaveAsync);
        group.MapPost("/kick/{characterId:guid}", KickAsync);
        group.MapPost("/transfer/{characterId:guid}", TransferAsync);
        group.MapPost("/disband", DisbandAsync);
        group.MapPost("/dungeon-runs/{runId:guid}/return-to-town", ReturnToTownAsync);
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

    private static async Task<IResult> GetStateAsync(
        ClaimsPrincipal user,
        PartyService partyService,
        DungeonService dungeonService,
        DungeonNavigationService navigationService,
        GameDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();

        Guid? characterId = await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!characterId.HasValue)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = PartyErrorCodes.CharacterNotFound
                });
        }

        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        string? locationId = await dbContext.CharacterLocations
            .AsNoTracking()
            .Where(location => location.CharacterId == characterId.Value)
            .Select(location => location.LocationId)
            .SingleOrDefaultAsync(cancellationToken);

        DungeonRunResponse? currentRun = null;
        if (party?.ActiveDungeonRunId is Guid activeRunId)
        {
            DungeonRun? run = await dbContext.DungeonRuns
                .AsNoTracking()
                .Include(candidate => candidate.Members)
                .Include(candidate => candidate.Encounters)
                    .ThenInclude(encounter => encounter.Members)
                .SingleOrDefaultAsync(candidate => candidate.Id == activeRunId
                    && candidate.PartyId == party.PartyId, cancellationToken);

            if (run is not null && dungeonService.GetDefinition(run.DungeonId) is DungeonDefinition definition)
                currentRun = ToResponse(run, definition);
        }

        if (currentRun is null)
        {
            DungeonRunView? current = await dungeonService.GetCurrentAsync(accountId, cancellationToken);
            if (current is not null)
                currentRun = ToResponse(current);
        }

        DungeonRunMemberResponse? currentMember = currentRun?.Members
            .SingleOrDefault(member => member.CharacterId == characterId.Value);
        bool hasActiveRun = currentRun is not null
            && string.Equals(currentRun.State, DungeonRunState.Active.ToString(), StringComparison.Ordinal);
        bool needsEntry = hasActiveRun
            && currentMember?.State != DungeonRunMemberState.Active.ToString();
        bool canEnter = hasActiveRun
            && currentMember?.State == DungeonRunMemberState.Active.ToString();
        string? enterBlockedReason = null;

        if (hasActiveRun && !canEnter)
        {
            DungeonNavigationResult eligibility = await navigationService.CanEnterAsync(
                accountId,
                currentRun!.RunId,
                cancellationToken);
            canEnter = eligibility.Succeeded;
            enterBlockedReason = eligibility.ErrorCode;
        }

        return Results.Ok(new PartyDungeonStateResponse(
            party is null ? null : ToResponse(party),
            currentRun,
            characterId.Value,
            locationId,
            needsEntry,
            canEnter,
            enterBlockedReason));
    }

    private static async Task<IResult> ReturnToTownAsync(
        Guid runId,
        ClaimsPrincipal user,
        DungeonNavigationService navigationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return ToNavigationResult(await navigationService.ReturnToTownAsync(
            accountId,
            runId,
            cancellationToken));
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

    private static IResult ToNavigationResult(DungeonNavigationResult result) =>
        result.Succeeded
            ? Results.Ok(new
            {
                locationId = result.LocationId,
                locationVersion = result.LocationVersion
            })
            : Results.Problem(
                statusCode: result.ErrorCode is DungeonErrorCodes.RunNotFound
                    or DungeonErrorCodes.CharacterNotFound
                    or DungeonErrorCodes.MemberNotInRun
                        ? StatusCodes.Status404NotFound
                        : StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.ErrorCode
                });

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
                member.JoinedAtUtc,
                member.LocationId,
                member.ActiveDungeonRunId)).ToArray(),
            snapshot.ActiveDungeonRunId);

    private static DungeonRunResponse ToResponse(DungeonRunView run) =>
        new(
            run.RunId,
            run.DungeonId,
            run.DisplayName,
            run.Description,
            run.State.ToString(),
            run.CurrentEncounterIndex,
            run.CurrentCheckpointId,
            run.EncounterCount,
            run.PartyId,
            run.Members.Select(member => new DungeonRunMemberResponse(
                member.CharacterId,
                member.State.ToString(),
                member.JoinedAtUtc)).ToArray(),
            run.Encounters.Select(encounter => new DungeonEncounterResponse(
                encounter.EncounterId,
                encounter.EncounterIndex,
                encounter.MonsterId,
                encounter.State.ToString(),
                encounter.WipeCount,
                encounter.CharacterIds)).ToArray());

    private static DungeonRunResponse ToResponse(DungeonRun run, DungeonDefinition definition)
    {
        string checkpointId = definition.Encounters.Count == 0
            ? string.Empty
            : definition.Encounters[Math.Clamp(
                run.CurrentEncounterIndex,
                0,
                definition.Encounters.Count - 1)].CheckpointId;

        return new DungeonRunResponse(
            run.Id,
            run.DungeonId,
            definition.DisplayName,
            definition.Description,
            run.State.ToString(),
            run.CurrentEncounterIndex,
            checkpointId,
            definition.Encounters.Count,
            run.PartyId,
            run.Members
                .OrderBy(member => member.JoinedAtUtc)
                .Select(member => new DungeonRunMemberResponse(
                    member.CharacterId,
                    member.State.ToString(),
                    member.JoinedAtUtc))
                .ToArray(),
            run.Encounters
                .OrderBy(encounter => encounter.EncounterIndex)
                .Select(encounter => new DungeonEncounterResponse(
                    encounter.Id,
                    encounter.EncounterIndex,
                    encounter.MonsterId,
                    encounter.State.ToString(),
                    encounter.WipeCount,
                    encounter.Members.Select(member => member.CharacterId).ToArray()))
                .ToArray());
    }

    private static PartyInviteResponse ToResponse(PartyInviteView invite) =>
        new(
            invite.Id,
            invite.PartyId,
            invite.InviterCharacterId,
            invite.TargetCharacterId,
            invite.Mode.ToString(),
            invite.Status.ToString(),
            invite.CreatedAtUtc,
            invite.ExpiresAtUtc,
            invite.InviterName);

    private static Guid GetAccountId(ClaimsPrincipal user) =>
        TryGetAccountId(user, out Guid accountId)
            ? accountId
            : throw new InvalidOperationException("Authenticated account claim is missing.");

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
