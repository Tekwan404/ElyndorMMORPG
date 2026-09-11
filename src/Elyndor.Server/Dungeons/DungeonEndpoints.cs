using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Dungeons;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Dungeons;

public static class DungeonEndpoints
{
    public static IEndpointRouteBuilder MapDungeonEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/dungeons")
            .RequireAuthorization()
            .WithTags("Dungeons");
        group.MapGet("", GetPreviews);
        group.MapGet("/current", GetCurrentAsync);
        group.MapPost("/teleport", TeleportAsync);
        group.MapPost("/runs", CreateAsync);
        group.MapPost("/runs/{runId:guid}/enter", EnterAsync);
        group.MapPost("/runs/{runId:guid}/restart", RestartAsync);
        group.MapPost("/runs/{runId:guid}/exit", ExitAsync);
        return endpoints;
    }

    private static IResult GetPreviews(DungeonService service) =>
        Results.Ok(service.GetDefinitions().Select(definition => new DungeonPreviewResponse(
            definition.Id,
            definition.DisplayName,
            definition.Description,
            definition.MinimumLevel,
            definition.MaximumLevel,
            definition.EntryLocationId,
            definition.MinimumPartySize,
            definition.MaximumPartySize,
            definition.Encounters.Select(encounter => new DungeonEncounterPreviewResponse(
                encounter.Id,
                encounter.MonsterId,
                encounter.CheckpointId,
                encounter.IsBoss)).ToArray())).ToArray());

    private static async Task<IResult> GetCurrentAsync(
        ClaimsPrincipal user,
        DungeonService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        DungeonRunView? run = await service.GetCurrentAsync(accountId, cancellationToken);
        return run is null ? Results.NoContent() : Results.Ok(ToResponse(run));
    }

    private static async Task<IResult> CreateAsync(
        CreateDungeonRunRequest request,
        ClaimsPrincipal user,
        DungeonService service,
        PartyService partyService,
        BootstrapService bootstrapService,
        GameDbContext dbContext,
        ICombatActivityReader combatActivity,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();

        DungeonTeleportResult prepared = await TeleportPartyToEntryAsync(
            accountId,
            request.DungeonId,
            request.RequestId,
            service,
            partyService,
            bootstrapService,
            dbContext,
            combatActivity,
            cancellationToken);
        if (!prepared.Succeeded)
            return ToTeleportResult(prepared);

        return ToResult(await service.CreateAsync(
            accountId,
            request.DungeonId,
            request.RequestId,
            cancellationToken));
    }

    private static async Task<IResult> TeleportAsync(
        TeleportToDungeonRequest request,
        ClaimsPrincipal user,
        DungeonService service,
        PartyService partyService,
        BootstrapService bootstrapService,
        GameDbContext dbContext,
        ICombatActivityReader combatActivity,
        CharacterOperationGuard operationGuard,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToTeleportResult(await TeleportPartyToEntryAsync(
                accountId,
                request.DungeonId,
                request.RequestId,
                service,
                partyService,
                bootstrapService,
                dbContext,
                combatActivity,
                cancellationToken)),
            () => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = CharacterOperationErrorCodes.InCombat,
                    ["correlationId"] = httpContext.TraceIdentifier
                }),
            cancellationToken);
    }

    private static async Task<DungeonTeleportResult> TeleportPartyToEntryAsync(
        Guid accountId,
        string dungeonId,
        Guid requestId,
        DungeonService service,
        PartyService partyService,
        BootstrapService bootstrapService,
        GameDbContext dbContext,
        ICombatActivityReader combatActivity,
        CancellationToken cancellationToken)
    {
        var definition = service.GetDefinition(dungeonId);
        if (definition is null)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.DungeonNotFound);

        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        if (party is null)
        {
            await bootstrapService.GetAsync(accountId, cancellationToken, checkpoint: true);
            return await service.TeleportToEntryAsync(accountId, dungeonId, requestId, cancellationToken);
        }

        Guid[] memberIds = party.Members
            .Select(member => member.CharacterId)
            .Distinct()
            .ToArray();
        var members = await dbContext.Characters
            .AsNoTracking()
            .Where(character => memberIds.Contains(character.Id))
            .Select(character => new { character.Id, character.AccountId, character.Level })
            .ToArrayAsync(cancellationToken);
        var caller = members.SingleOrDefault(member => member.AccountId == accountId);
        if (caller is null)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.CharacterNotFound);
        if (party.LeaderCharacterId != caller.Id)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.NotLeader);

        if (members.Length != memberIds.Length)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.CharacterNotFound);
        if (members.Any(member => member.Level < definition.MinimumLevel))
            return DungeonTeleportResult.Failure(DungeonErrorCodes.LevelRequired);
        if (members.Any(member => combatActivity.HasActiveCombat(member.AccountId)))
            return DungeonTeleportResult.Failure(CharacterOperationErrorCodes.InCombat);

        Guid[] activeTravelMemberIds = await dbContext.CharacterTravelStates
            .AsNoTracking()
            .Where(travel => memberIds.Contains(travel.CharacterId))
            .Select(travel => travel.CharacterId)
            .ToArrayAsync(cancellationToken);
        if (activeTravelMemberIds.Length != 0)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.TravelInProgress);

        foreach (var member in members)
        {
            await bootstrapService.GetAsync(member.AccountId, cancellationToken, checkpoint: true);
        }

        int healthyMemberCount = await dbContext.CharacterVitals
            .AsNoTracking()
            .CountAsync(vitals => memberIds.Contains(vitals.CharacterId) && vitals.CurrentHp > 0, cancellationToken);
        if (healthyMemberCount != memberIds.Length)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.MemberCannotEnter);

        DungeonTeleportResult? callerResult = null;
        foreach (var member in members.OrderBy(member => member.Id == caller.Id ? 1 : 0))
        {
            DungeonTeleportResult result = await service.TeleportToEntryAsync(
                member.AccountId,
                dungeonId,
                requestId,
                cancellationToken);
            if (!result.Succeeded)
                return result;
            if (member.Id == caller.Id)
                callerResult = result;
        }

        return callerResult ?? DungeonTeleportResult.Failure(DungeonErrorCodes.CharacterNotFound);
    }

    private static async Task<IResult> EnterAsync(
        Guid runId,
        ClaimsPrincipal user,
        DungeonService service,
        DungeonNavigationService navigationService,
        CharacterOperationGuard operationGuard,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                DungeonNavigationResult canEnter = await navigationService.CanEnterAsync(
                    accountId,
                    runId,
                    cancellationToken);
                return canEnter.Succeeded
                    ? ToResult(await service.EnterAsync(accountId, runId, cancellationToken))
                    : ToNavigationResult(canEnter);
            },
            () => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = CharacterOperationErrorCodes.InCombat,
                    ["correlationId"] = httpContext.TraceIdentifier
                }),
            cancellationToken);
    }

    private static async Task<IResult> RestartAsync(
        Guid runId,
        ClaimsPrincipal user,
        DungeonService service,
        CharacterOperationGuard operationGuard,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToResult(await service.RestartEncounterAsync(
                accountId,
                runId,
                cancellationToken)),
            () => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = CharacterOperationErrorCodes.InCombat,
                    ["correlationId"] = httpContext.TraceIdentifier
                }),
            cancellationToken);
    }

    private static async Task<IResult> ExitAsync(
        Guid runId,
        ClaimsPrincipal user,
        DungeonNavigationService navigationService,
        CharacterOperationGuard operationGuard,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToNavigationResult(await navigationService.ExitAsync(
                accountId,
                runId,
                cancellationToken)),
            () => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = CharacterOperationErrorCodes.InCombat,
                    ["correlationId"] = httpContext.TraceIdentifier
                }),
            cancellationToken);
    }

    private static IResult ToResult(DungeonOperationResult result) =>
        result.Succeeded
            ? Results.Ok(result.Run is null ? null : ToResponse(result.Run))
            : Results.Problem(
                statusCode: result.ErrorCode is DungeonErrorCodes.NotLeader
                    or DungeonErrorCodes.LevelRequired
                    or DungeonErrorCodes.InvalidLocation
                        ? StatusCodes.Status403Forbidden
                        : result.ErrorCode is DungeonErrorCodes.RunNotFound
                            or DungeonErrorCodes.DungeonNotFound
                            or DungeonErrorCodes.CharacterNotFound
                            or DungeonErrorCodes.MemberNotInRun
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });

    private static IResult ToNavigationResult(DungeonNavigationResult result) =>
        result.Succeeded
            ? Results.Ok(new
            {
                locationId = result.LocationId,
                locationVersion = result.LocationVersion
            })
            : Results.Problem(
                statusCode: result.ErrorCode is DungeonErrorCodes.NotLeader
                    or DungeonErrorCodes.LevelRequired
                    or DungeonErrorCodes.InvalidLocation
                        ? StatusCodes.Status403Forbidden
                        : result.ErrorCode is DungeonErrorCodes.RunNotFound
                            or DungeonErrorCodes.DungeonNotFound
                            or DungeonErrorCodes.CharacterNotFound
                            or DungeonErrorCodes.MemberNotInRun
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });

    private static IResult ToTeleportResult(DungeonTeleportResult result) =>
        result.Succeeded
            ? Results.Ok(new DungeonTeleportResponse(
                result.DungeonId!,
                result.LocationId!,
                result.LocationVersion!.Value))
            : Results.Problem(
                statusCode: result.ErrorCode is DungeonErrorCodes.NotLeader
                    or DungeonErrorCodes.LevelRequired
                    ? StatusCodes.Status403Forbidden
                    : result.ErrorCode is DungeonErrorCodes.DungeonNotFound
                        or DungeonErrorCodes.CharacterNotFound
                            ? StatusCodes.Status404NotFound
                            : StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });

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

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
