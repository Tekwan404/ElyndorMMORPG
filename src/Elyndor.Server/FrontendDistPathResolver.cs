namespace Elyndor.Server;

public static class FrontendDistPathResolver
{
    public static string Resolve(
        string? configuredPath,
        string packagedDirectoryName,
        string developmentFallbackPath,
        string? appBaseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagedDirectoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(developmentFallbackPath);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string baseDirectory = appBaseDirectory ?? AppContext.BaseDirectory;
        string packagedPath = Path.GetFullPath(
            Path.Combine(baseDirectory, packagedDirectoryName));
        if (File.Exists(Path.Combine(packagedPath, "index.html")))
        {
            return packagedPath;
        }

        return Path.GetFullPath(developmentFallbackPath);
    }
}
