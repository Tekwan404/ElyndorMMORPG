using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.WorldBosses;

[Authorize]
public sealed class WorldBossHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        Guid accountId = GetAccountId();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            AccountGroupName(accountId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public Task WatchSpawn(Guid spawnId)
    {
        if (spawnId == Guid.Empty)
            throw new HubException("world_boss_spawn_invalid");

        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            SpawnGroupName(spawnId),
            Context.ConnectionAborted);
    }

    public Task UnwatchSpawn(Guid spawnId)
    {
        if (spawnId == Guid.Empty)
            throw new HubException("world_boss_spawn_invalid");

        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            SpawnGroupName(spawnId),
            Context.ConnectionAborted);
    }

    public string Ping() => "pong";

    internal static string SpawnGroupName(Guid spawnId) =>
        $"world-boss:{spawnId:N}";

    internal static string AccountGroupName(Guid accountId) =>
        $"world-boss-account:{accountId:N}";

    private Guid GetAccountId() =>
        Guid.TryParse(
            Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out Guid accountId)
        && accountId != Guid.Empty
            ? accountId
            : throw new HubException("authentication_required");
}
