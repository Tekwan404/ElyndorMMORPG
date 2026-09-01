using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.Infrastructure.Content;

public static class GameContentPackageLoader
{
    private const string MonsterOverlayFileName = "whispering-forest-monsters.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    static GameContentPackageLoader()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

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

            package = await ApplyMonsterOverlayAsync(path, package, cancellationToken);

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

    private static async Task<GameContentPackage> ApplyMonsterOverlayAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory)) return package;

        string overlayPath = Path.Combine(directory, MonsterOverlayFileName);
        if (!File.Exists(overlayPath)) return package;

        await using FileStream stream = File.OpenRead(overlayPath);
        MonsterContentOverlay? overlay = await JsonSerializer.DeserializeAsync<MonsterContentOverlay>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (overlay is null)
        {
            throw new InvalidDataException($"Monster content overlay '{overlayPath}' is empty.");
        }

        return package with
        {
            ContentVersion = overlay.ContentVersion,
            BalanceVersion = overlay.BalanceVersion,
            PublishedAtUtc = overlay.PublishedAtUtc,
            Monsters = overlay.Monsters,
            MonsterAiProfiles = overlay.MonsterAiProfiles
        };
    }

    private sealed record MonsterContentOverlay(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        IReadOnlyList<MonsterDefinition> Monsters,
        IReadOnlyList<MonsterAiProfile> MonsterAiProfiles);
}
