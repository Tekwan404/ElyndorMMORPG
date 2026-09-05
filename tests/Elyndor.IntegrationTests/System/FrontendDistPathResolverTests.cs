using Elyndor.Server;

namespace Elyndor.IntegrationTests.System;

public sealed class FrontendDistPathResolverTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), $"elyndor-frontends-{Guid.NewGuid():N}");

    [Fact]
    public void PrefersExplicitConfiguration()
    {
        string configured = Path.Combine(root, "configured");
        Directory.CreateDirectory(configured);

        string result = FrontendDistPathResolver.Resolve(
            configured,
            "frontend-admin",
            Path.Combine(root, "dev"),
            root);

        Assert.Equal(Path.GetFullPath(configured), result);
    }

    [Fact]
    public void UsesPackagedFrontendWhenReleaseContainsIndex()
    {
        string packaged = Path.Combine(root, "frontend-admin");
        Directory.CreateDirectory(packaged);
        File.WriteAllText(Path.Combine(packaged, "index.html"), "<html></html>");

        string result = FrontendDistPathResolver.Resolve(
            configuredPath: null,
            packagedDirectoryName: "frontend-admin",
            developmentFallbackPath: Path.Combine(root, "dev"),
            appBaseDirectory: root);

        Assert.Equal(Path.GetFullPath(packaged), result);
    }

    [Fact]
    public void FallsBackToDevelopmentPathWithoutPackagedFrontend()
    {
        string development = Path.Combine(root, "web", "elyndor-admin", "dist");

        string result = FrontendDistPathResolver.Resolve(
            configuredPath: null,
            packagedDirectoryName: "frontend-admin",
            developmentFallbackPath: development,
            appBaseDirectory: root);

        Assert.Equal(Path.GetFullPath(development), result);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
