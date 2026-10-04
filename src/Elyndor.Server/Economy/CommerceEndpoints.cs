using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Core.Economy;
using Elyndor.Infrastructure.Economy;

namespace Elyndor.Server.Economy;

public static class CommerceEndpoints
{
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").RequireAuthorization().WithTags("Commerce");
        group.MapGet("/auction", async (string? search, string? type, bool? mine, int? page, ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
            Account(user) is { } account ? Results.Ok(await service.ListingsAsync(account, mine == true, search, type, page ?? 0, ct)) : Results.Unauthorized());
        group.MapGet("/auction/sellable-items", async (ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
            Account(user) is { } account ? Results.Ok(await service.SellableItemsAsync(account, ct)) : Results.Unauthorized());
        group.MapPost("/auction/preview", async (AuctionPreviewRequest request, ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
            Account(user) is { } account ? Result(await service.PreviewAsync(account, request, ct)) : Results.Unauthorized());
        group.MapPost("/auction", async (AuctionCreateRequest request, ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
        {
            if (Account(user) is not { } account) return Results.Unauthorized();
            if (!request.ExpectedFee.HasValue || !request.ExpectedTax.HasValue)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["code"] = "auction_quote_required" });
            return Result(await service.CreateAsync(account, request, ct));
        });
        group.MapPost("/auction/{id:guid}/buy", async (Guid id, CommerceRequest request, ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
            Account(user) is { } account ? Result(await service.BuyAsync(account, id, request.RequestId, ct)) : Results.Unauthorized());
        group.MapPost("/auction/{id:guid}/cancel", async (Guid id, CommerceRequest request, ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
            Account(user) is { } account ? Result(await service.ReturnAsync(account, id, request.RequestId, false, ct)) : Results.Unauthorized());
        group.MapGet("/mailbox", async (ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
            Account(user) is { } account ? Results.Ok(await service.MailAsync(account, ct)) : Results.Unauthorized());
        group.MapPost("/mailbox/{id:guid}/claim", async (Guid id, CommerceRequest request, ClaimsPrincipal user, AuctionSettlementService service, CancellationToken ct) =>
            Account(user) is { } account ? Result(await service.ClaimAsync(account, id, request.RequestId, ct)) : Results.Unauthorized());
        return endpoints;
    }
    private static Guid? Account(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var account) ? account : null;
    private static IResult Result<T>(CommerceResult<T> result) => result.Succeeded ? Results.Ok(result.Snapshot)
        : Results.Problem(statusCode: StatusCodes.Status409Conflict, extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
}
