using System.Text.Json;
using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public static class GameContentPackageLoader
{
    public static async Task<GameContentPackage> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            GameContentPackage package =
                await GameContentJson.ReadRequiredAsync<GameContentPackage>(
                    path,
                    cancellationToken);

            package = await CategoryContentComposer.ComposeAsync(
                path,
                package,
                cancellationToken);

            IReadOnlyList<ContentValidationError> errors =
                ContentValidationPipeline.Default.Validate(package);
            if (errors.Count > 0)
                throw new ContentPackageValidationException(errors);

            _ = GameContentIndexes.For(package);
            return package;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Game content package '{path}' does not match the required JSON shape.",
                exception);
        }
    }
}
