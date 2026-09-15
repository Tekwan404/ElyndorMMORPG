using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.World;
using Elyndor.Core.Releases;
using Elyndor.Infrastructure.Releases;

namespace Elyndor.Server.Releases;

public static class ReleaseNotesEndpoints
{
    public static IEndpointRouteBuilder MapReleaseNotesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/releases")
            .RequireAuthorization()
            .WithTags("Releases");

        group.MapGet("", GetHistory);
        group.MapPost("/{releaseId}/acknowledge", AcknowledgeAsync);
        return endpoints;
    }

    private static IResult GetHistory(IReleaseNotesCatalog catalog) =>
        Results.Ok(new ReleaseNotesHistoryResponse(
            catalog.History.Select(ToResponse).ToArray()));

    private static async Task<IResult> AcknowledgeAsync(
        string releaseId,
        ClaimsPrincipal user,
        ReleaseAcknowledgementService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        try
        {
            await service.AcknowledgeAsync(accountId, releaseId, cancellationToken);
            return Results.NoContent();
        }
        catch (ReleaseAcknowledgementException exception)
            when (exception.ErrorCode == "release_not_found")
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = exception.ErrorCode });
        }
    }

    private static BootstrapReleaseUpdateResponse ToResponse(ReleaseNoteDefinition release) =>
        new(
            release.Id,
            release.Title,
            release.PublishedAtUtc,
            release.Entries.Select(entry => new ReleaseNoteEntryResponse(
                entry.Kind.ToString(),
                entry.Text)).ToArray());

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
