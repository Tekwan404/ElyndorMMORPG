using System.Text.Json;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Infrastructure.Content;

// Compatibility only: existing pre-D3 root overlays are composed here.
// New content belongs under content/<category>/*.json.
internal static class LegacyContentOverlayComposer
{
    private const string PhaseFiveOverlayFileName = "phase5-progression-items.json";
    private const string PhaseFiveLegacyItemsFileName = "phase5-legacy-items.json";
    private const string WarriorCombatBaselineOverlayFileName = "warrior-combat-baseline.json";
    private const string MagePyromancerOverlayFileName = "mage-pyromancer.json";
    private const string ResourceScalingOverlayFileName = "resource-scaling.json";

    internal static async Task<GameContentPackage> ComposeAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        package = await ApplyPhaseFiveOverlayAsync(packagePath, package, cancellationToken);
        package = await ApplyPhaseFiveLegacyItemsAsync(packagePath, package, cancellationToken);
        package = await ApplyWarriorCombatBaselineOverlayAsync(packagePath, package, cancellationToken);
        package = await ApplyMagePyromancerOverlayAsync(packagePath, package, cancellationToken);
        package = await ApplyResourceScalingOverlayAsync(packagePath, package, cancellationToken);
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
                GameContentJson.SerializerOptions,
                cancellationToken);
            if (overlay is null)
            {
                throw new InvalidDataException($"Phase 5 content overlay '{overlayPath}' is empty.");
            }

            return package with
            {
                ContentVersion = ContentCompositionRules.HigherVersion(package.ContentVersion, overlay.ContentVersion),
                BalanceVersion = ContentCompositionRules.HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
                PublishedAtUtc = ContentCompositionRules.Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
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
                GameContentJson.SerializerOptions,
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
                    GameContentJson.SerializerOptions,
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
                ContentVersion = ContentCompositionRules.HigherVersion(package.ContentVersion, overlay.ContentVersion),
                BalanceVersion = ContentCompositionRules.HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
                PublishedAtUtc = ContentCompositionRules.Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
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
                GameContentJson.SerializerOptions,
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
                ContentVersion = ContentCompositionRules.HigherVersion(package.ContentVersion, overlay.ContentVersion),
                BalanceVersion = ContentCompositionRules.HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
                PublishedAtUtc = ContentCompositionRules.Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
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
                GameContentJson.SerializerOptions,
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
                ContentVersion = ContentCompositionRules.HigherVersion(package.ContentVersion, overlay.ContentVersion),
                BalanceVersion = ContentCompositionRules.HigherVersion(package.BalanceVersion, overlay.BalanceVersion),
                PublishedAtUtc = ContentCompositionRules.Later(package.PublishedAtUtc, overlay.PublishedAtUtc),
                ResourceScaling = overlay.ResourceScaling
            };
        }


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
