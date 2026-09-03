using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;
using Elyndor.Core.World;

namespace Elyndor.Infrastructure.Content;

public static class GameContentPackageLoader
{
    private const string MonsterOverlayFileName = "whispering-forest-monsters.json";
    private const string PhaseFiveOverlayFileName = "phase5-progression-items.json";
    private const string PhaseFiveLegacyItemsFileName = "phase5-legacy-items.json";
    private const string WarriorCombatBaselineOverlayFileName = "warrior-combat-baseline.json";
    private const string MagePyromancerOverlayFileName = "mage-pyromancer.json";
    private const string ResourceScalingOverlayFileName = "resource-scaling.json";
    private const string LocationOverlayDirectoryName = "locations";

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
            package = await ApplyLocationOverlaysAsync(path, package, cancellationToken);
            package = await ApplyPhaseFiveOverlayAsync(path, package, cancellationToken);
            package = await ApplyPhaseFiveLegacyItemsAsync(path, package, cancellationToken);
            package = await ApplyWarriorCombatBaselineOverlayAsync(path, package, cancellationToken);
            package = await ApplyMagePyromancerOverlayAsync(path, package, cancellationToken);
            package = await ApplyResourceScalingOverlayAsync(path, package, cancellationToken);

            ContentValidationError[] errors = GameContentPackageValidator.Validate(package)
                .Concat(WorldEncounterContentValidator.Validate(package))
                .ToArray();

            if (errors.Length > 0)
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

    private static async Task<GameContentPackage> ApplyLocationOverlaysAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory)) return package;

        string locationDirectory = Path.Combine(directory, LocationOverlayDirectoryName);
        if (!Directory.Exists(locationDirectory)) return package;

        HashSet<string> overlaidLocationIds = new(StringComparer.Ordinal);
        foreach (string overlayPath in Directory
                     .EnumerateFiles(locationDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            await using FileStream stream = File.OpenRead(overlayPath);
            LocationContentOverlay? overlay = await JsonSerializer.DeserializeAsync<LocationContentOverlay>(
                stream,
                SerializerOptions,
                cancellationToken);
            if (overlay is null)
                throw new InvalidDataException($"Location content overlay '{overlayPath}' is empty.");
            if (!overlaidLocationIds.Add(overlay.LocationId))
            {
                throw new InvalidDataException(
                    $"Location '{overlay.LocationId}' is defined by more than one location overlay.");
            }
            if (!package.Locations.Any(location =>
                    string.Equals(location.Id, overlay.LocationId, StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Location content overlay '{overlayPath}' references unknown location '{overlay.LocationId}'.");
            }

            package = package with
            {
                ContentVersion = HigherVersion(package.ContentVersion, overlay.ContentVersion),
                BalanceVersion = HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
                PublishedAtUtc = Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
                Locations = package.Locations
                    .Select(location => string.Equals(
                            location.Id,
                            overlay.LocationId,
                            StringComparison.Ordinal)
                        ? location with { Encounters = overlay.Encounters }
                        : location)
                    .ToArray()
            };
        }

        return package;
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

    private static async Task<GameContentPackage> ApplyMagePyromancerOverlayAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory)) return package;

        string overlayPath = Path.Combine(directory, MagePyromancerOverlayFileName);
        if (!File.Exists(overlayPath)) return package;

        await using FileStream stream = File.OpenRead(overlayPath);
        MagePyromancerOverlay? overlay = await JsonSerializer.DeserializeAsync<MagePyromancerOverlay>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (overlay is null)
        {
            throw new InvalidDataException($"Mage/Pyromancer content overlay '{overlayPath}' is empty.");
        }

        IReadOnlyList<ClassProfile> profiles = package.ClassProfiles
            ?? throw new InvalidDataException("Class profiles are required for mage combat overlay.");
        if (!profiles.Any(profile => string.Equals(profile.Id, overlay.ClassId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Mage combat overlay references unknown class '{overlay.ClassId}'.");
        }

        GameContentDefinition[] definitions = package.Definitions
            .Concat(overlay.Definitions)
            .GroupBy(item => (item.Type, item.Id))
            .Select(group => group.Last())
            .ToArray();
        AbilityDefinition[] abilities = (package.Abilities ?? [])
            .Concat(overlay.Abilities)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        TalentTreeDefinition[] talentTrees = (package.TalentTrees ?? [])
            .Concat([overlay.TalentTree])
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();

        return package with
        {
            ContentVersion = HigherVersion(package.ContentVersion, overlay.ContentVersion),
            BalanceVersion = HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
            PublishedAtUtc = Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
            Definitions = definitions,
            Abilities = abilities,
            TalentTrees = talentTrees,
            ClassProfiles = profiles
                .Select(profile => string.Equals(profile.Id, overlay.ClassId, StringComparison.Ordinal)
                    ? profile with
                    {
                        StartingAbilityIds = overlay.StartingAbilityIds,
                        AbilityUnlocks = [],
                        CombatAutoAttack = overlay.CombatAutoAttack,
                        AllowedWeaponCategories = overlay.AllowedWeaponCategories,
                        AllowedArmorCategories = overlay.AllowedArmorCategories
                    }
                    : profile)
                .ToArray()
        };
    }

    private static async Task<GameContentPackage> ApplyResourceScalingOverlayAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory)) return package;

        string overlayPath = Path.Combine(directory, ResourceScalingOverlayFileName);
        if (!File.Exists(overlayPath)) return package;

        await using FileStream stream = File.OpenRead(overlayPath);
        ResourceScalingOverlay? overlay = await JsonSerializer.DeserializeAsync<ResourceScalingOverlay>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (overlay is null)
        {
            throw new InvalidDataException($"Resource scaling overlay '{overlayPath}' is empty.");
        }
        if (overlay.ResourceScaling.ManaBase < 0 || overlay.ResourceScaling.ManaPerIntellect < 0)
        {
            throw new InvalidDataException(
                $"Resource scaling overlay '{overlayPath}' contains negative mana scaling values.");
        }

        return package with
        {
            ContentVersion = HigherVersion(package.ContentVersion, overlay.ContentVersion),
            BalanceVersion = HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
            PublishedAtUtc = Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
            ResourceScaling = overlay.ResourceScaling
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

    private sealed record LocationContentOverlay(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        string LocationId,
        IReadOnlyList<LocationEncounterDefinition> Encounters);

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

    private sealed record MagePyromancerOverlay(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        string ClassId,
        IReadOnlyList<string> StartingAbilityIds,
        AutoAttackProfile CombatAutoAttack,
        IReadOnlyList<string> AllowedWeaponCategories,
        IReadOnlyList<string> AllowedArmorCategories,
        IReadOnlyList<GameContentDefinition> Definitions,
        IReadOnlyList<AbilityDefinition> Abilities,
        TalentTreeDefinition TalentTree);

    private sealed record ResourceScalingOverlay(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        ResourceScalingProfile ResourceScaling);
}
