using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Talents;

namespace Elyndor.Server.Talents;

public static class TalentEndpoints
{
    public static IEndpointRouteBuilder MapTalentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1")
            .RequireAuthorization()
            .WithTags("Talents");

        group.MapGet("/talents/tree", GetTalentTreeAsync);
        group.MapGet("/character/talents", GetCharacterTalentsAsync);
        group.MapPost("/character/talents/allocate", AllocateTalentAsync);

        return endpoints;
    }

    private static async Task<IResult> GetTalentTreeAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        TalentAllocationService talentService,
        CancellationToken cancellationToken)
    {
        if (!TryGetCharacterId(user, out Guid characterId))
        {
            return Results.Unauthorized();
        }

        var talentTree = await talentService.GetCharacterTalentTreeAsync(
            characterId,
            cancellationToken);

        return talentTree is null
            ? CreateProblem("talent_tree_not_found", StatusCodes.Status404NotFound, httpContext)
            : Results.Ok(talentTree);
    }

    private static async Task<IResult> GetCharacterTalentsAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        TalentAllocationService talentService,
        CancellationToken cancellationToken)
    {
        if (!TryGetCharacterId(user, out Guid characterId))
        {
            return Results.Unauthorized();
        }

        var talents = await talentService.GetCharacterTalentsAsync(
            characterId,
            cancellationToken);

        return talents is null
            ? Results.Ok(new { characterId, allocatedPoints = new Dictionary<string, int>(), totalSpentPoints = 0, totalAvailablePoints = 0 })
            : Results.Ok(talents);
    }

    private static async Task<IResult> AllocateTalentAsync(
        AllocateTalentRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        TalentAllocationService talentService,
        CancellationToken cancellationToken)
    {
        if (!TryGetCharacterId(user, out Guid characterId))
        {
            return Results.Unauthorized();
        }

        var result = await talentService.AllocateTalentAsync(
            characterId,
            new TalentAllocationCommand(request.TalentId),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Talents);
        }

        int statusCode = result.ErrorCode switch
        {
            TalentAllocationErrorCodes.LevelRequirementNotMet
                or TalentAllocationErrorCodes.NoAvailablePoints
                or TalentAllocationErrorCodes.BranchRequirementNotMet
                or TalentAllocationErrorCodes.PrerequisiteNotMet
                or TalentAllocationErrorCodes.PrerequisiteNotMaxed
                or TalentAllocationErrorCodes.TalentMaxRank
                    => StatusCodes.Status422UnprocessableEntity,
            TalentAllocationErrorCodes.TalentTreeNotFound
                or TalentAllocationErrorCodes.TalentNotFound
                or TalentAllocationErrorCodes.CharacterNotFound
                    => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return CreateProblem(result.ErrorCode!, statusCode, httpContext, result.Message);
    }

    private static bool TryGetCharacterId(
        ClaimsPrincipal user,
        out Guid characterId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out characterId)
        && characterId != Guid.Empty;

    private static IResult CreateProblem(
        string code,
        int statusCode,
        HttpContext httpContext,
        string? message = null) =>
        Results.Problem(
            statusCode: statusCode,
            detail: message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = httpContext.TraceIdentifier
            });
}

public sealed record AllocateTalentRequest(string TalentId);
