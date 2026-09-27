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
                var lots = await db.AuctionListings.AsNoTracking().Where(x => x.State == "ACTIVE" && x.ExpiresAt <= now)
                    .OrderBy(x => x.ExpiresAt).Take(100).ToArrayAsync(stoppingToken);
                foreach (var lot in lots)
                {
                    var account = await db.Characters.Where(x => x.Id == lot.SellerId).Select(x => x.AccountId).SingleAsync(stoppingToken);
                    var result = await auctions.ReturnAsync(account, lot.Id, Guid.NewGuid(), true, stoppingToken);
                    if (!result.Succeeded && result.ErrorCode != "auction_unavailable")
                        ExpirationRejected(logger, lot.Id, result.ErrorCode);
                }
                var expired = await db.PlayerTrades.AsNoTracking().Where(x => x.State == "OPEN" && x.ExpiresAt <= now)
                    .OrderBy(x => x.ExpiresAt).Take(100).ToArrayAsync(stoppingToken);
                foreach (var trade in expired)
                {
                    var account = await db.Characters.Where(x => x.Id == trade.CharacterAId).Select(x => x.AccountId).SingleAsync(stoppingToken);
                    await trades.ActAsync(account, trade.Id, Guid.NewGuid(), "CANCEL", trade.Revision, null, 0, "", stoppingToken);
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
