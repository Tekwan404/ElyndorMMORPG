using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterOperationGuardTests
{
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

    private sealed class FakeCombatActivityReader : ICombatActivityReader
    {
        public bool Active { get; set; }

        public bool HasActiveCombat(Guid accountId) => Active;
    }
}
