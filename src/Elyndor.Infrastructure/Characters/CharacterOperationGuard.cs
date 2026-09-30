using Elyndor.Infrastructure.Combat;

namespace Elyndor.Infrastructure.Characters;

public static class CharacterOperationErrorCodes
{
    public const string InCombat = "character_in_combat";
}

/// <summary>
/// Serializes state-changing player operations against combat start on a bounded set of
/// process-local stripes. The combat registry remains the authority for whether an account
/// currently owns an active CombatSession.
/// </summary>
public sealed class CharacterOperationGuard(ICombatActivityReader combatActivity)
{
    private const int StripeCount = 256;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, StripeCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public async Task<IDisposable> AcquireManyAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        int[] stripeIndexes = accountIds
            .Select(GetStripeIndex)
            .Distinct()
            .OrderBy(static stripeIndex => stripeIndex)
            .ToArray();

        var acquired = new List<SemaphoreSlim>(stripeIndexes.Length);
        try
        {
            foreach (int stripeIndex in stripeIndexes)
            {
                SemaphoreSlim gate = _gates[stripeIndex];
                await gate.WaitAsync(cancellationToken);
                acquired.Add(gate);
            }

            return new MultiGateLease(acquired);
        }
        catch
        {
            for (int index = acquired.Count - 1; index >= 0; index--)
                acquired[index].Release();

            throw;
        }
    }

    public async Task<T> ExecuteOutOfCombatAsync<T>(
        Guid accountId,
        Func<Task<T>> operation,
        Func<T> blockedResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(blockedResult);
        SemaphoreSlim gate = GetGate(accountId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (combatActivity.HasActiveCombat(accountId))
                return blockedResult();

            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> ExecuteExclusiveAsync<T>(
        Guid accountId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        SemaphoreSlim gate = GetGate(accountId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim GetGate(Guid accountId)
    {
        return _gates[GetStripeIndex(accountId)];
    }

    private static int GetStripeIndex(Guid accountId)
    {
        uint hash = unchecked((uint)accountId.GetHashCode());
        return (int)(hash % StripeCount);
    }

    private sealed class MultiGateLease(IReadOnlyList<SemaphoreSlim> gates) : IDisposable
    {
        private IReadOnlyList<SemaphoreSlim>? _gates = gates;

        public void Dispose()
        {
            IReadOnlyList<SemaphoreSlim>? gates = Interlocked.Exchange(ref _gates, null);
            if (gates is null)
                return;

            for (int index = gates.Count - 1; index >= 0; index--)
                gates[index].Release();
        }
    }
}
