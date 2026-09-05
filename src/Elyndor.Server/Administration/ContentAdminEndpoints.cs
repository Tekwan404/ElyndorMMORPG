using Elyndor.Contracts.Administration;
using Elyndor.Core.Combat.Simulation;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.Server.Administration;

public static class ContentAdminEndpoints
{
    public static IEndpointRouteBuilder MapContentAdminEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/admin/content")
            .WithTags("Content Administration")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        group.MapGet("/current", GetCurrent);
        group.MapPost("/validate", Validate);
        group.MapPost("/simulate", Simulate);
        group.MapPost("/revisions", CreateRevisionAsync);
        group.MapGet("/revisions/{revisionId:guid}", GetRevisionAsync);
        group.MapGet("/history", GetHistoryAsync);
        group.MapPost("/revisions/{revisionId:guid}/publish", PublishAsync);
        group.MapPost("/releases/{releaseId:guid}/rollback", RollbackAsync);
        return endpoints;
    }

    private static IResult GetCurrent(ContentAdministrationService service)
    {
        ContentAdminRuntimeState current = service.GetCurrent();
        return Results.Ok(new ContentAdminCurrentResponse(
            current.Package.ContentVersion,
            current.Package.BalanceVersion,
            current.Package.PublishedAtUtc,
            current.RevisionId,
            current.ReleaseId,
            current.PayloadSha256,
            current.PayloadJson));
    }

    private static IResult Validate(ContentAdminValidateRequest request)
    {
        ContentDraftValidationResult result =
            ContentAdministrationService.ValidateDraft(request.PayloadJson);
        return Results.Ok(ToValidationResponse(result));
    }

    private static IResult Simulate(
        ContentAdminSimulationRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ContentDraftValidationResult validation =
            ContentAdministrationService.ValidateDraft(request.PayloadJson);
        if (!validation.IsValid)
        {
            return Results.Json(
                new
                {
                    code = "content_invalid",
                    correlationId = context.TraceIdentifier,
                    errors = validation.Errors.Select(error => new
                    {
                        error.Code,
                        error.Path,
                        error.Message
                    })
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        try
        {
            CombatSimulationResult result =
                new CombatSimulationRunner(validation.Package!).Run(
                    new CombatSimulationScenario(
                        request.ClassId,
                        request.PlayerLevel,
                        request.MonsterId,
                        request.Iterations,
                        request.Seed,
                        request.MaxDurationSeconds,
                        request.AbilityPriority,
                        request.SelectedTalentRanks),
                    cancellationToken);

            return Results.Ok(new ContentAdminSimulationResponse(
                result.ContentVersion,
                result.BalanceVersion,
                result.ClassId,
                result.PlayerLevel,
                result.MonsterId,
                result.Iterations,
                result.Victories,
                result.Defeats,
                result.Timeouts,
                result.WinRatePercent,
                result.AverageDurationSeconds,
                result.P50DurationSeconds,
                result.P95DurationSeconds,
                result.AveragePlayerDps,
                result.AverageEnemyDps,
                result.AveragePlayerRemainingHp,
                result.DamageSources.Select(source =>
                    new ContentAdminSimulationDamageSourceResponse(
                        source.DefinitionId,
                        source.AverageDamage,
                        source.DamageSharePercent)).ToArray()));
        }
        catch (CombatSimulationException exception)
        {
            return Problem(
                context,
                StatusCodes.Status422UnprocessableEntity,
                exception.Code);
        }
        catch (ArgumentException)
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                "content_simulation_request_invalid");
        }
    }

    private static async Task<IResult> CreateRevisionAsync(
        ContentAdminCreateRevisionRequest request,
        HttpContext context,
        ContentAdministrationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            ContentRevision revision = await service.CreateDraftAsync(
                request.PayloadJson,
                request.BasePayloadSha256,
                AdminAuthorization.Actor(context.User),
                request.Note,
                cancellationToken);
            return Results.Ok(ToRevisionResponse(revision));
        }
        catch (ContentDraftConflictException)
        {
            return Problem(
                context,
                StatusCodes.Status409Conflict,
                "content_draft_stale");
        }
        catch (ContentPayloadTooLargeException)
        {
            return Problem(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "content_payload_too_large");
        }
        catch (ContentDraftValidationException exception)
        {
            return Results.Json(
                new
                {
                    code = "content_invalid",
                    correlationId = context.TraceIdentifier,
                    errors = exception.Errors.Select(error => new
                    {
                        error.Code,
                        error.Path,
                        error.Message
                    })
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (ArgumentException)
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                "content_request_invalid");
        }
    }

    private static async Task<IResult> GetRevisionAsync(
        Guid revisionId,
        HttpContext context,
        ContentAdministrationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            ContentRevision? revision =
                await service.GetRevisionAsync(revisionId, cancellationToken);
            return revision is null
                ? Problem(
                    context,
                    StatusCodes.Status404NotFound,
                    "content_revision_not_found")
                : Results.Ok(ToRevisionDetailResponse(revision));
        }
        catch (ArgumentException)
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                "content_request_invalid");
        }
    }

    private static async Task<IResult> GetHistoryAsync(
        int? limit,
        ContentAdministrationService service,
        CancellationToken cancellationToken)
    {
        ContentAdminHistory history =
            await service.GetHistoryAsync(limit ?? 30, cancellationToken);
        return Results.Ok(new ContentAdminHistoryResponse(
            history.Revisions.Select(ToRevisionResponse).ToArray(),
            history.Releases.Select(ToReleaseResponse).ToArray()));
    }

    private static async Task<IResult> PublishAsync(
        Guid revisionId,
        ContentAdminPublishRequest request,
        HttpContext context,
        ContentAdministrationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            ContentPublicationResult? published = await service.PublishAsync(
                revisionId,
                request.ExpectedLivePayloadSha256,
                AdminAuthorization.Actor(context.User),
                request.Note,
                cancellationToken);
            return published is null
                ? Problem(
                    context,
                    StatusCodes.Status404NotFound,
                    "content_revision_not_found")
                : Results.Ok(ToReleaseResponse(published.Release));
        }
        catch (ContentPublicationConflictException)
        {
            return Problem(
                context,
                StatusCodes.Status409Conflict,
                "content_live_changed");
        }
        catch (InvalidDataException)
        {
            return Problem(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "content_revision_invalid");
        }
        catch (ArgumentException)
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                "content_request_invalid");
        }
    }

    private static async Task<IResult> RollbackAsync(
        Guid releaseId,
        ContentAdminRollbackRequest request,
        HttpContext context,
        ContentAdministrationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            ContentPublicationResult? published = await service.RollbackAsync(
                releaseId,
                request.ExpectedLivePayloadSha256,
                AdminAuthorization.Actor(context.User),
                request.Note,
                cancellationToken);
            return published is null
                ? Problem(
                    context,
                    StatusCodes.Status404NotFound,
                    "content_release_not_found")
                : Results.Ok(ToReleaseResponse(published.Release));
        }
        catch (ContentPublicationConflictException)
        {
            return Problem(
                context,
                StatusCodes.Status409Conflict,
                "content_live_changed");
        }
        catch (InvalidDataException)
        {
            return Problem(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "content_revision_invalid");
        }
        catch (ArgumentException)
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                "content_request_invalid");
        }
    }

    private static ContentAdminValidationResponse ToValidationResponse(
        ContentDraftValidationResult result) =>
        new(
            result.IsValid,
            result.CanonicalPayloadJson,
            result.PayloadSha256,
            result.Errors
                .Select(error => new ContentAdminValidationErrorResponse(
                    error.Code,
                    error.Path,
                    error.Message))
                .ToArray());

    private static ContentAdminRevisionResponse ToRevisionResponse(
        ContentRevision revision) =>
        new(
            revision.Id,
            revision.ContentVersion,
            revision.BalanceVersion,
            revision.SourcePublishedAtUtc,
            revision.PayloadSha256,
            revision.CreatedAtUtc,
            revision.CreatedBy,
            revision.Note);

    private static ContentAdminRevisionDetailResponse ToRevisionDetailResponse(
        ContentRevision revision) =>
        new(
            revision.Id,
            revision.ContentVersion,
            revision.BalanceVersion,
            revision.SourcePublishedAtUtc,
            revision.PayloadSha256,
            revision.CreatedAtUtc,
            revision.CreatedBy,
            revision.Note,
            revision.PayloadJson);

    private static ContentAdminReleaseResponse ToReleaseResponse(
        ContentRelease release) =>
        new(
            release.Id,
            release.RevisionId,
            release.PublishedAtUtc,
            release.PublishedBy,
            release.Note);

    private static IResult Problem(
        HttpContext context,
        int statusCode,
        string code) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier
            });
}
