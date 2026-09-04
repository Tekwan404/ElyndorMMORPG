using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public sealed class StaticContentSnapshotProvider : IContentSnapshotProvider
{
    private readonly GameContentSnapshot snapshot;

    public StaticContentSnapshotProvider(GameContentPackage package)
    {
        snapshot = GameContentSnapshot.Create(
            package ?? throw new ArgumentNullException(nameof(package)));
    }

    public GameContentSnapshot GetCurrent() => snapshot;
}
