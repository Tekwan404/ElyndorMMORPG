using System.Reflection;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterOperationGuardTests
{
    private const int StripeCount = 256;

    [Fact]
    public async Task OutOfCombatOperationIsRejectedWhenCombatIsActive()
    {
        FakeCombatActivityReader combat = new() { Active = true };
        CharacterOperationGuard guard = new(combat);
        bool executed = false;

        string result = await guard.ExecuteOutOfCombatAsync(
            Guid.CreateVersion7(),
            () =>
            {
                executed = true;
                return Task.FromResult("executed");
            },
            () => "blocked",
            CancellationToken.None);

        Assert.Equal("blocked", result);
        Assert.False(executed);
    }

    [Fact]
    public async Task CombatStartStripeSerializesAgainstFollowingWorldMutation()
    {
        FakeCombatActivityReader combat = new();
        CharacterOperationGuard guard = new(combat);
        Guid accountId = Guid.CreateVersion7();
        TaskCompletionSource<bool> entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string> start = guard.ExecuteExclusiveAsync(
            accountId,
            async () =>
            {
                entered.SetResult(true);
                await release.Task;
                combat.Active = true;
                return "combat-started";
            },
            CancellationToken.None);

        await entered.Task;

        Task<string> worldMutation = guard.ExecuteOutOfCombatAsync(
            accountId,
            () => Task.FromResult("world-mutated"),
            () => "blocked",
            CancellationToken.None);

        Assert.False(worldMutation.IsCompleted);
        release.SetResult(true);

        Assert.Equal("combat-started", await start);
        Assert.Equal("blocked", await worldMutation);
    }

    [Fact]
    public async Task AcquireManyWithReversedOrderDoesNotDeadlock()
    {
        CharacterOperationGuard guard = new(new FakeCombatActivityReader());
        (Guid firstAccount, Guid secondAccount) = CreateAccountsOnDistinctOrderedStripes();
        IDisposable blocker = await guard.AcquireManyAsync(
            [firstAccount, secondAccount],
            CancellationToken.None);

        var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task first = HoldAsync(
            guard,
            [firstAccount, secondAccount],
            firstEntered,
            releaseFirst);
        Task second = HoldAsync(
            guard,
            [secondAccount, firstAccount],
            secondEntered,
            releaseSecond);

        blocker.Dispose();

        Task winner = await Task.WhenAny(firstEntered.Task, secondEntered.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
        if (ReferenceEquals(winner, firstEntered.Task))
        {
            releaseFirst.TrySetResult(true);
            await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            releaseSecond.TrySetResult(true);
        }
        else
        {
            releaseSecond.TrySetResult(true);
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            releaseFirst.TrySetResult(true);
        }

        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AcquireManyDeduplicatesRepeatedKeys()
    {
        CharacterOperationGuard guard = new(new FakeCombatActivityReader());
        Guid accountId = Guid.CreateVersion7();
        SemaphoreSlim gate = GateFor(guard, accountId);

        IDisposable lease = await guard.AcquireManyAsync(
                [accountId, accountId, accountId],
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, gate.CurrentCount);
        lease.Dispose();
        Assert.Equal(1, gate.CurrentCount);

        using IDisposable reacquired = await guard.AcquireManyAsync(
                [accountId],
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, gate.CurrentCount);
    }

    [Fact]
    public async Task CancellationReleasesAlreadyAcquiredEarlierKeys()
    {
        CharacterOperationGuard guard = new(new FakeCombatActivityReader());
        (Guid lowAccount, Guid highAccount) = CreateAccountsOnDistinctOrderedStripes();
        SemaphoreSlim lowGate = GateFor(guard, lowAccount);
        SemaphoreSlim highGate = GateFor(guard, highAccount);
        using IDisposable highBlocker = await guard.AcquireManyAsync(
            [highAccount],
            CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        Task<IDisposable> pending = guard.AcquireManyAsync(
            [lowAccount, highAccount],
            cancellation.Token);

        Assert.Equal(0, lowGate.CurrentCount);
        Assert.Equal(0, highGate.CurrentCount);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, lowGate.CurrentCount);
        Assert.Equal(0, highGate.CurrentCount);
    }

    [Fact]
    public async Task DisposeReleasesEveryAcquiredGateAndIsIdempotent()
    {
        CharacterOperationGuard guard = new(new FakeCombatActivityReader());
        (Guid firstAccount, Guid secondAccount) = CreateAccountsOnDistinctOrderedStripes();
        SemaphoreSlim firstGate = GateFor(guard, firstAccount);
        SemaphoreSlim secondGate = GateFor(guard, secondAccount);

        IDisposable lease = await guard.AcquireManyAsync(
            [firstAccount, secondAccount],
            CancellationToken.None);

        Assert.Equal(0, firstGate.CurrentCount);
        Assert.Equal(0, secondGate.CurrentCount);
        lease.Dispose();
        lease.Dispose();
        Assert.Equal(1, firstGate.CurrentCount);
        Assert.Equal(1, secondGate.CurrentCount);
    }

    private static async Task HoldAsync(
        CharacterOperationGuard guard,
        Guid[] accountIds,
        TaskCompletionSource<bool> entered,
        TaskCompletionSource<bool> release)
    {
        using IDisposable lease = await guard.AcquireManyAsync(
            accountIds,
            CancellationToken.None);
        entered.TrySetResult(true);
        await release.Task;
    }

    private static (Guid LowAccount, Guid HighAccount) CreateAccountsOnDistinctOrderedStripes()
    {
        Guid first = Guid.CreateVersion7();
        int firstStripe = Stripe(first);
        while (true)
        {
            Guid second = Guid.CreateVersion7();
            int secondStripe = Stripe(second);
            if (secondStripe == firstStripe)
                continue;

            return firstStripe < secondStripe
                ? (first, second)
                : (second, first);
        }
    }

    private static SemaphoreSlim GateFor(CharacterOperationGuard guard, Guid accountId)
    {
        FieldInfo gatesField = typeof(CharacterOperationGuard).GetField(
            "_gates",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CharacterOperationGuard gates were not found.");
        var gates = (SemaphoreSlim[])gatesField.GetValue(guard)!;
        return gates[Stripe(accountId)];
    }

    private static int Stripe(Guid accountId)
    {
        uint hash = unchecked((uint)accountId.GetHashCode());
        return (int)(hash % StripeCount);
    }

    private sealed class FakeCombatActivityReader : ICombatActivityReader
    {
        public bool Active { get; set; }

        public bool HasActiveCombat(Guid accountId) => Active;
    }
}
