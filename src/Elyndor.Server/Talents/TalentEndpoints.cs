using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Talents;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Talents;
using Elyndor.Infrastructure.Characters;

namespace Elyndor.Server.Talents;

public static class TalentEndpoints
{
    public static IEndpointRouteBuilder MapTalentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/talents")
            .RequireAuthorization().WithTags("Talents");
        group.MapGet("/", GetAsync);
        group.MapPost("/learn", LearnAsync);
        group.MapPost("/switch", SwitchAsync);
        group.MapPost("/reset", ResetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        HttpContext context,
        TalentService service,
        CancellationToken cancellationToken)
    {
        return TryGetAccountId(user, out Guid accountId)
            ? ToResult(await service.GetAsync(accountId, cancellationToken), context)
            : Results.Unauthorized();
    }

    private static async Task<IResult> LearnAsync(
        LearnTalentRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        TalentService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToResult(
                await service.LearnAsync(
                    accountId,
                    request.LoadoutId,
                    request.TalentId,
                    request.ExpectedStateVersion,
                    request.MutationId,
                    cancellationToken),
                context),
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> SwitchAsync(
        SwitchTalentLoadoutRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        TalentService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToResult(
                await service.SwitchAsync(
                    accountId,
                    request.LoadoutId,
                    request.ExpectedStateVersion,
                    request.MutationId,
                    cancellationToken),
                context),
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> ResetAsync(
        ResetTalentLoadoutRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        TalentService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToResult(
                await service.ResetAsync(
                    accountId,
                    request.LoadoutId,
                    request.ExpectedStateVersion,
                    request.MutationId,
                    cancellationToken),
                context),
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static IResult InCombatProblem(HttpContext context) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = CharacterOperationErrorCodes.InCombat,
                ["correlationId"] = context.TraceIdentifier
            });

    private static IResult ToResult(TalentOperationResult result, HttpContext context)
    {
        if (result.IsSuccess) return Results.Ok(ToResponse(result.Snapshot!));
        int status = result.ErrorCode switch
        {
            TalentErrorCodes.Conflict or TalentErrorCodes.MutationConflict =>
                StatusCodes.Status409Conflict,
            "character_not_found" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        return Results.Problem(
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.ErrorCode,
                ["correlationId"] = context.TraceIdentifier
            });
    }

    private static TalentSnapshotResponse ToResponse(TalentStateSnapshot snapshot)
    {
        IReadOnlyDictionary<string, int> activeRanks =
            snapshot.State.GetRanks(snapshot.State.ActiveLoadoutId);
        int earned =
            TalentRules.EarnedPoints(snapshot.Character.Level, snapshot.Tree.MaxSpendablePoints);
        return new TalentSnapshotResponse(
            snapshot.Tree.Id,
            snapshot.Tree.ClassId,
            snapshot.Tree.Version,
            snapshot.State.ActiveLoadoutId,
            snapshot.State.StateVersion,
            earned,
            Math.Max(0, earned - activeRanks.Values.Sum()),
            snapshot.Tree.Branches.Select(branch => new TalentBranchResponse(
                branch.Id,
                branch.Name,
                branch.Fantasy,
                branch.NodeCount)).ToArray(),
            snapshot.Tree.Nodes.Select(node => new TalentNodeResponse(
                node.Id,
                node.BranchId,
                node.Tier,
                node.RequiredSpentPoints,
                node.Name,
                node.EnglishName,
                node.MaxRank,
                node.Prerequisites.Select(item => new TalentPrerequisiteResponse(
                    item.TalentId,
                    item.RequiredRank)).ToArray(),
                node.Description,
                node.RequiredLevel,
                node.IconId,
                RuntimeStatus(node),
                node.Modifiers?.FirstOrDefault(modifier =>
                    modifier.Type == TalentModifierType.AbilityModifier
                    && modifier.Key == TalentModifierKeys.UnlockAbility)?.TargetId)).ToArray(),
            [
                new TalentLoadoutResponse(
                    TalentLoadoutIds.Loadout1,
                    snapshot.Loadout1Ranks,
                    snapshot.Loadout1Ranks.Values.Sum()),
                new TalentLoadoutResponse(
                    TalentLoadoutIds.Loadout2,
                    snapshot.Loadout2Ranks,
                    snapshot.Loadout2Ranks.Values.Sum())
            ]);
    }

    private static bool TryGetAccountId(
        ClaimsPrincipal user,
        out Guid accountId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out accountId)
        && accountId != Guid.Empty;

    private static string RuntimeStatus(TalentDefinition node)
    {
        bool supported = node.Modifiers?.Any(modifier =>
            modifier.RuntimeStatus == TalentModifierRuntimeStatus.Supported
            || BerserkerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
            || PyromancerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)) == true;
        bool deferred = node.Modifiers?.Any(modifier =>
            modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
            && !BerserkerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
            && !PyromancerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)) == true;
        return (supported, deferred) switch
        {
            (true, true) => "PARTIAL",
            (true, false) => "SUPPORTED",
            _ => "DEFERRED"
        };
    }
}
