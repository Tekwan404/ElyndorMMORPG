using System.Text.Json;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Progression;
using Elyndor.Core.Talents;
using Elyndor.Core.World;

namespace Elyndor.Infrastructure.Content;

internal static class CategoryContentComposer
{
    private static readonly string[] FragmentDirectories =
    [
        "abilities",
        "bosses",
        "classes",
        "effects",
        "items",
        "loot",
        "merchants",
        "monsters",
        "sets",
        "talents"
    ];

    internal static async Task<GameContentPackage> ComposeAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? contentDirectory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(contentDirectory))
            return package;

        foreach (string category in FragmentDirectories)
        {
            string directory = Path.Combine(contentDirectory, category);
            if (!Directory.Exists(directory))
                continue;

            foreach (string path in Directory
                         .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                ContentCategoryFragment fragment =
                    await GameContentJson.ReadRequiredAsync<ContentCategoryFragment>(
                        path,
                        cancellationToken);
                package = ComposeFragment(package, fragment);
            }
        }

        return await ComposeLocationsAsync(contentDirectory, package, cancellationToken);
    }

    private static async Task<GameContentPackage> ComposeLocationsAsync(
        string contentDirectory,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string directory = Path.Combine(contentDirectory, "locations");
        if (!Directory.Exists(directory))
            return package;

        HashSet<string> patchedLocationIds = new(StringComparer.Ordinal);
        foreach (string path in Directory
                     .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken);
            using JsonDocument document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("locationId", out _))
            {
                ContentCategoryFragment categoryFragment =
                    JsonSerializer.Deserialize<ContentCategoryFragment>(
                        json,
                        GameContentJson.SerializerOptions)
                    ?? throw new InvalidDataException($"Content file '{path}' is empty.");
                package = ComposeFragment(package, categoryFragment);
                continue;
            }

            LocationEncounterFragment fragment =
                JsonSerializer.Deserialize<LocationEncounterFragment>(
                    json,
                    GameContentJson.SerializerOptions)
                ?? throw new InvalidDataException($"Location content file '{path}' is empty.");

            if (!patchedLocationIds.Add(fragment.LocationId))
            {
                throw new InvalidDataException(
                    $"Location '{fragment.LocationId}' is patched by more than one category file.");
            }

            if (!package.Locations.Any(location =>
                    string.Equals(location.Id, fragment.LocationId, StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Location content file '{path}' references unknown location '{fragment.LocationId}'.");
            }

            package = package with
            {
                ContentVersion = ContentCompositionRules.HigherVersion(
                    package.ContentVersion,
                    fragment.ContentVersion),
                BalanceVersion = ContentCompositionRules.HigherVersion(
                    package.BalanceVersion,
                    fragment.BalanceVersion),
                PublishedAtUtc = ContentCompositionRules.Later(
                    package.PublishedAtUtc,
                    fragment.PublishedAtUtc),
                Locations = package.Locations
                    .Select(location => string.Equals(
                            location.Id,
                            fragment.LocationId,
                            StringComparison.Ordinal)
                        ? location with { Encounters = fragment.Encounters }
                        : location)
                    .ToArray()
            };
        }

        return package;
    }

    private static GameContentPackage ComposeFragment(
        GameContentPackage package,
        ContentCategoryFragment fragment)
    {
        string contentVersion = fragment.ContentVersion is null
            ? package.ContentVersion
            : ContentCompositionRules.HigherVersion(
                package.ContentVersion,
                fragment.ContentVersion);
        string balanceVersion = fragment.BalanceVersion is null
            ? package.BalanceVersion
            : ContentCompositionRules.HigherVersion(
                package.BalanceVersion,
                fragment.BalanceVersion);
        DateTimeOffset publishedAtUtc = fragment.PublishedAtUtc is null
            ? package.PublishedAtUtc
            : ContentCompositionRules.Later(
                package.PublishedAtUtc,
                fragment.PublishedAtUtc.Value);

        return package with
        {
            ContentVersion = contentVersion,
            BalanceVersion = balanceVersion,
            PublishedAtUtc = publishedAtUtc,
            Definitions = fragment.Definitions is null
                ? package.Definitions
                : ContentCompositionRules.MergeByKey(
                    package.Definitions,
                    fragment.Definitions,
                    item => (item.Type, item.Id)),
            Locations = fragment.Locations is null
                ? package.Locations
                : ContentCompositionRules.MergeByKey(
                    package.Locations,
                    fragment.Locations,
                    item => item.Id),
            ClassProfiles = ContentCompositionRules.MergeOptionalByKey(
                package.ClassProfiles,
                fragment.ClassProfiles,
                item => item.Id),
            StatFormula = fragment.StatFormula ?? package.StatFormula,
            ResourceProfiles = ContentCompositionRules.MergeOptionalByKey(
                package.ResourceProfiles,
                fragment.ResourceProfiles,
                item => item.Id),
            Effects = ContentCompositionRules.MergeOptionalByKey(
                package.Effects,
                fragment.Effects,
                item => item.Id),
            Abilities = ContentCompositionRules.MergeOptionalByKey(
                package.Abilities,
                fragment.Abilities,
                item => item.Id),
            TalentTrees = ContentCompositionRules.MergeOptionalByKey(
                package.TalentTrees,
                fragment.TalentTrees,
                item => item.Id),
            Monsters = ContentCompositionRules.MergeOptionalByKey(
                package.Monsters,
                fragment.Monsters,
                item => item.Id),
            MonsterAiProfiles = ContentCompositionRules.MergeOptionalByKey(
                package.MonsterAiProfiles,
                fragment.MonsterAiProfiles,
                item => item.Id),
            LevelProgression = fragment.LevelProgression ?? package.LevelProgression,
            Items = ContentCompositionRules.MergeOptionalByKey(
                package.Items,
                fragment.Items,
                item => item.Id),
            LootTables = ContentCompositionRules.MergeOptionalByKey(
                package.LootTables,
                fragment.LootTables,
                item => item.Id),
            EquipmentSets = ContentCompositionRules.MergeOptionalByKey(
                package.EquipmentSets,
                fragment.EquipmentSets,
                item => item.Id),
            Merchants = ContentCompositionRules.MergeOptionalByKey(
                package.Merchants,
                fragment.Merchants,
                item => item.Id),
            ResourceScaling = fragment.ResourceScaling ?? package.ResourceScaling
        };
    }

    private sealed record ContentCategoryFragment(
        string? ContentVersion = null,
        string? BalanceVersion = null,
        DateTimeOffset? PublishedAtUtc = null,
        IReadOnlyList<GameContentDefinition>? Definitions = null,
        IReadOnlyList<LocationDefinition>? Locations = null,
        IReadOnlyList<ClassProfile>? ClassProfiles = null,
        StatFormulaProfile? StatFormula = null,
        IReadOnlyList<ResourceProfile>? ResourceProfiles = null,
        IReadOnlyList<EffectDefinition>? Effects = null,
        IReadOnlyList<AbilityDefinition>? Abilities = null,
        IReadOnlyList<TalentTreeDefinition>? TalentTrees = null,
        IReadOnlyList<MonsterDefinition>? Monsters = null,
        IReadOnlyList<MonsterAiProfile>? MonsterAiProfiles = null,
        LevelProgressionDefinition? LevelProgression = null,
        IReadOnlyList<ItemDefinition>? Items = null,
        IReadOnlyList<LootTableDefinition>? LootTables = null,
        IReadOnlyList<EquipmentSetDefinition>? EquipmentSets = null,
        IReadOnlyList<MerchantDefinition>? Merchants = null,
        ResourceScalingProfile? ResourceScaling = null);

    private sealed record LocationEncounterFragment(
        string ContentVersion,
        string BalanceVersion,
        DateTimeOffset PublishedAtUtc,
        string LocationId,
        IReadOnlyList<LocationEncounterDefinition> Encounters);
}
