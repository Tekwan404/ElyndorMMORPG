using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.Realtime;

[Authorize]
public sealed class LiveStateHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        Guid accountId = GetAccountId();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(accountId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public static string GroupName(Guid accountId) => $"state-account:{accountId:N}";

    private Guid GetAccountId()
    {
        string? raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(raw, out Guid accountId) && accountId != Guid.Empty
            ? accountId
            : throw new HubException("unauthorized");
    }
}
