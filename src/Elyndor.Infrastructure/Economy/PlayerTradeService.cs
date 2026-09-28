using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Economy;

public sealed class PlayerTradeService(GameDbContext db, CommerceTransaction transactions,
    IContentSnapshotProvider content, TimeProvider time)
{
    public async Task<TradeResponse[]> ActiveAsync(Guid account, CancellationToken ct)
    {
        var id = await transactions.CharacterIdAsync(account, ct);
        if (id is null) return [];
        return (await db.PlayerTrades.AsNoTracking()
            .Where(x => x.State == "OPEN" && (x.CharacterAId == id || x.CharacterBId == id))
            .OrderBy(x => x.ExpiresAt).ToArrayAsync(ct)).Select(Response).ToArray();
    }

    public async Task<CommerceResult<TradeResponse>> OpenAsync(Guid account, Guid otherCharacter, Guid request,
        string connection, CancellationToken ct)
    {
        var me = await transactions.CharacterIdAsync(account, ct);
        Guid id = CommerceTransaction.OperationId(account, request);
        if (me is null || me == otherCharacter) return CommerceResult.Failure<TradeResponse>("trade_invalid_participants");
        return await transactions.RunAsync(account, request, "TRADE_OPEN", new { otherCharacter }, [me.Value, otherCharacter],
            async (_, replay) =>
            {
                if (replay) return Response(await db.PlayerTrades.SingleAsync(x => x.Id == id, ct));
                await transactions.EligibleAsync([me.Value, otherCharacter], ct);
                if (await db.PlayerTrades.AnyAsync(x => x.State == "OPEN" &&
                    (x.CharacterAId == me || x.CharacterBId == me || x.CharacterAId == otherCharacter || x.CharacterBId == otherCharacter), ct))
                    throw new CommerceRuleException("trade_already_active");
                var trade = new PlayerTrade(id, me.Value, otherCharacter, time.GetUtcNow());
                trade.Connect(me.Value, connection);
                db.PlayerTrades.Add(trade);
                return Response(trade);
            }, ct);
    }

    public async Task<CommerceResult<TradeResponse>> ActAsync(Guid account, Guid id, Guid request, string action,
        int revision, Guid[]? items, long gold, string connection, CancellationToken ct)
    {
        var preview = await db.PlayerTrades.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (preview is null) return CommerceResult.Failure<TradeResponse>("trade_not_found");
        return await transactions.RunAsync(account, request, "TRADE_" + action,
            new { id, revision, items, gold }, [preview.CharacterAId, preview.CharacterBId], async (characters, replay) =>
            {
                var trade = await db.PlayerTrades.SingleAsync(x => x.Id == id, ct);
                var actor = characters.Single(x => x.AccountId == account);
                trade.IsA(actor.Id);
                if (replay) return Response(trade);
                if (time.GetUtcNow() >= trade.ExpiresAt) { trade.Cancel(); await ReleaseAsync(trade, ct); return Response(trade); }
                if (action == "CONNECT") { trade.Connect(actor.Id, connection); return Response(trade); }
                if (action == "DECLINE") { trade.Cancel(); await ReleaseAsync(trade, ct); return Response(trade); }
                trade.RequireConnection(actor.Id, connection);
                if (action == "CANCEL") { trade.Cancel(); await ReleaseAsync(trade, ct); return Response(trade); }
                await transactions.EligibleAsync([trade.CharacterAId, trade.CharacterBId], ct);
                if (action == "OFFER")
                {
                    if (revision != trade.Revision) throw new CommerceRuleException("trade_stale_revision");
                    var offered = items ?? throw new CommerceRuleException("trade_invalid_offer");
                    var old = trade.IsA(actor.Id) ? trade.ItemsA : trade.ItemsB;
                    trade.ChangeOffer(actor.Id, offered, gold);
                    foreach (var itemId in old.Except(offered))
                    {
                        var item = await db.CharacterItems.SingleOrDefaultAsync(x => x.Id == itemId && x.CharacterId == actor.Id, ct);
                        if (item?.TransactionLockId == id) item.ReleaseTransactionLock(id);
                    }
                    foreach (var itemId in offered)
                    {
                        var item = await transactions.ItemAsync(itemId, actor.Id, old.Contains(itemId) ? id : null, false, ct);
                        item.AcquireTransactionLock(id);
                    }
                }
                else if (action == "LOCK") trade.Lock(actor.Id, revision);
                else if (action == "CONFIRM")
                {
                    trade.Confirm(actor.Id, revision);
                    if (trade.Ready)
                    {
                        var a = characters.Single(x => x.Id == trade.CharacterAId);
                        var b = characters.Single(x => x.Id == trade.CharacterBId);
                        if (a.Gold < trade.GoldA || b.Gold < trade.GoldB) throw new CommerceRuleException("commerce_insufficient_funds");
                        var capacityA = await InventoryCapacity.GetStateAsync(db, a.Id, content.GetCurrent(), ct);
                        var capacityB = await InventoryCapacity.GetStateAsync(db, b.Id, content.GetCurrent(), ct);
                        if (capacityA.UsedSlots - trade.ItemsA.Length + trade.ItemsB.Length > capacityA.Capacity
                            || capacityB.UsedSlots - trade.ItemsB.Length + trade.ItemsA.Length > capacityB.Capacity)
                            throw new CommerceRuleException("commerce_inventory_full");
                        foreach (var itemId in trade.ItemsA) (await transactions.ItemAsync(itemId, a.Id, id, false, ct)).Transfer(id, b.Id, "INVENTORY");
                        foreach (var itemId in trade.ItemsB) (await transactions.ItemAsync(itemId, b.Id, id, false, ct)).Transfer(id, a.Id, "INVENTORY");
                        a.TrySpendGold(trade.GoldA); b.TrySpendGold(trade.GoldB);
                        a.AddGold(trade.GoldB); b.AddGold(trade.GoldA);
                        trade.Complete();
                    }
                }
                else throw new CommerceRuleException("trade_invalid_action");
                return Response(trade);
            }, ct);
    }

    public async Task DisconnectAsync(Guid account, string connection, CancellationToken ct)
    {
        var trades = await db.PlayerTrades.AsNoTracking().Where(x => x.State == "OPEN"
            && (x.ConnectionA == connection || x.ConnectionB == connection)).ToArrayAsync(ct);
        foreach (var trade in trades)
            await ActAsync(account, trade.Id, Guid.NewGuid(), "CANCEL", trade.Revision, null, 0, connection, ct);
    }

    private async Task ReleaseAsync(PlayerTrade trade, CancellationToken ct)
    {
        var items = await db.CharacterItems.Where(x => x.TransactionLockId == trade.Id).ToArrayAsync(ct);
        foreach (var item in items) item.ReleaseTransactionLock(trade.Id);
    }
    public static TradeResponse Response(PlayerTrade t) => new(t.Id, t.State, t.Revision, t.CharacterAId, t.CharacterBId,
        t.ItemsA, t.ItemsB, t.GoldA.ToString(System.Globalization.CultureInfo.InvariantCulture),
        t.GoldB.ToString(System.Globalization.CultureInfo.InvariantCulture),
        t.LockedA, t.LockedB, t.ConfirmedA, t.ConfirmedB);
}
