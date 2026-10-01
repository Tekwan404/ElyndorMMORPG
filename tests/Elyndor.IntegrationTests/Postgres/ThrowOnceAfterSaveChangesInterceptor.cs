using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Elyndor.IntegrationTests.Postgres;

internal sealed class ThrowOnceAfterSaveChangesInterceptor : SaveChangesInterceptor
{
    private int _remainingFailures = 1;

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _remainingFailures, 0) == 1)
        {
            throw new InvalidOperationException(
                "Injected failure after SaveChanges and before the surrounding transaction commit.");
        }

        return ValueTask.FromResult(result);
    }
}