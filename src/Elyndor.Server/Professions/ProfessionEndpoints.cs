using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        string lockKey = accountId.ToString("N");
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);

        ProfessionMutationResult result = await service.LearnAsync(
            accountId,
            request.ProfessionId,
            request.MutationId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    private static IResult ToResult(ProfessionMutationResult result, HttpContext httpContext)
    {
        if (result.IsSuccess)
            return Results.Ok(result);
        int status = result.ErrorCode switch
        {
            ProfessionErrorCodes.CharacterNotFound or ProfessionErrorCodes.CorpseNotFound or ProfessionErrorCodes.RecipeNotFound => StatusCodes.Status404NotFound,
            ProfessionErrorCodes.ProfessionAlreadyLearned or ProfessionErrorCodes.ProfessionLimitReached or ProfessionErrorCodes.CorpseAlreadySkinned or ProfessionErrorCodes.IdempotencyConflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        return Problem(result.ErrorCode ?? "profession_operation_failed", status, httpContext);
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
}
