using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class ShatteredOrderDungeonContentTests
{
    private static readonly string[] ObservatorySetIds =
    [
        "SET_BLACK_BASTION_WARRIOR_BERSERKER",
        "SET_BLACK_BASTION_MAGE_FROST",
        "SET_BLACK_BASTION_ARCHER_SURVIVAL",
        "SET_BLACK_BASTION_PALADIN_HOLY"
    ];

    private static readonly string[] BossIds =
    [
        "SHATTERED_ORDER_CITADEL_BOSS_ZERKALNYI_KASTELAN_L30",
        "SHATTERED_ORDER_CITADEL_BOSS_ARKHIMAG_VELLARIUS_L30",
        "SHATTERED_ORDER_CITADEL_BOSS_KHRANITEL_DUSH_MOR_ET_L30",
        "SHATTERED_ORDER_CITADEL_BOSS_TRIEDINYI_MAGISTR_AZRAEL_L30"
    ];

    [Fact]
    public async Task ObservatoryAndShatteredOrderCitadelAreDistinctProductionDungeons()
    {
        GameContentPackage package = await LoadAsync();
        var dungeons = package.Dungeons!;
        var observatory = dungeons.Single(item => item.Id == "SHATTERED_ORDER_CITADEL_TEST");
        var citadel = dungeons.Single(item => item.Id == "SHATTERED_ORDER_CITADEL");

        Assert.Equal("Обсерватория Погасшего Неба", observatory.DisplayName);
        Assert.Equal(25, observatory.MinimumLevel);
        Assert.Equal(25, observatory.MaximumLevel);
        Assert.Equal(4, observatory.Encounters.Count);
        Assert.Contains(observatory.Encounters, item => item.MonsterId == "SHATTERED_ORDER_AZRAEL_L25");

        LocationDefinition observatoryLocation = package.Locations.Single(item => item.Id == observatory.EntryLocationId);
        Assert.Equal(1, observatoryLocation.MinimumLevel);
        Assert.Equal(25, observatoryLocation.RecommendedLevel);
        Assert.Equal("SAFE", observatoryLocation.DangerLevel);
        Assert.False(ContainsLegacyMirrorLore(observatory.DisplayName));
        Assert.False(ContainsLegacyMirrorLore(observatory.Description));
        Assert.False(ContainsTestCopy(observatory.DisplayName));
        Assert.False(ContainsTestCopy(observatory.Description));
        Assert.False(ContainsLegacyMirrorLore(observatoryLocation.DisplayName));
        Assert.False(ContainsLegacyMirrorLore(observatoryLocation.Description));
        Assert.False(ContainsTestCopy(observatoryLocation.DisplayName));
        Assert.False(ContainsTestCopy(observatoryLocation.Description));

        Assert.Equal("Цитадель Расколотого Ордена", citadel.DisplayName);
        Assert.Equal(30, citadel.MinimumLevel);
        Assert.Equal(1, citadel.MinimumPartySize);
        Assert.Equal(5, citadel.MaximumPartySize);
        Assert.Equal(12, citadel.Encounters.Count);
        Assert.Equal(4, citadel.Encounters.Count(item => item.IsBoss));
        GameContentIndexes indexes = GameContentIndexes.For(package);
        Assert.DoesNotContain(citadel.Encounters, item =>
            indexes.MonstersById[item.MonsterId].DisplayName!.Contains("Зеркал", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ObservatoryHasLevelAppropriateRewardsAndNoPlayerFacingMirrorLore()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        DungeonDefinition observatory = package.Dungeons!.Single(item => item.Id == "SHATTERED_ORDER_CITADEL_TEST");
        DungeonDefinition blackBastion = package.Dungeons!.Single(item => item.Id == "BLACK_BASTION");

        MonsterDefinition[] bosses = observatory.Encounters
            .Where(item => item.IsBoss)
            .Select(item => indexes.MonstersById[item.MonsterId])
            .ToArray();
        Assert.Equal(4, bosses.Length);
        Assert.All(bosses, boss =>
        {
            Assert.Equal(25, boss.Level);
            Assert.True(boss.GrantsXp, boss.Id);
            Assert.True(boss.GoldRewardMin > 0, boss.Id);
            Assert.True(boss.GoldRewardMax >= boss.GoldRewardMin, boss.Id);
            Assert.NotNull(boss.LootTableId);
            Assert.False(ContainsObservatoryMirrorLore(boss.DisplayName), $"{boss.Id}: {boss.DisplayName}");
            Assert.False(ContainsObservatoryMirrorLore(boss.Description), $"{boss.Id}: {boss.Description}");
        });

        HashSet<string> observatoryLoot = LootItemIds(observatory, indexes);
        HashSet<string> blackBastionLoot = LootItemIds(blackBastion, indexes);
        ItemDefinition[] movedSetItems = package.Items!
            .Where(item => item.SetId is not null && ObservatorySetIds.Contains(item.SetId, StringComparer.Ordinal))
            .ToArray();

        Assert.Equal(32, movedSetItems.Length);
        Assert.All(ObservatorySetIds, setId =>
            Assert.Equal(8, movedSetItems.Count(item => item.SetId == setId)));
        Assert.All(movedSetItems, item =>
        {
            Assert.Equal(25, item.RequiredLevel);
            Assert.Equal(25, item.ItemLevelMin);
            Assert.Equal(25, item.ItemLevelMax);
            Assert.Equal("LEVEL_25_28", item.AffixCountProfileId);
            Assert.Contains(item.Id, observatoryLoot);
            Assert.DoesNotContain(item.Id, blackBastionLoot);
            Assert.Contains("Обсерватор", item.Description, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task CitadelRosterUsesDedicatedAbilitiesAndAi()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        var dungeon = package.Dungeons!.Single(item => item.Id == "SHATTERED_ORDER_CITADEL");

        foreach (var encounter in dungeon.Encounters)
        {
            MonsterDefinition monster = indexes.MonstersById[encounter.MonsterId];
            Assert.NotEmpty(monster.AbilityIds);
            Assert.StartsWith("SHATTERED_ORDER_", monster.AiProfileId);
            Assert.All(monster.AbilityIds, id => Assert.True(indexes.AbilitiesById.ContainsKey(id)));
        }

        Assert.Equal(
            ["Кастелян Торвальд Сломанный Щит", "Архивариус Эллария Хладная", "Командор Реван Чёрный Клинок", "Верховный Магистр Каэль-Мор"],
            BossIds.Select(id => indexes.MonstersById[id].DisplayName!).ToArray());
    }

    [Fact]
    public async Task CitadelRewardsUseFallenOrderRatherThanMirrorLore()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        var dungeon = package.Dungeons!.Single(item => item.Id == "SHATTERED_ORDER_CITADEL");
        HashSet<string> rewardItemIds = dungeon.Encounters
            .Select(encounter => indexes.MonstersById[encounter.MonsterId].LootTableId)
            .Where(lootTableId => lootTableId is not null)
            .Select(lootTableId => indexes.LootTablesById[lootTableId!])
            .SelectMany(lootTable => lootTable.Entries.Select(entry => entry.ItemId)
                .Concat((lootTable.SelectionGroups ?? []).SelectMany(group => group.Entries)
                    .Select(entry => entry.ItemId)))
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(rewardItemIds);
        Assert.All(rewardItemIds, itemId =>
        {
            var item = indexes.ItemsById[itemId];
            Assert.False(ContainsLegacyMirrorLore(item.Name), $"{itemId}: {item.Name}");
            Assert.False(ContainsLegacyMirrorLore(item.Description), $"{itemId}: {item.Description}");
        });
        Assert.All(
            package.EquipmentSets!.Where(set => set.Id.StartsWith(
                "SET_SHATTERED_ORDER_RAID_",
                StringComparison.Ordinal)),
            set => Assert.False(ContainsLegacyMirrorLore(set.Name), $"{set.Id}: {set.Name}"));
    }

    [Fact]
    public async Task BossPhasesMatchApprovedThresholdsAndBoundAdds()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);

        EncounterDefinition torvald = indexes.EncountersByMonsterId[BossIds[0]];
        EncounterPhaseDefinition defense = torvald.Phases.Single(item => item.Id == "TORVALD_LAST_DEFENSE");
        Assert.Equal(40, defense.Trigger.Threshold);
        Assert.DoesNotContain("TORVALD_SHIELD_SLAM", defense.AbilityIds!);

        EncounterDefinition ellaria = indexes.EncountersByMonsterId[BossIds[1]];
        Assert.Equal([70m, 35m], ellaria.Phases.Select(item => item.Trigger.Threshold).ToArray());
        Assert.All(ellaria.Phases, phase => Assert.Contains(phase.Actions, action =>
            action.Summon?.MonsterId == "SHATTERED_ORDER_CORRUPTED_FOLIO"
            && action.Summon.MaxActive == 1
            && action.Summon.AuraTargetSelector == EncounterTargetSelectors.Boss));

        EncounterDefinition revan = indexes.EncountersByMonsterId[BossIds[2]];
        Assert.Contains(revan.Phases, phase => phase.Id == "REVAN_MARCH_OF_THE_DEAD" && phase.Trigger.Threshold == 30);
        Assert.All(
            revan.Phases.SelectMany(phase => phase.Actions).Where(action => action.Summon is not null),
            action => Assert.InRange(action.Summon!.MaxActive, 1, 4));
    }

    [Fact]
    public async Task KaelMorHasThreeTrialsAndFinalBurnAbilitySet()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        EncounterDefinition encounter = indexes.EncountersByMonsterId[BossIds[3]];

        Assert.Equal([75m, 50m, 25m], encounter.Phases
            .Where(item => item.Id.StartsWith("KAEL_MOR_TRIAL_", StringComparison.Ordinal))
            .Select(item => item.Trigger.Threshold)
            .ToArray());

        EncounterPhaseDefinition final = encounter.Phases.Single(item => item.Id == "KAEL_MOR_LAST_GRAND_MASTER");
        Assert.Equal(20, final.Trigger.Threshold);
        Assert.Contains("KAEL_MOR_DARK_BOLTS_FINAL", final.AbilityIds!);
        Assert.DoesNotContain("KAEL_MOR_MASTER_CURSE", final.AbilityIds!);
    }

    [Fact]
    public async Task EncounterObjectsAndTrialShadowsNeverGrantRewards()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        string[] noRewardIds =
        [
            "SHATTERED_ORDER_CORRUPTED_FOLIO",
            "SHATTERED_ORDER_FALLEN_KNIGHT_SUMMON",
            "SHATTERED_ORDER_TRIAL_DEFENDER",
            "SHATTERED_ORDER_TRIAL_CASTER"
        ];

        foreach (string id in noRewardIds)
        {
            MonsterDefinition monster = indexes.MonstersById[id];
            Assert.False(monster.GrantsXp);
            Assert.Equal(0, monster.GoldRewardMin);
            Assert.Equal(0, monster.GoldRewardMax);
            Assert.Null(monster.LootTableId);
        }
    }

    [Fact]
    public async Task KeyMechanicsUseSupportedEffectsAndSelectors()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);

        AbilityDefinition mark = indexes.AbilitiesById["TORVALD_TRAITOR_MARK"];
        var markEffect = mark.Actions!.Single(action => action.Effect?.Id == "TORVALD_TRAITOR_MARK_DOT").Effect!;
        Assert.NotEmpty(markEffect.OnExpireActions!);

        Assert.True(indexes.AbilitiesById["ELLARIA_FROSTBOLT"].Interruptible);
        Assert.True(indexes.AbilitiesById["ELLARIA_SHARD_VOLLEY"].Interruptible);
        Assert.True(indexes.AbilitiesById["FALLEN_CHAPLAIN_HEAL"].Interruptible);
        Assert.True(indexes.AbilitiesById["FALLEN_CHAPLAIN_SHIELD"].Interruptible);
    }

    private static Task<GameContentPackage> LoadAsync() =>
        GameContentPackageLoader.LoadAsync(RepositoryContentPath());

    private static bool ContainsLegacyMirrorLore(string? value) =>
        value?.Contains("зеркал", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("отраж", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("между мирами", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("триедин", StringComparison.OrdinalIgnoreCase) == true;

    private static bool ContainsTestCopy(string? value) =>
        value?.Contains("тест", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("отключ", StringComparison.OrdinalIgnoreCase) == true;

    private static bool ContainsObservatoryMirrorLore(string? value) =>
        value?.Contains("зеркал", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("отраж", StringComparison.OrdinalIgnoreCase) == true;

    private static HashSet<string> LootItemIds(
        DungeonDefinition dungeon,
        GameContentIndexes indexes) =>
        dungeon.Encounters
            .Select(encounter => indexes.MonstersById[encounter.MonsterId].LootTableId)
            .Where(lootTableId => lootTableId is not null)
            .Select(lootTableId => indexes.LootTablesById[lootTableId!])
            .SelectMany(table => table.Entries.Select(entry => entry.ItemId)
                .Concat((table.SelectionGroups ?? []).SelectMany(group => group.Entries)
                    .Select(entry => entry.ItemId)))
            .ToHashSet(StringComparer.Ordinal);

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
