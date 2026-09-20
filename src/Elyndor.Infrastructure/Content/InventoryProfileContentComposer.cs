using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

internal static class InventoryProfileContentComposer
{
    internal static async Task<GameContentPackage> ComposeAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? contentDirectory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(contentDirectory))
            return package;

        string directory = Path.Combine(contentDirectory, "inventory");
        if (!Directory.Exists(directory))
            return package;

        string[] files = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
            return package;
        if (files.Length > 1)
        {
            throw new InvalidDataException(
                "Inventory profile must be defined by exactly one content/inventory JSON file.");
        }

        InventoryProfileFragment fragment =
            await GameContentJson.ReadRequiredAsync<InventoryProfileFragment>(
                files[0],
                cancellationToken);
        if (fragment.InventoryProfile is null)
            throw new InvalidDataException($"Inventory content file '{files[0]}' does not define inventoryProfile.");

        return package with { InventoryProfile = fragment.InventoryProfile };
    }

    private sealed record InventoryProfileFragment(
        InventoryProfileDefinition? InventoryProfile = null);
}
