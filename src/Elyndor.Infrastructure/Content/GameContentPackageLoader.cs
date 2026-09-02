using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;

namespace Elyndor.Infrastructure.Content;

public static class GameContentPackageLoader
{
    private const string MonsterOverlayFileName = "whispering-forest-monsters.json";
    private const string PhaseFiveOverlayFileName = "phase5-progression-items.json";
    private const string PhaseFiveLegacyItemsFileName = "phase5-legacy-items.json";
    private const string WarriorCombatBaselineOverlayFileName = "warrior-combat-baseline.json";

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
            package = await ApplyPhaseFiveOverlayAsync(path, package, cancellationToken);
            package = await ApplyPhaseFiveLegacyItemsAsync(path, package, cancellationToken);
            package = await ApplyWarriorCombatBaselineOverlayAsync(path, package, cancellationToken);

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
            ContentVersion = HigherVersion(package.ContentVersion, overlay.ContentVersion),
            BalanceVersion = HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
            PublishedAtUtc = Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
            Monsters = overlay.Monsters,
            MonsterAiProfiles = overlay.MonsterAiProfiles
        };
    }

    private static async Task<GameContentPackage> ApplyPhaseFiveOverlayAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory)) return package;

        string overlayPath = Path.Combine(directory, PhaseFiveOverlayFileName);
        if (!File.Exists(overlayPath)) return package;

        await using FileStream stream = File.OpenRead(overlayPath);
        PhaseFiveContentOverlay? overlay = await JsonSerializer.DeserializeAsync<PhaseFiveContentOverlay>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (overlay is null)
        {
            throw new InvalidDataException($"Phase 5 content overlay '{overlayPath}' is empty.");
        }

        return package with
        {
            ContentVersion = HigherVersion(package.ContentVersion, overlay.ContentVersion),
            BalanceVersion = HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
            PublishedAtUtc = Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
            LevelProgression = overlay.LevelProgression,
            Items = overlay.Items,
            LootTables = overlay.LootTables,
            EquipmentSets = overlay.EquipmentSets,
            Merchants = overlay.Merchants
        };
    }

    private static async Task<GameContentPackage> ApplyPhaseFiveLegacyItemsAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory)) return package;

        string overlayPath = Path.Combine(directory, PhaseFiveLegacyItemsFileName);
        if (!File.Exists(overlayPath)) return package;

        await using FileStream stream = File.OpenRead(overlayPath);
        LegacyItemsOverlay? overlay = await JsonSerializer.DeserializeAsync<LegacyItemsOverlay>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (overlay is null)
        {
            throw new InvalidDataException($"Legacy item overlay '{overlayPath}' is empty.");
        }

        ItemDefinition[] items = (package.Items ?? [])
            .Concat(overlay.Items)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return package with { Items = items };
    }

    private static async Task<GameContentPackage> ApplyWarriorCombatBaselineOverlayAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory)) return package;

        string overlayPath = Path.Combine(directory, WarriorCombatBaselineOverlayFileName);
        if (!File.Exists(overlayPath)) return package;

        await using FileStream stream = File.OpenRead(overlayPath);
        WarriorCombatBaselineOverlay? overlay =
            await JsonSerializer.DeserializeAsync<WarriorCombatBaselineOverlay>(
                stream,
                SerializerOptions,
                cancellationToken);
        if (overlay is null)
        {
            throw new InvalidDataException($"Warrior combat baseline overlay '{overlayPath}' is empty.");
        }

        IReadOnlyList<ClassProfile> profiles = package.ClassProfiles
            ?? throw new InvalidDataException("Class profiles are required for warrior combat baseline overlay.");
        if (!profiles.Any(profile => string.Equals(profile.Id, overlay.ClassId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Warrior combat baseline overlay references unknown class '{overlay.ClassId}'.");
        }

        return package with
        {
            ContentVersion = HigherVersion(package.ContentVersion, overlay.ContentVersion),
            BalanceVersion = HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
            PublishedAtUtc = Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
            ClassProfiles = profiles
                .Select(profile => string.Equals(profile.Id, overlay.ClassId, StringComparison.Ordinal)
                    ? profile with
                    {
                        StartingAbilityIds = overlay.StartingAbilityIds,
                        AbilityUnlocks = []
                    }
                    : profile)
                .ToArray()
        };
    }

    private static string HigherVersion(string current, string candidate)
    {
        if (Version.TryParse(current, out Version? currentVersion)
            && Version.TryParse(candidate, out Version? candidateVersion))
        {
            return candidateVersion > currentVersion ? candidate : current;
        }

        return string.CompareOrdinal(candidate, current) > 0 ? candidate : current;
    }

    private static DateTimeOffset Later(DateTimeOffset current, DateTimeOffset candidate) =>
        candidate > current ? candidate : current;

    private sealed record MonsterContentOverlay(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        IReadOnlyList<MonsterDefinition> Monsters,
        IReadOnlyList<MonsterAiProfile> MonsterAiProfiles);

    private sealed record PhaseFiveContentOverlay(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        LevelProgressionDefinition LevelProgression,
        IReadOnlyList<ItemDefinition> Items,
        IReadOnlyList<LootTableDefinition> LootTables,
        IReadOnlyList<EquipmentSetDefinition> EquipmentSets,
        IReadOnlyList<MerchantDefinition> Merchants);

    private sealed record LegacyItemsOverlay(IReadOnlyList<ItemDefinition> Items);

    private sealed record WarriorCombatBaselineOverlay(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        string ClassId,
        IReadOnlyList<string> StartingAbilityIds);
}
