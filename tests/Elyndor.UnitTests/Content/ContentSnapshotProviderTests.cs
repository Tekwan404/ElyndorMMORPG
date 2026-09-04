using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class ContentSnapshotProviderTests
{
    [Fact]
    public void StaticProviderReturnsOnePrebuiltSnapshot()
    {
        DateTimeOffset publishedAt = new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);
        GameContentPackage package = new(
            "content-1",
            "balance-1",
            publishedAt,
            [],
            []);

        StaticContentSnapshotProvider provider = new(package);

        GameContentSnapshot first = provider.GetCurrent();
        GameContentSnapshot second = provider.GetCurrent();

        Assert.Same(first, second);
        Assert.Same(package, first.Package);
        Assert.Equal("content-1", first.ContentVersion);
        Assert.Equal("balance-1", first.BalanceVersion);
        Assert.Equal(publishedAt, first.PublishedAtUtc);
        Assert.Empty(first.Indexes.LocationsById);
        Assert.Empty(first.WorldMap.Locations);
    }
}
