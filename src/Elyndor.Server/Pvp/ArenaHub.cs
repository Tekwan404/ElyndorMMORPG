using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Arena;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Pvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.Pvp;

/// <summary>
/// Realtime arena channel. A live connection is the player's presence: it keeps a queue entry alive
/// and satisfies the reconnect grace of a running match. Clients send only intent + command id.
/// </summary>
[Authorize]
public sealed class ArenaHub(
    ArenaMatchRuntime runtime,
    ArenaPresenceTracker presence,
    IArenaUpdatePublisher publisher,
    IContentSnapshotProvider contentProvider,
    CombatCommandRateLimiter rateLimiter,
    Microsoft.Extensions.Options.IOptions<ArenaOptions> options) : Hub
{
    public static string GroupName(Guid accountId) => $"arena:{accountId:N}";

    public override async Task OnConnectedAsync()
    {
        if (!options.Value.Enabled) throw new HubException("arena_disabled");
        Guid accountId = GetAccountId();
        presence.Connected(accountId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(accountId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetAccountId(out Guid accountId))
            presence.Disconnected(accountId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public ArenaMatchResponse? GetMatch(Guid matchId, long afterSequence)
    {
        Guid accountId = GetAccountId();
        ArenaTestState? state = runtime.GetState(accountId, matchId, Math.Max(0, afterSequence));
        return state is null ? null : ArenaContractMapper.ToResponse(state, contentProvider.GetCurrent().Package);
    }

    public async Task<ArenaCommandResponse> UseAbility(Guid matchId, string abilityId, Guid targetActorId,
        string commandId)
    {
        Guid accountId = GetCommandAccountId();
        ArenaTestCommandResult result = runtime.UseAbility(accountId, matchId, commandId, abilityId, targetActorId);
        if (result.Succeeded) await NotifyOpponentAsync(accountId, matchId, result.State?.Sequence ?? 0);
        return ArenaContractMapper.ToResponse(result, contentProvider.GetCurrent().Package);
    }

    public async Task<ArenaCommandResponse> Surrender(Guid matchId)
    {
        Guid accountId = GetCommandAccountId();
        ArenaTestCommandResult result = runtime.Surrender(accountId, matchId);
        if (result.Succeeded) await NotifyOpponentAsync(accountId, matchId, result.State?.Sequence ?? 0);
        return ArenaContractMapper.ToResponse(result, contentProvider.GetCurrent().Package);
    }

    private async Task NotifyOpponentAsync(Guid accountId, Guid matchId, long sequence)
    {
        if (runtime.OpponentAccountId(matchId, accountId) is not { } opponent) return;
        try
        {
            await publisher.PublishMatchUpdatedAsync(opponent, matchId, sequence, Context.ConnectionAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A committed command must never look failed because a notification could not be sent.
        }
    }

    private Guid GetCommandAccountId()
    {
        Guid accountId = GetAccountId();
        if (!rateLimiter.TryAcquire(accountId)) throw new HubException("rate_limited");
        return accountId;
    }

    private Guid GetAccountId() =>
        TryGetAccountId(out Guid accountId) ? accountId : throw new HubException("unauthorized");

    private bool TryGetAccountId(out Guid accountId) =>
        Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
