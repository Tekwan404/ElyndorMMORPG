using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Core.Economy;
using Elyndor.Infrastructure.Economy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.Economy;

[Authorize]
public sealed class TradeHub(PlayerTradeService trades) : Hub
{
    public Task<TradeResponse[]> Active() => trades.ActiveAsync(Account(), Context.ConnectionAborted);
    public Task<CommerceResult<TradeResponse>> Open(Guid targetCharacterId, Guid requestId) =>
        trades.OpenAsync(Account(), targetCharacterId, requestId, Context.ConnectionId, Context.ConnectionAborted);
    public Task<CommerceResult<TradeResponse>> Join(Guid tradeId, Guid requestId) =>
        Act(tradeId, requestId, "CONNECT", 0);
    public Task<CommerceResult<TradeResponse>> Offer(Guid tradeId, TradeOfferRequest request) =>
        trades.ActAsync(Account(), tradeId, request.RequestId, "OFFER", request.Revision, request.ItemIds,
            request.Gold, Context.ConnectionId, Context.ConnectionAborted);
    public Task<CommerceResult<TradeResponse>> Lock(Guid tradeId, TradeRevisionRequest request) =>
        Act(tradeId, request.RequestId, "LOCK", request.Revision);
    public Task<CommerceResult<TradeResponse>> Confirm(Guid tradeId, TradeRevisionRequest request) =>
        Act(tradeId, request.RequestId, "CONFIRM", request.Revision);
    public Task<CommerceResult<TradeResponse>> Cancel(Guid tradeId, Guid requestId) => Act(tradeId, requestId, "CANCEL", 0);
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await trades.DisconnectAsync(Account(), Context.ConnectionId, CancellationToken.None);
        await base.OnDisconnectedAsync(exception);
    }
    private Task<CommerceResult<TradeResponse>> Act(Guid id, Guid request, string action, int revision) =>
        trades.ActAsync(Account(), id, request, action, revision, null, 0, Context.ConnectionId, Context.ConnectionAborted);
    private Guid Account() => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : throw new HubException("unauthorized");
}
