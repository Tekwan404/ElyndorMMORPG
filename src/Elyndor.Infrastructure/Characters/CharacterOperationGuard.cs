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
        uint hash = unchecked((uint)accountId.GetHashCode());
        return _gates[hash % StripeCount];
    }
}
