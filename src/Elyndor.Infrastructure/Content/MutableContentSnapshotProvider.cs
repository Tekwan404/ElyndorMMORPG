using System.Collections.Concurrent;
using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public sealed record ActiveContentRuntimeState(
    GameContentSnapshot Snapshot,
    Guid? RevisionId,
    Guid? ReleaseId);

public sealed class MutableContentSnapshotProvider : IContentSnapshotProvider
{
    private readonly ConcurrentDictionary<Guid, GameContentSnapshot> revisionCache = new();
    private ActiveContentRuntimeState current;

    public MutableContentSnapshotProvider(GameContentPackage initialPackage)
    {
        current = new ActiveContentRuntimeState(
            GameContentSnapshot.Create(
                initialPackage ?? throw new ArgumentNullException(nameof(initialPackage))),
            null,
            null);
    }

    public GameContentSnapshot GetCurrent() =>
        Volatile.Read(ref current).Snapshot;

    public ActiveContentRuntimeState GetRuntimeState() =>
        Volatile.Read(ref current);

    public int CachedRevisionCount => revisionCache.Count;

    internal GameContentSnapshot GetOrCreateRevisionSnapshot(
        Guid revisionId,
        GameContentPackage package)
    {
        if (revisionId == Guid.Empty)
            throw new ArgumentException("Revision id cannot be empty.", nameof(revisionId));
        ArgumentNullException.ThrowIfNull(package);

        return revisionCache.GetOrAdd(
            revisionId,
            _ => GameContentSnapshot.Create(package));
    }

    internal ActiveContentRuntimeState Activate(
        Guid revisionId,
        Guid releaseId,
        GameContentSnapshot snapshot)
    {
        if (revisionId == Guid.Empty)
            throw new ArgumentException("Revision id cannot be empty.", nameof(revisionId));
        if (releaseId == Guid.Empty)
            throw new ArgumentException("Release id cannot be empty.", nameof(releaseId));
        ArgumentNullException.ThrowIfNull(snapshot);

        ActiveContentRuntimeState next = new(
            snapshot,
            revisionId,
            releaseId);
        Interlocked.Exchange(ref current, next);
        return next;
    }
}

public sealed class ContentPublicationCoordinator
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
}
