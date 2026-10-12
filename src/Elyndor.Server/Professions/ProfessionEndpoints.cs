using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
using Elyndor.Infrastructure.Characters;
using Elyndor.Core.Content;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Professions;

public static class ProfessionEndpoints
{
    public static IEndpointRouteBuilder MapProfessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/professions")
            .RequireAuthorization()
            .WithTags("Professions");

        group.MapGet("/", GetAsync);
        group.MapPost("/learn", LearnAsync);
        group.MapPost("/skinning", SkinAsync);
        group.MapPost("/craft", CraftAsync);
        group.MapPost("/gather", GatherAsync);
        group.MapPost("/forget", ForgetAsync);
        group.MapPost("/study", StudyAsync);
        group.MapPost("/disenchant", DisenchantAsync);
        group.MapPost("/enchant", EnchantAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        ProfessionService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();
        ProfessionStateSnapshot? state = await service.GetStateAsync(accountId, cancellationToken);
        return state is null
            ? Problem(ProfessionErrorCodes.CharacterNotFound, StatusCodes.Status404NotFound, httpContext)
            : Results.Ok(state);
    }

    private static async Task<IResult> LearnAsync(
        LearnProfessionRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ProfessionService service,
        GameDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        ProfessionMutationResult result = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            string lockKey = accountId.ToString("N");
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                cancellationToken);

            ProfessionMutationResult learnResult = await service.LearnAsync(
                accountId,
                request.ProfessionId,
                request.MutationId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return learnResult;
        });

        return ToResult(result, httpContext);
    }

    private static async Task<IResult> SkinAsync(
        SkinCorpseRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ProfessionService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();
        ProfessionMutationResult result = await service.SkinAsync(
            accountId,
            request.CombatSessionId,
            request.EnemyActorId,
            request.MutationId,
            cancellationToken);
        return ToResult(result, httpContext);
    }

    private static async Task<IResult> CraftAsync(
        CraftProfessionRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ProfessionService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();
        ProfessionMutationResult result = await service.CraftAsync(accountId, request.RecipeId, request.MutationId, cancellationToken);
        return ToResult(result, httpContext);
    }

    private static async Task<IResult> StudyAsync(CraftProfessionRequest request, ClaimsPrincipal user,
        HttpContext httpContext, ProfessionRecipeLearningService service, CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(accountId, async () =>
            ToResult(await service.LearnAsync(accountId, request.RecipeId, request.MutationId, cancellationToken), httpContext),
            () => Problem("combat_active", StatusCodes.Status409Conflict, httpContext), cancellationToken);
    }

    private static async Task<IResult> ForgetAsync(LearnProfessionRequest request, ClaimsPrincipal user,
        HttpContext httpContext, ProfessionService service, CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(accountId, async () =>
            ToResult(await service.ForgetAsync(accountId, request.ProfessionId, request.MutationId, cancellationToken), httpContext),
            () => Problem("combat_active", StatusCodes.Status409Conflict, httpContext), cancellationToken);
    }

    private static async Task<IResult> GatherAsync(GatherResourceRequest request, ClaimsPrincipal user,
        HttpContext httpContext, ResourceGatheringService service, ProfessionService professions, IContentSnapshotProvider contentProvider, CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(accountId, async () =>
            ToResult(contentProvider.GetCurrent().Package.ProfessionGatheringNodes?.Any(node => node.Id == request.NodeId) == true
                ? await professions.GatherAsync(accountId, request.NodeId, request.MutationId, cancellationToken)
                : await service.GatherAsync(accountId, request.NodeId, request.MutationId, cancellationToken), httpContext),
            () => Problem("combat_active", StatusCodes.Status409Conflict, httpContext),
            cancellationToken);
    }

    private static IResult ToResult(ProfessionMutationResult result, HttpContext httpContext)
    {
        if (result.IsSuccess)
            return Results.Ok(result);
        int status = result.ErrorCode switch
        {
            ProfessionErrorCodes.CharacterNotFound or ProfessionErrorCodes.CorpseNotFound or ProfessionErrorCodes.RecipeNotFound or "resource_not_found" => StatusCodes.Status404NotFound,
            ProfessionErrorCodes.ProfessionAlreadyLearned or ProfessionErrorCodes.ProfessionLimitReached or ProfessionErrorCodes.CorpseAlreadySkinned or ProfessionErrorCodes.IdempotencyConflict or "resource_unavailable" or "resource_travelling" or "resource_afk_active" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        return Problem(result.ErrorCode ?? "profession_operation_failed", status, httpContext);
    }

    private static async Task<IResult> DisenchantAsync(DisenchantProfessionRequest request, ClaimsPrincipal user,
        HttpContext httpContext, ProfessionService service, CharacterOperationGuard operationGuard, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(accountId, async () =>
            ToResult(await service.DisenchantAsync(accountId, request.ItemId, request.MutationId, cancellationToken), httpContext),
            () => Problem("combat_active", StatusCodes.Status409Conflict, httpContext), cancellationToken);
    }

    private static async Task<IResult> EnchantAsync(EnchantProfessionRequest request, ClaimsPrincipal user,
        HttpContext httpContext, ProfessionService service, CharacterOperationGuard operationGuard, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(accountId, async () =>
            ToResult(await service.EnchantAsync(accountId, request.ItemId, request.EnchantmentId, request.MutationId, cancellationToken), httpContext),
            () => Problem("combat_active", StatusCodes.Status409Conflict, httpContext), cancellationToken);
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;

    private static IResult Problem(string code, int statusCode, HttpContext httpContext) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = httpContext.TraceIdentifier
            });

    public sealed record LearnProfessionRequest(string ProfessionId, Guid MutationId);
    public sealed record SkinCorpseRequest(Guid CombatSessionId, Guid EnemyActorId, Guid MutationId);
    public sealed record CraftProfessionRequest(string RecipeId, Guid MutationId);
    public sealed record DisenchantProfessionRequest(Guid ItemId, Guid MutationId);
    public sealed record EnchantProfessionRequest(Guid ItemId, string EnchantmentId, Guid MutationId);
    public sealed record GatherResourceRequest(string NodeId, Guid MutationId);
}
