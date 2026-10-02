using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.WorldBosses;
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
        return endpoints;
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
