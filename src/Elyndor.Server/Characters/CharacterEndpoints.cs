using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Characters;
using Elyndor.Core.Characters;
using Elyndor.Infrastructure.Characters;

namespace Elyndor.Server.Characters;

public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1")
            .RequireAuthorization()
            .WithTags("Character");

        group.MapGet("/me", (ClaimsPrincipal user) =>
            TryGetAccountId(user, out Guid accountId)
                ? Results.Ok(new AccountResponse(accountId))
                : Results.Unauthorized());
        group.MapGet("/character", GetCharacterAsync);
        group.MapPost("/character", CreateCharacterAsync);
        group.MapGet("/character/companion", GetCompanionAsync);
        group.MapPost("/character/companion/select", SelectCompanionAsync);

        return endpoints;
    }

    private static async Task<IResult> GetCharacterAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        CharacterCreationService characterService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
        {
            return Results.Unauthorized();
        }

        Character? character = await characterService.GetAsync(
            accountId,
            cancellationToken);
        return character is null
            ? CreateProblem(
                "character_not_found",
                StatusCodes.Status404NotFound,
                httpContext)
            : Results.Ok(ToResponse(character));
    }

    private static async Task<IResult> CreateCharacterAsync(
        CreateCharacterRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CharacterCreationService characterService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
        {
            return Results.Unauthorized();
        }

        CharacterCreationResult result = await characterService.CreateAsync(
            accountId,
            new CreateCharacterCommand(
                request.RequestId,
                request.Name,
                request.RaceId,
                request.GenderId,
                request.ClassId),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(ToResponse(result.Character!));
        }

        int statusCode = result.ErrorCode is
            CharacterCreationErrorCodes.AlreadyExists
            or CharacterCreationErrorCodes.NameTaken
            or CharacterCreationErrorCodes.IdempotencyConflict
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
        return CreateProblem(result.ErrorCode!, statusCode, httpContext);
    }

    private static async Task<IResult> GetCompanionAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        CharacterCompanionService companionService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        CharacterCompanionSelectionResult result = await companionService.GetAsync(
            accountId,
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(ToResponse(result.Snapshot!))
            : CreateProblem(result.ErrorCode!, StatusCodes.Status422UnprocessableEntity, httpContext);
    }

    private static async Task<IResult> SelectCompanionAsync(
        SelectCompanionRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CharacterCompanionService companionService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        CharacterCompanionSelectionResult result = await companionService.SelectAsync(
            accountId,
            request.CompanionProfileId,
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(ToResponse(result.Snapshot!))
            : CreateProblem(result.ErrorCode!, StatusCodes.Status422UnprocessableEntity, httpContext);
    }

    private static bool TryGetAccountId(
        ClaimsPrincipal user,
        out Guid accountId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out accountId)
        && accountId != Guid.Empty;

    private static CharacterResponse ToResponse(Character character) =>
        new(
            character.Id,
            character.Name,
            character.RaceId,
            character.GenderId,
            character.ClassId,
            character.Level,
            character.CreatedAtUtc);

    private static CharacterCompanionResponse ToResponse(CharacterCompanionSnapshot snapshot) =>
        new(
            snapshot.SelectedPhysicalProfileId,
            snapshot.EffectiveProfileId,
            snapshot.AvailableProfiles.Select(profile => new CompanionProfileResponse(
                profile.Id,
                profile.Name,
                profile.Archetype,
                profile.ArtId)).ToArray());

    private static IResult CreateProblem(
        string code,
        int statusCode,
        HttpContext httpContext) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = httpContext.TraceIdentifier
            });
}
