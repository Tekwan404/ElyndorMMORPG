using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.World;
using Elyndor.Core.World;
using Elyndor.Infrastructure.World;

namespace Elyndor.Server.World;

public static class WorldEndpoints
{
    public static IEndpointRouteBuilder MapWorldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1")
            .RequireAuthorization()
            .WithTags("World");

        group.MapGet("/bootstrap", GetBootstrapAsync);
        group.MapGet("/world/locations", (WorldMap worldMap) =>
            Results.Ok(worldMap.Locations
                .OrderBy(location => location.Id, StringComparer.Ordinal)
                .Select(ToLocation)
                .ToArray()));
        group.MapPost("/world/travel", TravelAsync);

        return endpoints;
    }

    private static async Task<IResult> GetBootstrapAsync(
        ClaimsPrincipal user,
        BootstrapService bootstrapService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
        {
            return Results.Unauthorized();
        }

        BootstrapSnapshot snapshot = await bootstrapService.GetAsync(
            accountId,
            cancellationToken);
        return Results.Ok(ToResponse(snapshot));
    }

    private static async Task<IResult> TravelAsync(
        TravelRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        TravelService travelService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
        {
            return Results.Unauthorized();
        }

        TravelResult result = await travelService.TravelAsync(
            accountId,
            request.RequestId,
            request.TargetLocationId,
            cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Ok(new TravelResponse(result.LocationId!, result.Version!.Value));
        }

        int statusCode = result.ErrorCode is
            TravelErrorCodes.Conflict or TravelErrorCodes.IdempotencyConflict
                ? StatusCodes.Status409Conflict
                : result.ErrorCode is TravelErrorCodes.InvalidTransition
                    or TravelErrorCodes.UnknownLocation
                    or TravelErrorCodes.InvalidRequest
                        ? StatusCodes.Status422UnprocessableEntity
                        : StatusCodes.Status404NotFound;
        return Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.ErrorCode!,
                ["correlationId"] = httpContext.TraceIdentifier
            });
    }

    private static BootstrapResponse ToResponse(BootstrapSnapshot snapshot) =>
        new(
            snapshot.AccountId,
            snapshot.Character is null
                ? null
                : new BootstrapCharacterResponse(
                    snapshot.Character.Id,
                    snapshot.Character.Name,
                    snapshot.Character.RaceId,
                    snapshot.Character.GenderId,
                    snapshot.Character.ClassId,
                    snapshot.Character.Level,
                    snapshot.Character.PrimaryAttribute,
                    snapshot.Character.ClassProfileVersion,
                    snapshot.Character.KnownAbilityIds,
                    new CharacterStatsResponse(
                        snapshot.Character.Stats.Strength,
                        snapshot.Character.Stats.Agility,
                        snapshot.Character.Stats.Intellect,
                        snapshot.Character.Stats.Stamina,
                        snapshot.Character.Stats.MaxHp,
                        snapshot.Character.Stats.AttackPower,
                        snapshot.Character.Stats.SpellPower,
                        snapshot.Character.Stats.CriticalChance,
                        snapshot.Character.Stats.CriticalDamage,
                        snapshot.Character.Stats.Accuracy,
                        snapshot.Character.Stats.ArmorPenetration,
                        snapshot.Character.Stats.MagicPenetration,
                        snapshot.Character.Stats.AttackSpeed,
                        snapshot.Character.Stats.Armor,
                        snapshot.Character.Stats.MagicResistance,
                        snapshot.Character.Stats.Dodge),
                    new CharacterVitalsResponse(
                        snapshot.Character.Vitals.CurrentHp,
                        snapshot.Character.Vitals.MaxHp,
                        snapshot.Character.Vitals.ResourceType,
                        snapshot.Character.Vitals.CurrentResource,
                        snapshot.Character.Vitals.MaxResource,
                        snapshot.Character.Vitals.CheckpointedAtUtc)),
            snapshot.World is null
                ? null
                : new BootstrapWorldResponse(
                    ToLocation(snapshot.World.CurrentLocation),
                    snapshot.World.Version,
                    snapshot.World.OutgoingTransitions.Select(ToLocation).ToArray()),
            snapshot.ContentVersion,
            snapshot.BalanceVersion,
            snapshot.ServerTimeUtc);

    private static WorldLocationResponse ToLocation(BootstrapLocation location) =>
        new(
            location.Id,
            location.DisplayName,
            location.DangerLevel,
            location.RecommendedLevel);

    private static WorldLocationResponse ToLocation(LocationDefinition location) =>
        new(
            location.Id,
            location.DisplayName,
            location.DangerLevel,
            location.RecommendedLevel);

    private static bool TryGetAccountId(
        ClaimsPrincipal user,
        out Guid accountId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out accountId)
        && accountId != Guid.Empty;
}
