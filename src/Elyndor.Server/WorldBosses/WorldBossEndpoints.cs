using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.WorldBosses;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Combat;
using Elyndor.Server.Combat;
using Elyndor.Infrastructure.WorldBosses;

namespace Elyndor.Server.WorldBosses;

public static class WorldBossEndpoints
{
    public static IEndpointRouteBuilder MapWorldBossEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/world-boss")
            .RequireAuthorization()
            .WithTags("World Boss");

        group.MapGet("/active", GetActiveAsync);
        group.MapPost("/{spawnId:guid}/enter", EnterAsync);
        group.MapGet("/{spawnId:guid}/leaderboard", GetLeaderboardAsync);
        return endpoints;
    }

    private static async Task<IResult> GetLeaderboardAsync(
        Guid spawnId,
        ClaimsPrincipal user,
        HttpContext httpContext,
        WorldBossReadService readService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        WorldBossLeaderboardReadResult result = await readService.GetLeaderboardAsync(
            accountId,
            spawnId,
            cancellationToken);
        if (!result.CharacterFound || !result.SpawnFound)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.CharacterFound
                        ? WorldBossEnterErrorCodes.SpawnNotFound
                        : WorldBossEnterErrorCodes.CharacterNotFound,
                    ["correlationId"] = httpContext.TraceIdentifier
                });
        }

        return Results.Ok(new WorldBossLeaderboardResponse(
            result.SpawnId,
            result.Players.Select(entry => new WorldBossPersonalLeaderboardEntryResponse(
                entry.Rank,
                entry.CharacterId,
                entry.Name,
                entry.Damage)).ToArray(),
            result.Parties.Select(entry => new WorldBossPartyLeaderboardEntryResponse(
                entry.Rank,
                entry.PartyId,
                entry.LeaderName,
                entry.Damage)).ToArray(),
            result.PersonalRank,
            result.PersonalDamage,
            result.PartyId,
            result.PartyRank,
            result.PartyDamage));
    }

    private static async Task<IResult> EnterAsync(
        Guid spawnId,
        ClaimsPrincipal user,
        HttpContext httpContext,
        WorldBossEnterService enterService,
        IContentSnapshotProvider contentProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        WorldBossEnterResult result = await enterService.EnterAsync(
            accountId,
            spawnId,
            cancellationToken);
        if (result.Succeeded && result.Combat is { } combat)
        {
            return Results.Ok(CombatContractMapper.ToResponse(
                combat,
                contentProvider.GetCurrent().Package));
        }

        int statusCode = result.ErrorCode switch
        {
            WorldBossEnterErrorCodes.CharacterNotFound
                or WorldBossEnterErrorCodes.SpawnNotFound =>
                StatusCodes.Status404NotFound,
            WorldBossEnterErrorCodes.EncounterNotConfigured
                or WorldBossEnterErrorCodes.ContentVersionMismatch =>
                StatusCodes.Status503ServiceUnavailable,
            WorldBossEnterErrorCodes.Expired
                or WorldBossEnterErrorCodes.NotActive
                or Elyndor.Core.Combat.Sessions.CombatErrorCodes.AlreadyActive =>
                StatusCodes.Status409Conflict,
            Elyndor.Core.Combat.Sessions.CombatErrorCodes.InvalidLocation
                or Elyndor.Core.Combat.Sessions.CombatErrorCodes.CommandRejected =>
                StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status409Conflict
        };

        return Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.ErrorCode ?? "world_boss_enter_failed",
                ["correlationId"] = httpContext.TraceIdentifier
            });
    }

    private static async Task<IResult> GetActiveAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        WorldBossReadService readService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        WorldBossActiveReadResult result = await readService.GetActiveAsync(
            accountId,
            cancellationToken);
        if (!result.CharacterFound)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "character_not_found",
                    ["correlationId"] = httpContext.TraceIdentifier
                });
        }

        if (result.Active is not { } active)
            return Results.NoContent();

        return Results.Ok(new WorldBossActiveResponse(
            active.SpawnId,
            active.BossDefinitionId,
            active.Name,
            active.Level,
            active.CurrentHealth,
            active.MaxHealth,
            active.CurrentPhase,
            active.PhaseName,
            active.SpawnedAtUtc,
            active.ExpiresAtUtc,
            active.Participants,
            active.PersonalDamage,
            active.PartyDamage,
            active.ContentVersion,
            active.BalanceVersion));
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
