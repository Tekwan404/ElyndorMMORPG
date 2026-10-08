using System.Text.Json;
using Elyndor.ContentValidator;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;

namespace Elyndor.UnitTests.Items;

public sealed class AffixV2CatalogTests
{
    [Fact]
    public async Task AuthoredItemsHaveOneCanonicalDefinitionPerId()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json")))
            root = root.Parent;
        Assert.NotNull(root);
        Dictionary<string, string> sources = new(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root.FullName, "content", "items"), "*.json"))
        {
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            foreach (JsonElement item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                string id = item.GetProperty("id").GetString()!;
                Assert.True(sources.TryAdd(id, path), $"Item {id} is defined in both {sources[id]} and {path}.");
            }
        }
        Assert.Equal((await LoadAsync()).Items!.Count, sources.Count);
    }

    [Fact]
    public async Task EveryEffectiveTemplateHasRealRangesAndLegalDropAndReforgeCombinations()
    {
        GameContentPackage content = await LoadAsync();
        ItemizationDefinition itemization = content.Itemization!;
        IReadOnlyList<ItemAffixAuditEntry> audit = ItemAffixAudit.Create(content);
        Assert.Equal(content.Items!.Count, audit.Count);
        Assert.All(audit, row => Assert.Empty(row.Issues));
        foreach (ItemDefinition item in content.Items.Where(ProceduralItemPolicy.IsEnabled))
        {
            Assert.Equal(2, item.GenerationVersion);
            ItemAffixPoolDefinition pool = itemization.AffixPools.Single(candidate => candidate.Id == item.RandomAffixPoolId);
            ItemizationDefinition normalized = ItemizationBudgetPolicy.NormalizeForTemplate(item, itemization);
            for (int seed = 0; seed < 8; seed++)
            {
                GeneratedItemInstance generated = ItemInstanceGenerator.Generate(item, normalized, "NORMAL", new SeededGameRandom(seed));
                Assert.Equal(generated.Affixes.Count, generated.Affixes.Select(affix => affix.StatId).Distinct().Count());
                foreach (GeneratedItemAffix affix in generated.Affixes)
                {
                    ItemAffixRuleDefinition rule = itemization.AffixRules!.Single(rule => rule.StatId == affix.StatId);
                    Assert.True(ItemAffixEligibilityPolicy.IsAllowed(item, rule), $"{item.Id}:{affix.StatId}");
                    Assert.True(ItemAffixEligibilityPolicy.IsCompatible(pool, affix.StatId,
                        generated.Affixes.Where(other => other.SlotKey != affix.SlotKey).Select(other => other.StatId)));
                    if (affix.IsGuaranteed) continue;
                    var candidates = ItemInstanceGenerator.GetReforgeAffixCandidates(item, normalized, generated.Affixes, affix.SlotKey);
                    Assert.NotEmpty(candidates);
                    Assert.All(candidates, candidate => Assert.True(ItemAffixEligibilityPolicy.IsAllowed(item,
                        itemization.AffixRules!.Single(rule => rule.StatId == candidate.StatId))));
                }
            }
        }
    }

    [Fact]
    public async Task HolyPoolsAndCasterPoolsExcludeIrrelevantOffensiveRolls()
    {
        GameContentPackage content = await LoadAsync();
        Assert.All(content.Items!.Where(item => item.Id.Contains("PALADIN_HOLY", StringComparison.Ordinal)),
            item => Assert.Equal("PALADIN_HOLY", item.RandomAffixPoolId));
        Assert.All(content.Itemization!.AffixPools.Where(pool => pool.Id.StartsWith("MAGE_", StringComparison.Ordinal)),
            pool => Assert.DoesNotContain(ItemStatIds.AttackSpeed, pool.StatIds));
        ItemAffixPoolDefinition holy = content.Itemization.AffixPools.Single(pool => pool.Id == "PALADIN_HOLY");
        Assert.DoesNotContain(ItemStatIds.AttackPower, holy.StatIds);
        Assert.DoesNotContain(ItemStatIds.Accuracy, holy.StatIds);
        Assert.DoesNotContain(ItemStatIds.ArmorPenetration, holy.StatIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadingLegacyItemPreservesBirthMetadataAndValues(bool collapsed)
    {
        GameContentPackage content = await LoadAsync();
        ItemDefinition definition = content.Items!.First(item => item.OffHandCategory == "SHIELD" && ProceduralItemPolicy.IsEnabled(item));
        GeneratedItemInstance born = ItemInstanceGenerator.Generate(definition, ItemizationBudgetPolicy.NormalizeForTemplate(definition, content.Itemization!),
            "NORMAL", new SeededGameRandom(42)) with { GenerationVersion = 1 };
        if (collapsed) born = born with { Affixes = born.Affixes.Select(affix => affix with { MinAtGeneration = 1, MaxAtGeneration = 1 }).ToArray() };
        CharacterItem item = new(Guid.NewGuid(), Guid.NewGuid(), definition.Id, 1, DateTimeOffset.UnixEpoch, definition.Version);
        item.ApplyGeneratedInstance(born, "legacy", "DROP", Guid.NewGuid(), "legacy");
        GeneratedItemInstance read = ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition, content.Itemization)!;
        Assert.Equal(1, read.GenerationVersion);
        Assert.Equal(born.Stars, read.Stars);
        Assert.Equal(born.RollQuality, read.RollQuality);
        Assert.Equal(born.IsPerfect, read.IsPerfect);
        Assert.Equal(born.DisplayName, read.DisplayName);
        Assert.Equal(born.ActualItemPower, read.ActualItemPower);
        Assert.Equal(born.MaxTemplateItemPower, read.MaxTemplateItemPower);
        Assert.Equal(born.Affixes.Select(affix => affix.Value), read.Affixes.Select(affix => affix.Value));
    }

    [Fact]
    public async Task LegacyHolyReforgeUsesCurrentPoolWithoutReclassifyingBirthQuality()
    {
        GameContentPackage content = await LoadAsync();
        ItemDefinition definition = content.Items!.First(item => item.Id.Contains("PALADIN_HOLY", StringComparison.Ordinal));
        GeneratedItemInstance born = ItemInstanceGenerator.Generate(definition, ItemizationBudgetPolicy.NormalizeForTemplate(definition, content.Itemization!),
            "NORMAL", new SeededGameRandom(8)) with { GenerationVersion = 1 };
        GeneratedItemAffix selected = born.Affixes.First(affix => !affix.IsGuaranteed);
        GeneratedItemAffix legacyPhysical = selected with { StatId = ItemStatIds.AttackPower, AffixDefinitionId = ItemStatIds.AttackPower };
        born = born with { Affixes = born.Affixes.Select(affix => affix.SlotKey == selected.SlotKey ? legacyPhysical : affix).ToArray() };
        var candidates = ItemInstanceGenerator.GetReforgeAffixCandidates(definition, content.Itemization!, born.Affixes, selected.SlotKey);
        Assert.DoesNotContain(candidates, candidate => candidate.StatId is ItemStatIds.AttackPower or ItemStatIds.ArmorPenetration or ItemStatIds.Accuracy);
        GeneratedItemAffix replacement = ItemInstanceGenerator.RollReforgeAffix(definition, content.Itemization!, born.Affixes, selected.SlotKey, "NORMAL", new SeededGameRandom(9));
        replacement = ItemReforgeQualityPolicy.Constrain(legacyPhysical, replacement, new SequenceGameRandom(0.5m));
        CharacterItem item = new(Guid.NewGuid(), Guid.NewGuid(), definition.Id, 1, DateTimeOffset.UnixEpoch, definition.Version);
        item.ApplyGeneratedInstance(born, "legacy-holy", "DROP", Guid.NewGuid(), "legacy");
        item.SelectReforgeSlot(selected.SlotKey);
        item.ApplyReforge(replacement);
        Assert.Equal(1, item.GenerationVersion);
        Assert.Equal(born.Stars, item.Stars);
        Assert.Equal(born.RollQuality, item.RollQuality);
        Assert.Equal(born.IsPerfect, item.IsPerfect);
        Assert.Equal(born.DisplayName, item.GeneratedDisplayName);
        Assert.Equal(born.Affixes.Where(affix => affix.SlotKey != selected.SlotKey).Select(affix => affix.Value),
            item.Affixes.Where(affix => affix.SlotKey != selected.SlotKey).Select(affix => affix.Value));
    }

    [Fact]
    public async Task DuplicateEligibilityRulesReturnValidationErrorsRatherThanThrowing()
    {
        GameContentPackage content = await LoadAsync();
        ItemizationDefinition malformed = content.Itemization! with
        {
            AffixRules = [.. content.Itemization!.AffixRules!, content.Itemization.AffixRules![0]]
        };
        Assert.Contains(GameContentPackageValidator.Validate(content with { Itemization = malformed }),
            error => error.Code == "INVALID_AFFIX_ELIGIBILITY_RULE");
    }

    internal static Task<GameContentPackage> LoadAsync()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(path)) return GameContentPackageLoader.LoadAsync(path);
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository content not found.");
    }
    [Fact]
    public async Task VampirismAffixesAreEligibleOnClassGearAndUniversalOnlyOnJewelry()
    {
        GameContentPackage content = await LoadAsync();
        ItemizationDefinition itemization = content.Itemization!;
        foreach (string statId in new[]
                 {
                     ItemStatIds.PhysicalVampirism,
                     ItemStatIds.MagicalVampirism,
                     ItemStatIds.UniversalVampirism
                 })
        {
            Assert.Contains(statId, ItemStatIds.ApprovedV1);
            Assert.True(ItemStatIds.IsPercentage(statId));
            Assert.True(itemization.StatPowerWeights[statId] > 0);
        }

        Assert.Contains(ItemStatIds.PhysicalVampirism,
            itemization.AffixPools.Single(pool => pool.Id == "WARRIOR_WEAPON").StatIds);
        Assert.Contains(ItemStatIds.MagicalVampirism,
            itemization.AffixPools.Single(pool => pool.Id == "MAGE_WEAPON").StatIds);
        Assert.Contains(ItemStatIds.UniversalVampirism,
            itemization.AffixPools.Single(pool => pool.Id == "ACCESSORY_GENERAL").StatIds);

        ItemAffixRuleDefinition universal = itemization.AffixRules!.Single(
            rule => rule.StatId == ItemStatIds.UniversalVampirism);
        ItemDefinition sample = content.Items!.First(item => item.Slot == EquipmentSlot.MainHand);
        Assert.False(ItemAffixEligibilityPolicy.IsAllowed(sample, universal));
        Assert.True(ItemAffixEligibilityPolicy.IsAllowed(sample with { Slot = EquipmentSlot.Ring1 }, universal));
    }

    [Fact]
    public void GeneratedVampirismAffixesBecomeUsableEquipmentStats()
    {
        ItemDefinition template = new(
            "TEST_VAMP_WAND", "Vamp Wand", ItemType.Equipment, ItemRarity.Rare,
            60, false, 1, EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 0, 0), "");
        GeneratedItemAffix[] affixes =
        [
            new("AFFIX_1", ItemStatIds.PhysicalVampirism, ItemStatIds.PhysicalVampirism,
                3.5m, 1m, 5m, 0.1m, 3, false, true, 0),
            new("AFFIX_2", ItemStatIds.MagicalVampirism, ItemStatIds.MagicalVampirism,
                2m, 1m, 5m, 0.1m, 3, false, true, 1),
            new("AFFIX_3", ItemStatIds.UniversalVampirism, ItemStatIds.UniversalVampirism,
                0.7m, 0.2m, 2m, 0.1m, 3, false, true, 2)
        ];

        ItemDefinition item = ItemInstanceGenerator.ApplyGeneratedAffixes(template, affixes);

        Assert.Equal(3.5m, item.PhysicalVampirismPercent);
        Assert.Equal(2m, item.MagicalVampirismPercent);
        Assert.Equal(0.7m, item.UniversalVampirismPercent);
    }

}
