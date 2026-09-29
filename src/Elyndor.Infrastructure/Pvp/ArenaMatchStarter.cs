using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Pvp;

/// <summary>
/// Turns a committed matchmaking row into a running combat. A match becomes live only when both
/// fighters were assembled and registered; any failure cancels it (no rating/Honor) and returns
/// the healthy participant to the queue.
/// </summary>
public sealed class ArenaMatchStarter(
    GameDbContext db,
    ArenaEligibilityService eligibility,
    ArenaMatchRuntime runtime,
    ArenaSettlementService settlement,
    ArenaQueueService queue,
    IArenaUpdatePublisher publisher,
    ILogger<ArenaMatchStarter> logger)
{
    public async Task StartAsync(ArenaMatchCreated created, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Guid> accounts = await db.Characters.AsNoTracking()
            .Where(x => x.Id == created.CharacterAId || x.Id == created.CharacterBId)
            .ToDictionaryAsync(x => x.Id, x => x.AccountId, cancellationToken);
        if (!accounts.TryGetValue(created.CharacterAId, out Guid accountA)
            || !accounts.TryGetValue(created.CharacterBId, out Guid accountB))
        {
            await CancelAsync(created, false, false, cancellationToken);
            return;
        }

        ArenaEligibilityResult first = await CheckAsync(accountA, cancellationToken);
        ArenaEligibilityResult second = await CheckAsync(accountB, cancellationToken);
        if (first.Entrant is not { } entrantA || second.Entrant is not { } entrantB)
        {
            await CancelAsync(created, first.Eligible, second.Eligible, cancellationToken);
            return;
        }

        try
        {
            runtime.Register(created.MatchId, entrantA, entrantB);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Arena match {MatchId} could not be registered.", created.MatchId);
            await CancelAsync(created, true, true, cancellationToken);
            return;
        }

        foreach (Guid accountId in new[] { accountA, accountB })
        {
            try
            {
                await publisher.PublishMatchFoundAsync(accountId, created.MatchId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Arena match-found notification failed for {MatchId}.", created.MatchId);
            }
        }
    }

    private async Task<ArenaEligibilityResult> CheckAsync(Guid accountId, CancellationToken cancellationToken)
    {
        try
        {
            return await eligibility.CheckAsync(accountId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Arena eligibility check failed.");
            return new ArenaEligibilityResult(null, "arena_start_failed");
        }
    }

    private async Task CancelAsync(ArenaMatchCreated created, bool requeueA, bool requeueB,
        CancellationToken cancellationToken)
    {
        await settlement.CompleteAndSettleAsync(created.MatchId, ArenaMatchOutcome.Cancelled, false,
            cancellationToken);
        if (requeueA) await queue.RequeueAsync(created.CharacterAId, created.Mode, cancellationToken);
        if (requeueB) await queue.RequeueAsync(created.CharacterBId, created.Mode, cancellationToken);
    }
}
