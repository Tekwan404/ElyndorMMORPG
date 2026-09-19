using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Raids;
using Elyndor.Server.Combat;

namespace Elyndor.Server.Raids;

internal static class RaidCombatEndpoints
{
    public static RouteGroupBuilder MapRaidCombatEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/combat/encounters/{encounterId:guid}/start",
            StartAsync);
        return group;
    }

    private static async Task<IResult> StartAsync(
        Guid encounterId,
        ClaimsPrincipal user,
        RaidCombatApplicationService combat,
        IContentSnapshotProvider contentProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        CombatOperationResult result = await combat.StartAsync(
            accountId,
            encounterId,
            cancellationToken);
        CombatUpdateResponse response = CombatContractMapper.ToResponse(
            result,
            contentProvider.GetCurrent().Package);
        if (result.Succeeded)
            return Results.Ok(response);

        int status = result.ErrorCode switch
        {
            RaidCombatRosterErrorCodes.RaidNotFound
                => StatusCodes.Status404NotFound,
            RaidCombatRosterErrorCodes.NotLeader
                => StatusCodes.Status403Forbidden,
            RaidCombatRosterErrorCodes.CapacityExceeded
            or RaidCombatRosterErrorCodes.LeaderUnavailable
            or CombatErrorCodes.AlreadyActive
                => StatusCodes.Status409Conflict,
            CombatErrorCodes.InvalidEncounter
            or CombatErrorCodes.InvalidLocation
            or CombatErrorCodes.UnsupportedMonster
            or CombatErrorCodes.UnsupportedClass
            or CombatErrorCodes.AfkFarmActive
                => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status409Conflict
        };
        return Results.Json(response, statusCode: status);
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
