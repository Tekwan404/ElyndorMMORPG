using Elyndor.Core.Pvp;
using Microsoft.Extensions.Options;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaLobbyStatus(ArenaStatus Standing, ArenaQueueStatus Queue, Guid? RuntimeMatchId);

/// <summary>Application entry point for the arena lobby: feature gate, admission, queue, status.</summary>
public sealed class ArenaLobbyService(
    IOptions<ArenaOptions> options,
    ArenaEligibilityService eligibility,
    ArenaQueueService queue,
    ArenaReadService read,
    ArenaMatchRuntime runtime)
{
    public bool Enabled => options.Value.Enabled;

    public async Task<ArenaQueueMutationResult> JoinAsync(Guid accountId, ArenaQueueMode mode,
        CancellationToken cancellationToken)
    {
        ArenaQueueStatus status = await queue.GetStatusAsync(accountId, cancellationToken);
        if (!Enabled) return new ArenaQueueMutationResult(false, "arena_disabled", status);
        if (!Enum.IsDefined(mode))
            return new ArenaQueueMutationResult(false, "arena_queue_mode_invalid", status);
        // Idempotent re-join of the same queue must not depend on transient admission state.
        if (status.IsQueued && status.QueueMode == mode)
            return new ArenaQueueMutationResult(true, null, status);

        ArenaEligibilityResult admission = await eligibility.CheckAsync(accountId, cancellationToken);
        if (!admission.Eligible)
            return new ArenaQueueMutationResult(false, admission.ErrorCode ?? "arena_unavailable", status);
        return await queue.JoinAsync(accountId, mode, cancellationToken);
    }

    public async Task<ArenaQueueMutationResult> LeaveAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (!Enabled)
            return new ArenaQueueMutationResult(false, "arena_disabled",
                await queue.GetStatusAsync(accountId, cancellationToken));
        return await queue.LeaveAsync(accountId, cancellationToken);
    }

    public async Task<ArenaLobbyStatus?> StatusAsync(Guid accountId, CancellationToken cancellationToken)
    {
        Guid? characterId = await read.CharacterIdForAccountAsync(accountId, cancellationToken);
        if (characterId is null) return null;
        return new ArenaLobbyStatus(
            await read.StatusAsync(characterId.Value, cancellationToken),
            await queue.GetStatusAsync(accountId, cancellationToken),
            runtime.ActiveMatchId(accountId));
    }
}
