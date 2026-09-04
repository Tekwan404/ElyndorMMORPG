using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Server.Identity;

namespace Elyndor.Server.Administration;

public static class AdminAuthorization
{
    public const string PolicyName = "elyndor-content-admin";
    public const string SuperAdminRole = "SUPER_ADMIN";

    public static string Actor(ClaimsPrincipal user)
    {
        string? telegramUserId =
            user.FindFirst(AuthenticationClaimTypes.TelegramUserId)?.Value;
        if (!string.IsNullOrWhiteSpace(telegramUserId))
            return $"telegram:{telegramUserId}";

        string? accountId = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return string.IsNullOrWhiteSpace(accountId)
            ? "authenticated-admin"
            : $"account:{accountId}";
    }
}
