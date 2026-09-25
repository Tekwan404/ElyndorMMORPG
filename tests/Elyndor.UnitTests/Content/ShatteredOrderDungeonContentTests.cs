using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class ShatteredOrderDungeonContentTests
{
    private static readonly string[] BossIds =
    [
        "SHATTERED_ORDER_CITADEL_BOSS_ZERKALNYI_KASTELAN_L30",
        "SHATTERED_ORDER_CITADEL_BOSS_ARKHIMAG_VELLARIUS_L30",
        "SHATTERED_ORDER_CITADEL_BOSS_KHRANITEL_DUSH_MOR_ET_L30",
        "SHATTERED_ORDER_CITADEL_BOSS_TRIEDINYI_MAGISTR_AZRAEL_L30"
    ];

    [Fact]
    public async Task MirrorObservatoryAndShatteredOrderCitadelAreDistinctDungeons()
    {
        GameContentPackage package = await LoadAsync();
        var dungeons = package.Dungeons!;
        var observatory = dungeons.Single(item => item.Id == "SHATTERED_ORDER_CITADEL_TEST");
        var citadel = dungeons.Single(item => item.Id == "SHATTERED_ORDER_CITADEL");

        Assert.Equal("Обсерватория Расколотого Зеркала", observatory.DisplayName);
        Assert.Equal(25, observatory.MinimumLevel);
        Assert.Contains(observatory.Encounters, item => item.MonsterId == "SHATTERED_ORDER_AZRAEL_L25");

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
            Assert.Equal(0, monster.XpReward);
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
