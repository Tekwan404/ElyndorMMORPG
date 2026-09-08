using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Quests;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Quests;

namespace Elyndor.Server.Quests;

public static class QuestEndpoints
{
    public static IEndpointRouteBuilder MapQuestEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/quests")
            .RequireAuthorization()
            .WithTags("Quests");

        group.MapGet("/", GetAsync);
        group.MapPost("/accept", AcceptAsync);
        group.MapPost("/abandon", AbandonAsync);
        group.MapPost("/claim", ClaimAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        QuestService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        QuestJournalSnapshot snapshot =
            await service.GetAsync(accountId, cancellationToken);
        return Results.Ok(new QuestJournalResponse(
            snapshot.Quests.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> AcceptAsync(
        QuestMutationRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        QuestService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                QuestMutationResult result = await service.AcceptAsync(
                    accountId,
                    request.QuestId,
                    cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(new QuestMutationResponse(
                        result.QuestId!,
                        "ACTIVE"))
                    : Problem(result.ErrorCode!, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> AbandonAsync(
        QuestMutationRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        QuestService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                QuestMutationResult result = await service.AbandonAsync(
                    accountId,
                    request.QuestId,
                    cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(new QuestMutationResponse(
                        result.QuestId!,
                        "ABANDONED"))
                    : Problem(result.ErrorCode!, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> ClaimAsync(
        QuestClaimRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        QuestService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                QuestClaimResult result = await service.ClaimAsync(
                    accountId,
                    request.QuestId,
                    request.MutationId,
                    cancellationToken);
                if (!result.IsSuccess)
                    return Problem(result.ErrorCode!, context);

                return Results.Ok(new QuestClaimResponse(
                    result.QuestId!,
                    result.Granted,
                    result.XpEarned,
                    result.GoldEarned,
                    result.Progression?.LeveledUp ?? false,
                    result.Progression?.PreviousLevel ?? 0,
                    result.Progression?.CurrentLevel ?? 0,
                    result.Items.Select(item =>
                        new QuestRewardItemResponse(
                            item.ItemId,
                            item.Quantity)).ToArray()));
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static QuestResponse ToResponse(QuestJournalEntry quest) =>
        new(
            quest.Id,
            quest.DisplayName,
            quest.Description,
            quest.Type,
            quest.RequiredLevel,
            quest.OfferLocationId,
            quest.Status,
            quest.Objectives.Select(objective =>
                new QuestObjectiveResponse(
                    objective.Id,
                    objective.Type,
                    objective.TargetId,
                    objective.CurrentCount,
                    objective.RequiredCount,
                    objective.Completed,
                    objective.ConsumeOnClaim)).ToArray(),
            quest.RewardXp,
            quest.RewardGold,
            quest.RewardItems.Select(item =>
                new QuestRewardItemResponse(
                    item.ItemId,
                    item.Quantity)).ToArray(),
            quest.PrerequisiteQuestIds,
            quest.UnlockLocationId);

    private static IResult Problem(string code, HttpContext context)
    {
        int statusCode = code switch
        {
            QuestErrorCodes.CharacterNotFound
                or QuestErrorCodes.QuestNotFound =>
                StatusCodes.Status404NotFound,
            QuestErrorCodes.LevelRequired
                or QuestErrorCodes.InvalidLocation
                or QuestErrorCodes.PrerequisiteRequired =>
                StatusCodes.Status403Forbidden,
            QuestErrorCodes.AlreadyActive
                or QuestErrorCodes.AlreadyCompleted
                or QuestErrorCodes.NotReady
                or QuestErrorCodes.ProtectedItems
                or QuestErrorCodes.ClaimConflict =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        return Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier
            });
    }

    private static IResult InCombatProblem(HttpContext context) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = CharacterOperationErrorCodes.InCombat,
                ["correlationId"] = context.TraceIdentifier
            });

    private static bool TryGetAccountId(
        ClaimsPrincipal user,
        out Guid accountId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out accountId)
        && accountId != Guid.Empty;
}
