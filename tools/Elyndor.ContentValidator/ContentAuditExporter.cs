using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Items;

namespace Elyndor.ContentValidator;

internal static class ContentAuditExporter
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static async Task ExportAsync(
        GameContentPackage package,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        await WriteAsync(
            Path.Combine(outputDirectory, "01-items.json"),
            BuildItems(package),
            cancellationToken);
        await WriteAsync(
            Path.Combine(outputDirectory, "02-enemies.json"),
            BuildEnemies(package),
            cancellationToken);
        await WriteAsync(
            Path.Combine(outputDirectory, "03-locations.json"),
            BuildLocations(package),
            cancellationToken);
    }

    private static Dictionary<string, object?> BuildItems(GameContentPackage package)
    {
        IReadOnlyList<ItemDefinition> items = package.Items ?? [];
        IReadOnlyList<EquipmentSetDefinition> sets = package.EquipmentSets ?? [];
        IReadOnlyList<LootTableDefinition> lootTables = package.LootTables ?? [];
        ItemizationDefinition? itemization = package.Itemization;

        Dictionary<string, ItemAffixPoolDefinition> pools = (itemization?.AffixPools ?? [])
            .ToDictionary(pool => pool.Id, StringComparer.Ordinal);
        Dictionary<string, ItemAffixCountProfileDefinition> countProfiles =
            (itemization?.AffixCountProfiles ?? [])
            .ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        Dictionary<string, EquipmentSetDefinition> setsById = sets
            .ToDictionary(set => set.Id, StringComparer.Ordinal);

        object[] rows = items
            .OrderBy(item => item.RequiredLevel)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item =>
            {
                string[] guaranteed = (item.GuaranteedAffixStatIds ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                string[] random = item.RandomAffixPoolId is not null
                    && pools.TryGetValue(item.RandomAffixPoolId, out ItemAffixPoolDefinition? pool)
                        ? pool.StatIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                        : [];
                string[] possibleStatIds = guaranteed
                    .Concat(random)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                EquipmentSetDefinition? set = item.SetId is not null
                    && setsById.TryGetValue(item.SetId, out EquipmentSetDefinition? foundSet)
                        ? foundSet
                        : null;

                return (object)new
                {
                    item.Id,
                    item.Name,
                    Level = item.RequiredLevel,
                    item.Description,
                    item.Type,
                    item.Rarity,
                    item.IconId,
                    item.Version,
                    Stats = new
                    {
                        item.Stats.Strength,
                        item.Stats.Agility,
                        item.Stats.Intellect,
                        item.Stats.Stamina,
                        item.MaxHpFlat,
                        item.AttackPowerFlat,
                        item.SpellPowerFlat,
                        item.CriticalChancePercent,
                        item.CriticalDamagePercent,
                        item.AccuracyPercent,
                        item.AttackSpeedPercent,
                        item.DodgePercent,
                        item.ArmorFlat,
                        item.MagicResistanceFlat,
                        item.ArmorPenetrationPercent,
                        item.MagicPenetrationPercent,
                        item.MaxResourceFlat,
                        item.WeaponDamageMin,
                        item.WeaponDamageMax,
                        item.BlockChancePercent,
                        item.BlockValueMin,
                        item.BlockValueMax
                    },
                    Equipment = new
                    {
                        item.Slot,
                        item.WeaponCategory,
                        item.ArmorCategory,
                        item.OffHandCategory,
                        item.WeaponBaseAttackIntervalSeconds,
                        item.AppearanceProfileId,
                        item.UniqueEquippedGroup
                    },
                    Stack = new { item.Stackable, item.MaxStack },
                    Economy = new
                    {
                        item.BuyPriceGold,
                        item.SellPriceGold,
                        item.TradePolicyId,
                        item.PremiumEligible,
                        item.InventoryCapacityBonus
                    },
                    Consumable = item.ConsumableActions is null
                        ? null
                        : new
                        {
                            item.ConsumableCooldownSeconds,
                            item.ConsumableCooldownCategoryId,
                            Actions = item.ConsumableActions
                        },
                    Generation = new
                    {
                        item.ItemLevelMin,
                        item.ItemLevelMax,
                        item.PrimaryStatRanges,
                        GuaranteedAffixStatIds = guaranteed,
                        item.RandomAffixPoolId,
                        RandomAffixStatIds = random,
                        item.AffixCountProfileId,
                        AffixCountProfile = item.AffixCountProfileId is not null
                            && countProfiles.TryGetValue(item.AffixCountProfileId, out ItemAffixCountProfileDefinition? profile)
                                ? profile
                                : null,
                        item.ExtraAffixBudgetCap,
                        item.PrefixSuffixPolicyId,
                        item.GenerationVersion
                    },
                    PossibleAffixes = possibleStatIds.Select(statId => new
                    {
                        StatId = statId,
                        IsGuaranteed = guaranteed.Contains(statId, StringComparer.Ordinal),
                        IsRandom = random.Contains(statId, StringComparer.Ordinal),
                        Names = (itemization?.AffixNames ?? [])
                            .Where(name => string.Equals(name.StatId, statId, StringComparison.Ordinal))
                            .OrderBy(name => name.Kind, StringComparer.Ordinal)
                            .Select(name => new
                            {
                                name.Id,
                                name.Kind,
                                name.Low,
                                name.Medium,
                                name.High
                            })
                    }),
                    Set = set is null
                        ? null
                        : new
                        {
                            set.Id,
                            set.Name,
                            PieceItemIds = items
                                .Where(candidate => string.Equals(candidate.SetId, set.Id, StringComparison.Ordinal))
                                .Select(candidate => candidate.Id)
                                .Order(StringComparer.Ordinal),
                            set.Bonuses
                        },
                    Acquisition = new
                    {
                        LootTableIds = lootTables
                            .Where(table => ContainsItem(table, item.Id))
                            .Select(table => table.Id)
                            .Order(StringComparer.Ordinal),
                        MerchantIds = (package.Merchants ?? [])
                            .Where(merchant => merchant.ItemIds.Contains(item.Id, StringComparer.Ordinal))
                            .Select(merchant => merchant.Id)
                            .Order(StringComparer.Ordinal),
                        PremiumOffers = (package.PremiumStoreOffers ?? [])
                            .Where(offer => string.Equals(offer.ItemDefinitionId, item.Id, StringComparison.Ordinal))
                            .OrderBy(offer => offer.Sku, StringComparer.Ordinal),
                        ProfessionRecipes = (package.ProfessionRecipes ?? [])
                            .Where(recipe => string.Equals(recipe.OutputItemId, item.Id, StringComparison.Ordinal))
                            .OrderBy(recipe => recipe.Id, StringComparer.Ordinal),
                        SkinningSources = (package.SkinningSources ?? [])
                            .Where(source => string.Equals(source.ItemId, item.Id, StringComparison.Ordinal))
                            .OrderBy(source => source.Id, StringComparer.Ordinal),
                        StartingClasses = (package.ClassProfiles ?? [])
                            .Where(profile => profile.StartingEquipmentItemIds?.Contains(item.Id, StringComparer.Ordinal) == true)
                            .Select(profile => profile.Id)
                            .Order(StringComparer.Ordinal)
                    }
                };
            })
            .ToArray();

        return Envelope(package, "items", rows.Length, rows);
    }

    private static Dictionary<string, object?> BuildEnemies(GameContentPackage package)
    {
        var abilitiesById = (package.Abilities ?? [])
            .ToDictionary(ability => ability.Id, StringComparer.Ordinal);
        var aiProfilesById = (package.MonsterAiProfiles ?? [])
            .ToDictionary(profile => profile.Id, StringComparer.Ordinal);

        object[] rows = (package.Monsters ?? [])
            .OrderBy(monster => monster.Level)
            .ThenBy(monster => monster.Id, StringComparer.Ordinal)
            .Select(monster => (object)new
            {
                monster.Id,
                Name = monster.DisplayName ?? monster.Name,
                InternalName = monster.Name,
                monster.Level,
                monster.Description,
                monster.Rank,
                monster.ArtId,
                monster.Version,
                monster.MaxHp,
                monster.Stats,
                Combat = new
                {
                    monster.AutoAttackInterval,
                    monster.AutoAttackBaseDamage,
                    monster.AutoAttackBaseDamageMin,
                    monster.AutoAttackBaseDamageMax,
                    monster.AutoAttackAttackPowerCoefficient
                },
                monster.AbilityIds,
                Abilities = monster.AbilityIds
                    .Select(id =>
                    {
                        bool found = abilitiesById.TryGetValue(id, out var ability);
                        return new { Id = id, Found = found, Definition = ability };
                    }),
                monster.AiProfileId,
                AiProfile = aiProfilesById.GetValueOrDefault(monster.AiProfileId),
                Summoning = new
                {
                    monster.SummonMonsterId,
                    monster.SummonIntervalSeconds,
                    monster.SummonCount,
                    monster.MaxActiveSummons
                },
                Rewards = new
                {
                    monster.XpReward,
                    monster.GoldRewardMin,
                    monster.GoldRewardMax,
                    monster.LootTableId,
                    LootTable = (package.LootTables ?? []).FirstOrDefault(table =>
                        string.Equals(table.Id, monster.LootTableId, StringComparison.Ordinal))
                },
                AppearsIn = new
                {
                    WorldLocationIds = package.Locations
                        .Where(location => location.Encounters?.Any(encounter =>
                            string.Equals(encounter.MonsterId, monster.Id, StringComparison.Ordinal)) == true)
                        .Select(location => location.Id)
                        .Order(StringComparer.Ordinal),
                    DungeonEncounters = (package.Dungeons ?? [])
                        .SelectMany(dungeon => dungeon.Encounters.Select(encounter => new
                        {
                            DungeonId = dungeon.Id,
                            EncounterId = encounter.Id,
                            IsPrimary = string.Equals(encounter.MonsterId, monster.Id, StringComparison.Ordinal),
                            AddRoles = (encounter.Adds ?? [])
                                .Where(add => string.Equals(add.MonsterId, monster.Id, StringComparison.Ordinal))
                                .Select(add => add.Role)
                                .ToArray()
                        }))
                        .Where(source => source.IsPrimary || source.AddRoles.Length > 0)
                        .OrderBy(source => source.DungeonId, StringComparer.Ordinal)
                        .ThenBy(source => source.EncounterId, StringComparer.Ordinal)
                }
            })
            .ToArray();

        return Envelope(package, "enemies", rows.Length, rows);
    }

    private static object BuildLocations(GameContentPackage package)
    {
        IReadOnlyList<DungeonDefinition> dungeons = package.Dungeons ?? [];
        object[] locations = package.Locations
            .OrderBy(location => location.MinimumLevel)
            .ThenBy(location => location.Id, StringComparer.Ordinal)
            .Select(location => (object)new
            {
                location.Id,
                Name = location.DisplayName,
                Level = location.RecommendedLevel,
                LevelRange = new { Minimum = location.MinimumLevel, Maximum = location.MaximumLevel },
                location.Description,
                location.DangerLevel,
                location.ArtId,
                location.Transitions,
                location.RequiredContractId,
                location.TravelDurationSeconds,
                location.AllowAfk,
                Encounters = location.Encounters,
                DungeonIds = dungeons
                    .Where(dungeon => string.Equals(dungeon.EntryLocationId, location.Id, StringComparison.Ordinal)
                        || string.Equals(dungeon.Id, location.Id, StringComparison.Ordinal))
                    .Select(dungeon => dungeon.Id)
                    .Order(StringComparer.Ordinal)
            })
            .ToArray();

        object[] dungeonRows = dungeons
            .OrderBy(dungeon => dungeon.MinimumLevel)
            .ThenBy(dungeon => dungeon.Id, StringComparer.Ordinal)
            .Select(dungeon => (object)new
            {
                dungeon.Id,
                Name = dungeon.DisplayName,
                Level = dungeon.MinimumLevel == dungeon.MaximumLevel
                    ? dungeon.MinimumLevel
                    : (int?)null,
                LevelRange = new { Minimum = dungeon.MinimumLevel, Maximum = dungeon.MaximumLevel },
                dungeon.Description,
                dungeon.EntryLocationId,
                PartySize = new { Minimum = dungeon.MinimumPartySize, Maximum = dungeon.MaximumPartySize },
                dungeon.Encounters
            })
            .ToArray();

        return new
        {
            SchemaVersion,
            package.ContentVersion,
            package.BalanceVersion,
            package.PublishedAtUtc,
            Totals = new { Locations = locations.Length, Dungeons = dungeonRows.Length },
            Locations = locations,
            Dungeons = dungeonRows
        };
    }

    private static Dictionary<string, object?> Envelope(
        GameContentPackage package,
        string collectionName,
        int total,
        object[] values) =>
        new Dictionary<string, object?>
        {
            ["schemaVersion"] = SchemaVersion,
            ["contentVersion"] = package.ContentVersion,
            ["balanceVersion"] = package.BalanceVersion,
            ["publishedAtUtc"] = package.PublishedAtUtc,
            ["total"] = total,
            [collectionName] = values
        };

    private static bool ContainsItem(LootTableDefinition table, string itemId) =>
        table.Entries.Any(entry => string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
        || (table.SelectionGroups ?? []).Any(group => group.Entries.Any(entry =>
            string.Equals(entry.ItemId, itemId, StringComparison.Ordinal)));

    private static async Task WriteAsync(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
