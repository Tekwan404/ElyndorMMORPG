using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Economy;
using Elyndor.Infrastructure.Economy;

namespace Elyndor.Server.Economy;

public static class EconomyEndpoints
{
    public static IEndpointRouteBuilder MapEconomyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/economy/wallet", GetWalletAsync)
            .RequireAuthorization()
            .WithTags("Economy");
        endpoints.MapPost("/api/v1/economy/store/purchase", PurchaseAsync)
            .RequireAuthorization()
            .WithTags("Economy");
        endpoints.MapGet("/api/v1/economy/store", GetStoreAsync)
            .RequireAuthorization()
            .WithTags("Economy");
        endpoints.MapPost("/api/v1/economy/promo/redeem", RedeemPromoAsync)
            .RequireAuthorization()
            .WithTags("Economy");
        return endpoints;
    }

    private static async Task<IResult> GetStoreAsync(ClaimsPrincipal user, PremiumStoreService service, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId) || accountId == Guid.Empty) return Results.Unauthorized();
        PremiumStoreSnapshot store = await service.GetAsync(accountId, cancellationToken);
        return Results.Ok(new PremiumStoreResponse(store.CrystalBalance, store.Offers.Select(offer => new PremiumStoreOfferResponse(
            offer.Offer.Sku, offer.Item.Id, offer.Item.Name, offer.Item.Description, offer.Item.Rarity.ToString(), offer.Item.IconId,
            offer.Offer.Quantity, offer.Offer.CrystalPrice, offer.CanPurchase)).ToArray()));
    }

    private static async Task<IResult> GetWalletAsync(
        ClaimsPrincipal user,
        CrystalWalletService service,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId)
            || accountId == Guid.Empty)
            return Results.Unauthorized();

        CrystalWalletSnapshot wallet = await service.GetAsync(accountId, cancellationToken);
        return Results.Ok(new CrystalWalletResponse(wallet.Balance));
    }

    private static async Task<IResult> PurchaseAsync(PremiumStorePurchaseRequest request, ClaimsPrincipal user, PremiumStoreService service, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId) || accountId == Guid.Empty) return Results.Unauthorized();
        PremiumStorePurchaseResult result = await service.PurchaseAsync(accountId, request.Sku, request.MutationId, cancellationToken);
        return result.Succeeded ? Results.Ok(new PremiumStorePurchaseResponse(result.CrystalBalance)) : Results.Problem(statusCode: StatusCodes.Status409Conflict, extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    private static async Task<IResult> RedeemPromoAsync(PromoCodeRedemptionRequest request, ClaimsPrincipal user, PromoCodeService service, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId) || accountId == Guid.Empty) return Results.Unauthorized();
        PromoCodeRedemptionResult result = await service.RedeemAsync(accountId, request.Code, request.MutationId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new PromoCodeRedemptionResponse(result.CrystalBalance))
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }
}
