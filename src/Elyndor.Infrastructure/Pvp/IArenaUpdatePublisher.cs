namespace Elyndor.Infrastructure.Pvp;

/// <summary>Post-commit notifications. Failures must never affect committed arena state.</summary>
public interface IArenaUpdatePublisher
{
    Task PublishMatchFoundAsync(Guid accountId, Guid matchId, CancellationToken cancellationToken);
    Task PublishMatchUpdatedAsync(Guid accountId, Guid matchId, long sequence, CancellationToken cancellationToken);
}

public sealed class NullArenaUpdatePublisher : IArenaUpdatePublisher
{
    public Task PublishMatchFoundAsync(Guid accountId, Guid matchId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task PublishMatchUpdatedAsync(Guid accountId, Guid matchId, long sequence,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
