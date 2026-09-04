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

        private static readonly JsonGameContentJson.SerializerOptions GameContentJson.SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        static GameContentPackageLoader()
        {
            GameContentJson.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
                    GameContentJson.SerializerOptions,
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

        private static readonly JsonGameContentJson.SerializerOptions GameContentJson.SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        static GameContentPackageLoader()
        {
            GameContentJson.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
                    GameContentJson.SerializerOptions,
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

        private static readonly JsonGameContentJson.SerializerOptions GameContentJson.SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        static GameContentPackageLoader()
        {
            GameContentJson.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
                    GameContentJson.SerializerOptions,
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

        private static readonly JsonGameContentJson.SerializerOptions GameContentJson.SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        static GameContentPackageLoader()
        {
            GameContentJson.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
                    GameContentJson.SerializerOptions,
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

        private static readonly JsonGameContentJson.SerializerOptions GameContentJson.SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        static GameContentPackageLoader()
        {
            GameContentJson.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
                    GameContentJson.SerializerOptions,
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
