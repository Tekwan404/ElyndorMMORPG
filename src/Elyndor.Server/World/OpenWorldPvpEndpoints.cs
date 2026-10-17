using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.World;
using Elyndor.Server.Combat;

namespace Elyndor.Server.World;

public static class OpenWorldPvpEndpoints
{
    public sealed record EnterRequest(Guid TargetCharacterId, bool AttackPlayer);

    public static IEndpointRouteBuilder MapOpenWorldPvpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/world/pvp").RequireAuthorization().WithTags("World");
        group.MapGet("/players", async (ClaimsPrincipal user, HttpContext context,
            OpenWorldPvpService service, CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(user, out var accountId)) return Results.Unauthorized();
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.GetPlayersAsync(accountId, cancellationToken));
        });
        group.MapPost("/enter", async (EnterRequest request, ClaimsPrincipal user,
            OpenWorldPvpService service, IContentSnapshotProvider content, CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(user, out var accountId)) return Results.Unauthorized();
            var result = await service.EnterAsync(accountId, request.TargetCharacterId, request.AttackPlayer, cancellationToken);
            return Results.Ok(CombatContractMapper.ToResponse(result, content.GetCurrent().Package));
        });
        return endpoints;
    }

    private static bool TryGetAccount(ClaimsPrincipal user, out Guid accountId) => Guid.TryParse(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId);
}
