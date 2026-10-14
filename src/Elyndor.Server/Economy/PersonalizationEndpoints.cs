using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Economy;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Economy;

namespace Elyndor.Server.Economy;

public static class PersonalizationEndpoints
{
    public static IEndpointRouteBuilder MapPersonalizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/economy/personalization").RequireAuthorization().WithTags("Economy");
        group.MapGet("", GetAsync);
        group.MapPost("/purchase", PurchaseAsync);
        group.MapPost("/equip", EquipAsync);
        group.MapPost("/rename", RenameAsync);
        return endpoints;
    }
    private static async Task<IResult> GetAsync(ClaimsPrincipal user, PersonalizationStoreService service, CancellationToken ct)
    {
        if (!AccountId(user, out Guid account)) return Results.Unauthorized();
        var store = await service.GetAsync(account, ct);
        return store is null ? Results.NotFound() : Results.Ok(new PersonalizationStoreResponse(store.CrystalBalance,
            store.CharacterName, store.ActiveFrameId, store.ActiveTitleId, store.Rename?.CrystalPrice,
            store.Cosmetics.Select(item => new ProfileCosmeticResponse(item.Definition.Id, item.Definition.Name,
                item.Definition.Description, item.Definition.Kind, item.Definition.StyleId, item.Definition.CrystalPrice,
                item.Owned, item.Definition.Enabled)).ToArray()));
    }
    private static async Task<IResult> PurchaseAsync(CosmeticPurchaseRequest request, ClaimsPrincipal user,
        PersonalizationStoreService service, CancellationToken ct) =>
        !AccountId(user, out Guid account) ? Results.Unauthorized()
            : Result(await service.PurchaseAsync(account, request.CosmeticId, request.MutationId, ct));
    private static async Task<IResult> EquipAsync(CosmeticEquipRequest request, ClaimsPrincipal user,
        PersonalizationStoreService service, CancellationToken ct) =>
        !AccountId(user, out Guid account) ? Results.Unauthorized()
            : Result(await service.EquipAsync(account, request.Kind, request.CosmeticId, ct));
    private static async Task<IResult> RenameAsync(RenameCharacterRequest request, ClaimsPrincipal user,
        PersonalizationStoreService service, CharacterOperationGuard guard, CancellationToken ct)
    {
        if (!AccountId(user, out Guid account)) return Results.Unauthorized();
        return await guard.ExecuteOutOfCombatAsync(account,
            async () => Result(await service.RenameAsync(account, request.Name, request.MutationId, ct)),
            () => Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = "personalization_in_combat" }), ct);
    }
    private static IResult Result(PersonalizationMutationResult result) => result.Succeeded
        ? Results.Ok(new PersonalizationMutationResponse(result.CrystalBalance, result.CharacterName, result.ActiveFrameId, result.ActiveTitleId))
        : Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    private static bool AccountId(ClaimsPrincipal user, out Guid account) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out account) && account != Guid.Empty;
}
