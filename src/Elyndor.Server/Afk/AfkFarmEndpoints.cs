using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Afk;
using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Afk;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Afk;

public static class AfkFarmEndpoints
{
    public static IEndpointRouteBuilder MapAfkFarmEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/afk")
            .RequireAuthorization().WithTags("AFK");
        group.MapPost("/preview", PreviewAsync);
        group.MapPost("/start", StartAsync);
        group.MapGet("", GetAsync);
        group.MapGet("/targets", GetTargetsAsync);
        group.MapPost("/stop", StopAsync);
        return endpoints;
    }

    private static async Task<IResult> StartAsync(StartAfkFarmRequest request, ClaimsPrincipal user,
        HttpContext context, AfkFarmService service, CancellationToken cancellationToken)
    {
        if (!TryAccount(user, out Guid accountId)) return Results.Unauthorized();
        if (request.DurationMinutes <= 0)
            return Problem("afk_invalid_request", StatusCodes.Status422UnprocessableEntity, context);
        AfkFarmMutationResult result = await service.StartAsync(accountId, request.LocationId, request.TargetMonsterId,
            TimeSpan.FromMinutes(request.DurationMinutes), cancellationToken);
        return result.Succeeded
            ? Results.Ok(ToState(result.Session!, []))
            : Problem(result.ErrorCode!, StatusCodes.Status409Conflict, context);
    }

    private static async Task<IResult> PreviewAsync(PreviewAfkFarmRequest request, ClaimsPrincipal user,
        HttpContext context, AfkFarmService service, CancellationToken cancellationToken)
    {
        if (!TryAccount(user, out Guid accountId)) return Results.Unauthorized();
        if (request.DurationMinutes <= 0)
            return Problem("afk_invalid_request", StatusCodes.Status422UnprocessableEntity, context);
        AfkFarmPreviewResult result = await service.PreviewAsync(accountId, request.LocationId, request.TargetMonsterId,
            TimeSpan.FromMinutes(request.DurationMinutes), cancellationToken);
        if (!result.Succeeded)
            return Problem(result.ErrorCode!, StatusCodes.Status409Conflict, context);

        AfkFarmSimulationResult simulation = result.Simulation!;
        return Results.Ok(new AfkFarmPreviewResponse(
            request.LocationId,
            request.TargetMonsterId,
            request.DurationMinutes,
            simulation.EncounteredEnemies,
            simulation.Kills,
            simulation.FailedKills,
            result.EstimatedXp,
            result.EstimatedGold,
            simulation.LootCandidates.Count,
            simulation.EfficiencyPercent));
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, HttpContext context,
        AfkFarmProgressService progress, GameDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!TryAccount(user, out Guid accountId)) return Results.Unauthorized();
        AfkFarmProgressResult result = await progress.ProcessAsync(accountId, cancellationToken);
        if (result.Session is null)
            return Problem(result.ErrorCode ?? AfkFarmErrorCodes.NoSession, StatusCodes.Status404NotFound, context);
        AfkFarmIntervalGrant[] grants = await dbContext.AfkFarmIntervalGrants.AsNoTracking()
            .Where(grant => grant.SessionId == result.Session.Id).ToArrayAsync(cancellationToken);
        return Results.Ok(ToState(result.Session, grants));
    }

    private static async Task<IResult> StopAsync(ClaimsPrincipal user, HttpContext context,
        AfkFarmService service, CancellationToken cancellationToken)
    {
        if (!TryAccount(user, out Guid accountId)) return Results.Unauthorized();
        AfkFarmMutationResult result = await service.StopAsync(accountId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(ToState(result.Session!, []))
            : Problem(result.ErrorCode!, StatusCodes.Status404NotFound, context);
    }

    private static async Task<IResult> GetTargetsAsync(ClaimsPrincipal user, GameDbContext dbContext,
        IContentSnapshotProvider contentProvider, CancellationToken cancellationToken)
    {
        if (!TryAccount(user, out Guid accountId)) return Results.Unauthorized();
        Guid? characterId = await dbContext.Characters.AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (characterId is null) return Results.Ok(Array.Empty<AfkFarmTargetResponse>());
        string? locationId = await dbContext.CharacterLocations.AsNoTracking()
            .Where(location => location.CharacterId == characterId.Value)
            .Select(location => location.LocationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (locationId is null || !contentProvider.GetCurrent().Indexes.LocationsById.TryGetValue(locationId, out var location))
            return Results.Ok(Array.Empty<AfkFarmTargetResponse>());
        var targets = (location.Encounters ?? [])
            .Where(encounter => contentProvider.GetCurrent().Indexes.MonstersById.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster)
                && monster.Rank == MonsterRank.Normal)
            .Select(encounter => contentProvider.GetCurrent().Indexes.MonstersById[encounter.MonsterId])
            .DistinctBy(monster => monster.Id)
            .OrderBy(monster => monster.DisplayName, StringComparer.Ordinal)
            .Select(monster => new AfkFarmTargetResponse(monster.Id, monster.DisplayName ?? monster.Id))
            .ToArray();
        return Results.Ok(targets);
    }

    private static AfkFarmStateResponse ToState(AfkFarmSession session,
        IReadOnlyList<AfkFarmIntervalGrant> grants) => new(
        session.Id, session.LocationId, session.TargetMonsterId, session.Status.ToString(),
        session.StartedAtUtc, session.EndsAtUtc, session.LastProcessedAtUtc, session.CompletedAtUtc,
        session.StopReason, grants.Sum(grant => grant.Kills), grants.Sum(grant => grant.XpEarned),
        grants.Sum(grant => grant.GoldEarned), grants.Sum(grant =>
            System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<Elyndor.Core.Items.LootRoll>>(grant.LootJson)
                ?.Sum(loot => loot.Quantity) ?? 0),
        0);

    private static bool TryAccount(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;

    private static IResult Problem(string code, int status, HttpContext context) => Results.Problem(
        statusCode: status, extensions: new Dictionary<string, object?>
        { ["code"] = code, ["correlationId"] = context.TraceIdentifier });
}
