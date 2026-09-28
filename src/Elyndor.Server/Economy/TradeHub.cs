using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Core.Economy;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Economy;

[Authorize]
public sealed partial class TradeHub(PlayerTradeService trades, GameDbContext db, IContentSnapshotProvider content,
    ILogger<TradeHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(Account()), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
    public Task<TradeResponse[]> Active() => trades.ActiveAsync(Account(), Context.ConnectionAborted);
    public async Task<TradeResponse?> Get(Guid tradeId)
    {
        var trade = await db.PlayerTrades.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tradeId, Context.ConnectionAborted);
        if (trade is null) return null;
        var ownedId = await db.Characters.AsNoTracking().Where(x => x.AccountId == Account())
            .Select(x => x.Id).SingleOrDefaultAsync(Context.ConnectionAborted);
        if (ownedId != trade.CharacterAId && ownedId != trade.CharacterBId)
            throw new HubException("trade_not_participant");
        return PlayerTradeService.Response(trade);
    }
    public async Task<TradeItemView[]> Items(Guid tradeId)
    {
        var trade = (await trades.ActiveAsync(Account(), Context.ConnectionAborted)).SingleOrDefault(x => x.Id == tradeId);
        if (trade is null) throw new HubException("trade_not_participant");
        var ids = trade.ItemsA.Concat(trade.ItemsB).ToArray();
        var items = await db.CharacterItems.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.ItemDefinitionId, x.Quantity }).ToArrayAsync(Context.ConnectionAborted);
        var definitions = content.GetCurrent().Package.Items?.ToDictionary(x => x.Id, StringComparer.Ordinal);
        return items.Select(x =>
        {
            var definition = definitions?.GetValueOrDefault(x.ItemDefinitionId);
            return new TradeItemView(x.Id, definition?.Name ?? x.ItemDefinitionId,
                definition?.IconId, definition?.Type.ToString() ?? "Other",
                definition?.Rarity.ToString() ?? "Common", x.Quantity);
        }).ToArray();
    }
    public async Task<CommerceResult<TradeResponse>> Open(Guid targetCharacterId, Guid requestId) =>
        await PublishAsync(await trades.OpenAsync(Account(), targetCharacterId, requestId,
            Context.ConnectionId, Context.ConnectionAborted));
    public Task<CommerceResult<TradeResponse>> Join(Guid tradeId, Guid requestId) =>
        Act(tradeId, requestId, "CONNECT", 0);
    public async Task<CommerceResult<TradeResponse>> Offer(Guid tradeId, TradeOfferRequest request) =>
        await PublishAsync(await trades.ActAsync(Account(), tradeId, request.RequestId, "OFFER", request.Revision,
            request.ItemIds, request.Gold, Context.ConnectionId, Context.ConnectionAborted));
    public Task<CommerceResult<TradeResponse>> Lock(Guid tradeId, TradeRevisionRequest request) =>
        Act(tradeId, request.RequestId, "LOCK", request.Revision);
    public Task<CommerceResult<TradeResponse>> Confirm(Guid tradeId, TradeRevisionRequest request) =>
        Act(tradeId, request.RequestId, "CONFIRM", request.Revision);
    public Task<CommerceResult<TradeResponse>> Cancel(Guid tradeId, Guid requestId) => Act(tradeId, requestId, "CANCEL", 0);
    public Task<CommerceResult<TradeResponse>> Decline(Guid tradeId, Guid requestId) => Act(tradeId, requestId, "DECLINE", 0);
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var active = await db.PlayerTrades.AsNoTracking()
            .Where(x => x.State == "OPEN" && (x.ConnectionA == Context.ConnectionId || x.ConnectionB == Context.ConnectionId))
            .Select(x => x.Id).ToArrayAsync(CancellationToken.None);
        await trades.DisconnectAsync(Account(), Context.ConnectionId, CancellationToken.None);
        foreach (var id in active)
        {
            var trade = await db.PlayerTrades.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, CancellationToken.None);
            if (trade is not null) await TryNotifyAsync(PlayerTradeService.Response(trade));
        }
        await base.OnDisconnectedAsync(exception);
    }
    private async Task<CommerceResult<TradeResponse>> Act(Guid id, Guid request, string action, int revision) =>
        await PublishAsync(await trades.ActAsync(Account(), id, request, action, revision, null, 0,
            Context.ConnectionId, Context.ConnectionAborted));
    private async Task<CommerceResult<TradeResponse>> PublishAsync(CommerceResult<TradeResponse> result)
    {
        if (result.Succeeded && result.Snapshot is { } trade) await TryNotifyAsync(trade);
        return result;
    }
    private async Task TryNotifyAsync(TradeResponse trade)
    {
        try { await NotifyAsync(trade); }
        catch (Exception ex) { LogTradePushFailure(logger, trade.Id, ex); }
    }
    private async Task NotifyAsync(TradeResponse trade)
    {
        var ids = await db.Characters.AsNoTracking()
            .Where(x => x.Id == trade.CharacterAId || x.Id == trade.CharacterBId)
            .Select(x => x.AccountId).ToArrayAsync(CancellationToken.None);
        await Clients.Groups(ids.Select(AccountGroup).ToArray()).SendAsync("TradeUpdated", trade, CancellationToken.None);
    }
    private static string AccountGroup(Guid accountId) => $"trade-account:{accountId:N}";
    private Guid Account() => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : throw new HubException("unauthorized");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to push trade {TradeId} update after commit")]
    private static partial void LogTradePushFailure(ILogger logger, Guid tradeId, Exception exception);
}
