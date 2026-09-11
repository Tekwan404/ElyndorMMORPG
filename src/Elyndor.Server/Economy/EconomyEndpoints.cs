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
        return endpoints;
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
}
