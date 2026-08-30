using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public static class GameContentPackageLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<GameContentPackage> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using FileStream stream = File.OpenRead(path);
            GameContentPackage? package = await JsonSerializer.DeserializeAsync<GameContentPackage>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (package is null)
            {
                throw new InvalidDataException($"Game content package '{path}' is empty.");
            }

            IReadOnlyList<ContentValidationError> errors =
                GameContentPackageValidator.Validate(package);

            if (errors.Count > 0)
            {
                throw new ContentPackageValidationException(errors);
            }

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
