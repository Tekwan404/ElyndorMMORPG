using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Economy;

public sealed partial class CommerceExpiryWorker(IServiceScopeFactory scopes, TimeProvider time,
    ILogger<CommerceExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                var auctions = scope.ServiceProvider.GetRequiredService<AuctionSettlementService>();
                var trades = scope.ServiceProvider.GetRequiredService<PlayerTradeService>();
                var now = time.GetUtcNow();
                var lots = await (
                        from lot in db.AuctionListings.AsNoTracking()
                        join seller in db.Characters.AsNoTracking()
                            on lot.SellerId equals seller.Id
                        where lot.State == "ACTIVE" && lot.ExpiresAt <= now
                        orderby lot.ExpiresAt
                        select new
                        {
                            ListingId = lot.Id,
                            lot.SellerId,
                            seller.AccountId
                        })
                    .Take(100)
                    .ToArrayAsync(stoppingToken);
                foreach (var lot in lots)
                {
                    var result = await auctions.ReturnExpiredAsync(
                        lot.AccountId,
                        lot.SellerId,
                        lot.ListingId,
                        Guid.NewGuid(),
                        stoppingToken);
                    if (!result.Succeeded && result.ErrorCode != "auction_unavailable")
                        ExpirationRejected(logger, lot.ListingId, result.ErrorCode);
                }
                var expired = await (
                        from trade in db.PlayerTrades.AsNoTracking()
                        join character in db.Characters.AsNoTracking()
                            on trade.CharacterAId equals character.Id
                        where trade.State == "OPEN" && trade.ExpiresAt <= now
                        orderby trade.ExpiresAt
                        select new
                        {
                            Trade = trade,
                            character.AccountId
                        })
                    .Take(100)
                    .ToArrayAsync(stoppingToken);
                foreach (var entry in expired)
                {
                    await trades.ActAsync(
                        entry.AccountId,
                        entry.Trade.Id,
                        Guid.NewGuid(),
                        "CANCEL",
                        entry.Trade.Revision,
                        null,
                        0,
                        "",
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { ExpirationFailed(logger, ex); }
        }
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Auction expiration {ListingId}: {ErrorCode}")]
    private static partial void ExpirationRejected(ILogger logger, Guid listingId, string? errorCode);
    [LoggerMessage(Level = LogLevel.Error, Message = "Commerce expiration failed; retrying next sweep")]
    private static partial void ExpirationFailed(ILogger logger, Exception exception);
}
