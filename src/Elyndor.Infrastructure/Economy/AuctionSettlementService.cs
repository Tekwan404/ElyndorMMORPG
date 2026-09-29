using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elyndor.Infrastructure.Economy;

public sealed class AuctionOptions
{
    public decimal ListingFeeRate { get; set; } = 0.01m;
    public decimal SaleTaxRate { get; set; } = 0.05m;
    public long MinimumListingFee { get; set; } = 1;
    public int MaxActiveListings { get; set; } = 20;
}

public sealed class AuctionSettlementService(GameDbContext db, CommerceTransaction transactions,
    IContentSnapshotProvider content, TimeProvider time, IOptions<AuctionOptions> options,
    ITelegramMessageSender? messageSender = null)
{
    public async Task<AuctionListingView[]> ListingsAsync(Guid account, bool mine, string? search, string? type, int page, CancellationToken ct)
    {
        if (page < 0 || page > 10_000 || search?.Length > 100) return [];
        var owner = await transactions.CharacterIdAsync(account, ct);
        if (owner is null) return [];
        var definitions = content.GetCurrent().Package.Items?.ToDictionary(x => x.Id, StringComparer.Ordinal);
        if (definitions is null) return [];
        var query = db.AuctionListings.AsNoTracking().Where(x => x.State == "ACTIVE" && x.ExpiresAt > time.GetUtcNow());
        if (mine) query = query.Where(x => x.SellerId == owner);
        else query = query.Where(x => x.SellerId != owner);
        if (!string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(type))
        {
            var matches = definitions.Values.Where(x =>
                    (string.IsNullOrWhiteSpace(search) || x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrWhiteSpace(type) || x.Type.ToString().Equals(type, StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.Id).ToArray();
            if (matches.Length == 0) return [];
            query = from lot in query
                    join item in db.CharacterItems.IgnoreQueryFilters() on lot.ItemId equals item.Id
                    where matches.Contains(item.ItemDefinitionId)
                    select lot;
        }
        var rows = await (from lot in query.OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id).Skip(page * 50).Take(50)
                          join item in db.CharacterItems.IgnoreQueryFilters().AsNoTracking() on lot.ItemId equals item.Id
                          join seller in db.Characters.AsNoTracking() on lot.SellerId equals seller.Id
                          where item.Storage == "AUCTION" && item.CharacterId == lot.SellerId
                          select new
                          {
                              lot,
                              item.ItemDefinitionId,
                              item.Quantity,
                              item.GeneratedDisplayName,
                              item.ItemLevel,
                              item.ActualItemPower,
                              item.RollQuality,
                              item.Stars,
                              item.IsPerfect,
                              item.EnhancementLevel,
                              item.RolledStrength,
                              item.RolledAgility,
                              item.RolledIntellect,
                              item.RolledStamina,
                              SellerName = seller.Name
                          })
            .ToArrayAsync(ct);
        if (rows.Length == 0) return [];

        Guid[] itemIds = rows.Select(row => row.lot.ItemId).ToArray();
        var affixes = await db.CharacterItemAffixes.AsNoTracking()
            .Where(affix => itemIds.Contains(affix.ItemInstanceId))
            .OrderBy(affix => affix.ItemInstanceId)
            .ThenBy(affix => affix.GenerationOrdinal)
            .ToArrayAsync(ct);
        var affixesByItem = affixes.GroupBy(affix => affix.ItemInstanceId).ToDictionary(
            group => group.Key,
            group => group.Select(affix => new AuctionAffixView(
                affix.SlotKey,
                affix.StatId,
                affix.Value,
                affix.AffixTier,
                affix.IsGuaranteed,
                affix.IsReforgeSlot)).ToArray());

        return rows.Select(row =>
        {
            var definition = definitions.GetValueOrDefault(row.ItemDefinitionId);
            var rolledStats = row.RolledStrength.HasValue || row.RolledAgility.HasValue
                || row.RolledIntellect.HasValue || row.RolledStamina.HasValue
                ? new AuctionRolledStatsView(row.RolledStrength, row.RolledAgility, row.RolledIntellect, row.RolledStamina)
                : null;
            return new AuctionListingView(row.lot.Id, row.lot.SellerId, row.SellerName, row.lot.ItemId,
                row.ItemDefinitionId,
                string.IsNullOrWhiteSpace(row.GeneratedDisplayName) ? definition?.Name ?? row.ItemDefinitionId : row.GeneratedDisplayName!,
                definition?.IconId,
                definition?.Type.ToString() ?? "Other", definition?.Rarity.ToString() ?? "Common",
                row.Quantity, row.ItemLevel, row.ActualItemPower, row.RollQuality, row.Stars, row.IsPerfect,
                row.EnhancementLevel, rolledStats, affixesByItem.GetValueOrDefault(row.lot.ItemId) ?? [],
                Money(row.lot.Price), row.lot.ExpiresAt);
        }).ToArray();
    }

    public async Task<CommerceResult<AuctionFeePreviewView>> PreviewAsync(Guid account, AuctionPreviewRequest request, CancellationToken ct)
    {
        var character = await transactions.CharacterIdAsync(account, ct);
        if (character is null) return CommerceResult.Failure<AuctionFeePreviewView>("commerce_character_not_found");
        try
        {
            if (await db.AuctionListings.CountAsync(x => x.SellerId == character && x.State == "ACTIVE", ct)
                >= options.Value.MaxActiveListings)
                throw new CommerceRuleException("auction_listing_limit");
            _ = await transactions.ItemAsync(request.ItemId, character.Value, null, false, ct);
            var (fee, tax) = Quote(request.Price);
            return CommerceResult.Success(new AuctionFeePreviewView(
                request.ItemId, Money(request.Price), Money(fee), Money(tax), Money(request.Price - tax)));
        }
        catch (CommerceRuleException exception)
        {
            return CommerceResult.Failure<AuctionFeePreviewView>(exception.Code);
        }
        catch (OverflowException)
        {
            return CommerceResult.Failure<AuctionFeePreviewView>("auction_invalid_listing");
        }
    }

    public async Task<CommerceResult<AuctionResponse>> CreateAsync(Guid account, AuctionCreateRequest request, CancellationToken ct)
    {
        var character = await transactions.CharacterIdAsync(account, ct);
        Guid id = CommerceTransaction.OperationId(account, request.RequestId);
        if (character is null) return CommerceResult.Failure<AuctionResponse>("commerce_character_not_found");
        return await transactions.RunAsync(account, request.RequestId, "AUCTION_CREATE", request, [character.Value], async (characters, replay) =>
        {
            if (replay) return Response(await db.AuctionListings.SingleAsync(x => x.Id == id, ct));
            if (await db.AuctionListings.CountAsync(x => x.SellerId == character && x.State == "ACTIVE", ct)
                >= options.Value.MaxActiveListings)
                throw new CommerceRuleException("auction_listing_limit");
            var item = await transactions.ItemAsync(request.ItemId, character.Value, null, false, ct);
            var (fee, tax) = Quote(request.Price);
            if ((request.ExpectedFee.HasValue || request.ExpectedTax.HasValue)
                && (request.ExpectedFee != fee || request.ExpectedTax != tax))
                throw new CommerceRuleException("auction_quote_changed");
            var listing = new AuctionListing(id, character.Value, item.Id, request.Price, fee, tax, time.GetUtcNow());
            if (!characters[0].TrySpendGold(fee)) throw new CommerceRuleException("commerce_insufficient_funds");
            item.AcquireTransactionLock(listing.Id);
            item.Transfer(listing.Id, character.Value, "AUCTION");
            db.AuctionListings.Add(listing);
            return Response(listing);
        }, ct);
    }

    public async Task<CommerceResult<AuctionResponse>> BuyAsync(Guid account, Guid id, Guid request, CancellationToken ct)
    {
        var listing = await db.AuctionListings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        var buyerId = await transactions.CharacterIdAsync(account, ct);
        if (listing is null || buyerId is null) return CommerceResult.Failure<AuctionResponse>("auction_not_found");

        AuctionSaleNotification? notification = null;
        CommerceResult<AuctionResponse> result = await transactions.RunAsync(account, request, "AUCTION_BUY", new { id }, [listing.SellerId, buyerId.Value],
            async (characters, replay) =>
            {
                // Execution strategy retries and idempotent replays must never duplicate Telegram notifications.
                notification = null;
                var lot = await LockAsync(id, ct);
                if (replay) return Response(lot);
                var item = await transactions.ItemAsync(lot.ItemId, lot.SellerId, id, true, ct);
                lot.Sell(buyerId.Value, time.GetUtcNow());
                var buyer = characters.Single(x => x.Id == buyerId);
                var seller = characters.Single(x => x.Id == lot.SellerId);
                if (!buyer.TrySpendGold(lot.Price)) throw new CommerceRuleException("commerce_insufficient_funds");
                seller.AddGold(lot.Price - lot.Tax);
                // Delivery is durable in the same transaction, including when the buyer's bag is full.
                item.Transfer(id, buyer.Id, "MAILBOX");
                db.CommerceMails.Add(new CommerceMail(id, buyer.Id, item.Id, time.GetUtcNow()));

                long sellerTelegramUserId = await db.Accounts.AsNoTracking()
                    .Where(candidate => candidate.Id == seller.AccountId)
                    .Select(candidate => candidate.TelegramUserId)
                    .SingleAsync(ct);
                notification = new AuctionSaleNotification(sellerTelegramUserId, ResolveItemName(item));
                return Response(lot);
            }, ct);

        if (result.Succeeded && notification is not null && messageSender is not null)
        {
            try
            {
                await messageSender.SendAsync(
                    notification.TelegramUserId,
                    $"Ваш предмет «{notification.ItemName}» был куплен.",
                    CancellationToken.None);
            }
            catch
            {
                // The auction settlement is already committed. Telegram delivery is best-effort and must not roll it back.
            }
        }

        return result;
    }

    public async Task<CommerceResult<AuctionResponse>> ReturnAsync(Guid account, Guid id, Guid request, bool expired, CancellationToken ct)
    {
        var seller = await transactions.CharacterIdAsync(account, ct);
        if (seller is null) return CommerceResult.Failure<AuctionResponse>("commerce_character_not_found");
        return await transactions.RunAsync(account, request, expired ? "AUCTION_EXPIRE" : "AUCTION_CANCEL", new { id }, [seller.Value],
            async (_, replay) =>
            {
                var lot = await LockAsync(id, ct);
                if (lot.SellerId != seller) throw new CommerceRuleException("auction_not_owner");
                if (replay) return Response(lot);
                bool isExpired = time.GetUtcNow() >= lot.ExpiresAt;
                if (expired && !isExpired) throw new CommerceRuleException("auction_not_expired");
                var item = await transactions.ItemAsync(lot.ItemId, lot.SellerId, id, true, ct, verifyTradePolicy: false);
                lot.Return(isExpired);
                bool canReturnToBag = !isExpired && await InventoryCapacity.FreeSlotsAsync(db, lot.SellerId, content.GetCurrent(), ct) > 0;
                item.Transfer(id, lot.SellerId, canReturnToBag ? "INVENTORY" : "MAILBOX");
                if (!canReturnToBag)
                    db.CommerceMails.Add(new CommerceMail(id, lot.SellerId, item.Id, time.GetUtcNow()));
                return Response(lot);
            }, ct);
    }

    public async Task<CommerceResult<Guid>> ClaimAsync(Guid account, Guid id, Guid request, CancellationToken ct)
    {
        var owner = await transactions.CharacterIdAsync(account, ct);
        if (owner is null) return CommerceResult.Failure<Guid>("commerce_character_not_found");
        return await transactions.RunAsync(account, request, "MAIL_CLAIM", new { id }, [owner.Value], async (_, replay) =>
        {
            var mail = await db.CommerceMails.SingleOrDefaultAsync(x => x.Id == id && x.CharacterId == owner, ct)
                ?? throw new CommerceRuleException("mail_not_found");
            if (replay || mail.ClaimedAt.HasValue) return mail.ItemId;
            if (await InventoryCapacity.FreeSlotsAsync(db, owner.Value, content.GetCurrent(), ct) < 1)
                throw new CommerceRuleException("commerce_inventory_full");
            var item = await db.CharacterItems.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == mail.ItemId, ct);
            if (item is null || item.CharacterId != owner || item.Storage != "MAILBOX" || item.TransactionLockId != id)
                throw new CommerceRuleException("mail_item_unavailable");
            item.Transfer(id, owner.Value, "INVENTORY");
            mail.Claim(time.GetUtcNow());
            return item.Id;
        }, ct);
    }

    public async Task<MailResponse[]> MailAsync(Guid account, CancellationToken ct)
    {
        var owner = await transactions.CharacterIdAsync(account, ct);
        if (owner is null) return [];
        var entries = await (from mail in db.CommerceMails.AsNoTracking()
                join item in db.CharacterItems.IgnoreQueryFilters().AsNoTracking() on mail.ItemId equals item.Id
                join lot in db.AuctionListings.AsNoTracking() on mail.Id equals lot.Id
                where mail.CharacterId == owner && mail.ClaimedAt == null && item.Storage == "MAILBOX"
                orderby mail.CreatedAt
                select new { mail.Id, mail.ItemId, mail.CreatedAt, item.ItemDefinitionId, item.Quantity, lot.BuyerId })
            .Take(100).ToArrayAsync(ct);
        var definitions = content.GetCurrent().Package.Items?.ToDictionary(x => x.Id, StringComparer.Ordinal);
        return entries.Select(entry =>
        {
            var definition = definitions?.GetValueOrDefault(entry.ItemDefinitionId);
            return new MailResponse(entry.Id, entry.ItemId, entry.CreatedAt,
                entry.ItemDefinitionId, definition?.Name ?? entry.ItemDefinitionId, definition?.IconId, entry.Quantity,
                entry.BuyerId == owner ? "PURCHASE" : "RETURN");
        }).ToArray();
    }

    private string ResolveItemName(Elyndor.Core.Items.CharacterItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.GeneratedDisplayName)) return item.GeneratedDisplayName;
        return content.GetCurrent().Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out var definition)
            ? definition.Name
            : item.ItemDefinitionId;
    }

    private (long Fee, long Tax) Quote(long price)
    {
        if (price <= 0) throw new CommerceRuleException("auction_invalid_listing");
        long fee = checked((long)Math.Max(options.Value.MinimumListingFee, decimal.Ceiling(price * options.Value.ListingFeeRate)));
        long tax = checked((long)decimal.Ceiling(price * options.Value.SaleTaxRate));
        if (fee < 0 || tax < 0 || tax > price) throw new CommerceRuleException("auction_invalid_listing");
        return (fee, tax);
    }

    private static string Money(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private async Task<AuctionListing> LockAsync(Guid id, CancellationToken ct) =>
        await db.AuctionListings.FromSqlInterpolated($"SELECT * FROM game.auction_listings WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new CommerceRuleException("auction_not_found");
    public static AuctionResponse Response(AuctionListing x) => new(x.Id, x.State, x.SellerId, x.ItemId,
        Money(x.Price), Money(x.Fee), Money(x.Tax), x.BuyerId, x.ExpiresAt);

    private sealed record AuctionSaleNotification(long TelegramUserId, string ItemName);
}
