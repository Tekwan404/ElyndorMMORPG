using Elyndor.Core.World;

namespace Elyndor.Core.Content;

public sealed class GameContentSnapshot
{
    private GameContentSnapshot(GameContentPackage package, bool directTravel)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));
        Indexes = GameContentIndexes.For(package);
        WorldMap = new WorldMap(package.Locations, directTravel);
    }

    public GameContentPackage Package { get; }
    public GameContentIndexes Indexes { get; }
    public WorldMap WorldMap { get; }

    public string ContentVersion => Package.ContentVersion;
    public string BalanceVersion => Package.BalanceVersion;
    public DateTimeOffset PublishedAtUtc => Package.PublishedAtUtc;

    public static GameContentSnapshot Create(
        GameContentPackage package,
        bool directTravel = false) =>
        new(package, directTravel);
}

public interface IContentSnapshotProvider
{
    GameContentSnapshot GetCurrent();
}
