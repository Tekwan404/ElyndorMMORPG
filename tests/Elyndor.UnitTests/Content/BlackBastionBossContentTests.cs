using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class BlackBastionBossContentTests
{
    private static readonly string[] BossIds =
    [
        "BLACK_BASTION_BOSS_KOMENDANT_VRAT_RAGNOR_L40",
        "BLACK_BASTION_BOSS_INKVIZITOR_SEIRA_L40",
        "BLACK_BASTION_BOSS_RUNNYI_KOLOSS_ARK_TOR_L40",
        "BLACK_BASTION_BOSS_MARSHAL_PEPELNOGO_ZNAMENI_L40",
        "BLACK_BASTION_BOSS_MAGISTR_CHIORNOI_ZVEZDY_L40",
        "BLACK_BASTION_BOSS_PERVYI_STRAZH_MOR_KAR_L40"
    ];

    [Fact]
    public async Task SixBossesUseDedicatedAbilitiesAiAndDungeonEncounters()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        var dungeon = package.Dungeons!.Single(item => item.Id == "BLACK_BASTION");

        Assert.Equal(1, dungeon.MinimumPartySize);
        Assert.Equal(5, dungeon.MaximumPartySize);

        foreach (string bossId in BossIds)
        {
            MonsterDefinition boss = indexes.MonstersById[bossId];
            Assert.Equal(MonsterRank.Boss, boss.Rank);
            Assert.NotEmpty(boss.AbilityIds);
            Assert.NotEqual("AUTHORED_EMPTY_AI", boss.AiProfileId);
            Assert.Contains(package.MonsterAiProfiles!, ai => ai.Id == boss.AiProfileId);
            Assert.All(boss.AbilityIds, abilityId => Assert.True(indexes.AbilitiesById.ContainsKey(abilityId)));
            Assert.True(indexes.EncountersByMonsterId.ContainsKey(bossId));
            Assert.Contains(dungeon.Encounters, encounter => encounter.MonsterId == bossId && encounter.IsBoss);
        }
    }

    [Fact]
    public async Task SeiraResurrectsHerExecutionerAtHalfHealth()
    {
        GameContentPackage package = await LoadAsync();
        EncounterDefinition encounter = package.Encounters!.Single(item => item.Id == "BLACK_BASTION_SEIRA_ENCOUNTER");

        EncounterPhaseDefinition opening = encounter.Phases.Single(phase => phase.Id == "INQUISITION_EXECUTIONER");
        Assert.Equal(EncounterTriggerType.CombatStart, opening.Trigger.Type);
        Assert.Contains(opening.Actions, action =>
            action.Summon?.MonsterId == "SEIRA_INQUISITION_EXECUTIONER"
            && action.Summon.AuraEffectId == "SEIRA_EXECUTIONER_GUARD"
            && action.Summon.AuraTargetSelector == EncounterTargetSelectors.Boss);

        EncounterPhaseDefinition confession = encounter.Phases.Single(phase => phase.Id == "FINAL_CONFESSION");
        Assert.Equal(EncounterTriggerType.HpAtOrBelow, confession.Trigger.Type);
        Assert.Equal(50, confession.Trigger.Threshold);
        Assert.Contains(confession.Actions, action => action.EffectId == "SEIRA_FINAL_CONFESSION_SLEEP");
        Assert.Contains(confession.Actions, action =>
            action.Summon?.MonsterId == "SEIRA_INQUISITION_EXECUTIONER"
            && action.Delay == TimeSpan.FromSeconds(2)
            && action.Summon.InitialHpPercent == 60);
    }

    [Fact]
    public async Task RagnorFrenzyIncreasesAttackSpeedAndReplacesCleaveWithFasterVariant()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        EncounterPhaseDefinition frenzy = indexes.EncountersByMonsterId[
            "BLACK_BASTION_BOSS_KOMENDANT_VRAT_RAGNOR_L40"].Phases.Single();
        AbilityDefinition normal = indexes.AbilitiesById["RAGNOR_CLEAVE"];
        AbilityDefinition fast = indexes.AbilitiesById["RAGNOR_FRENZY_CLEAVE"];

        Assert.Contains(frenzy.Actions, action => action.EffectId == "RAGNOR_COMMANDER_FRENZY_HASTE");
        Assert.Contains("RAGNOR_FRENZY_CLEAVE", frenzy.AbilityIds!);
        Assert.DoesNotContain("RAGNOR_CLEAVE", frenzy.AbilityIds!);
        Assert.True(fast.Cooldown < normal.Cooldown);
        Assert.Equal(normal.Actions, fast.Actions);
    }

    [Fact]
    public async Task ArkTorAndMarshalHaveBoundedPriorityAdds()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        MonsterDefinition arkTor = indexes.MonstersById["BLACK_BASTION_BOSS_RUNNYI_KOLOSS_ARK_TOR_L40"];

        EncounterDefinition arkEncounter = indexes.EncountersByMonsterId[arkTor.Id];
        Assert.Contains(arkEncounter.Phases, phase =>
            phase.Trigger.Type == EncounterTriggerType.ElapsedTime
            && phase.Actions.Any(action => action.Summon?.MonsterId == "ARK_TOR_RUNE_SENTINEL"));
        Assert.Contains(arkEncounter.Phases, phase => phase.Trigger.Threshold == 66);
        Assert.Contains(arkEncounter.Phases, phase => phase.Trigger.Threshold == 33);
        Assert.All(
            arkEncounter.Phases.SelectMany(phase => phase.Actions).Where(action => action.Summon is not null),
            action => Assert.InRange(action.Summon!.MaxActive, 1, 3));

        EncounterDefinition marshal = indexes.EncountersByMonsterId[
            "BLACK_BASTION_BOSS_MARSHAL_PEPELNOGO_ZNAMENI_L40"];
        EncounterPhaseDefinition alert = marshal.Phases.Single(phase => phase.Id == "INTRUDER_ALERT");
        Assert.Equal(40, alert.Trigger.Threshold);
        Assert.Equal(3, alert.Actions.Sum(action => action.Summon?.Count ?? 0));
        Assert.Contains(alert.Actions, action => action.Summon?.MonsterId == "ASHEN_FIELD_MEDIC");
    }

    [Fact]
    public async Task BlackStarTargetsManaAndLivingBombExplodesAcrossParty()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        MonsterDefinition magistrate = indexes.MonstersById[
            "BLACK_BASTION_BOSS_MAGISTR_CHIORNOI_ZVEZDY_L40"];
        MonsterAiProfile ai = indexes.MonsterAiProfilesById[magistrate.AiProfileId];
        MonsterAbilityRule manaBurn = ai.AbilityRules!.Single(rule => rule.AbilityId == "BLACK_STAR_MANA_BURN");
        AbilityDefinition livingBomb = indexes.AbilitiesById["BLACK_STAR_LIVING_BOMB"];
        AbilityDefinition manaBurnAbility = indexes.AbilitiesById["BLACK_STAR_MANA_BURN"];
        EffectDefinition bomb = livingBomb.Actions!
            .Single(action => action.Effect?.Id == "BLACK_STAR_LIVING_BOMB_MARK")
            .Effect!;

        Assert.Equal(AbilityTargetSelectorProfile.RandomManaUser, manaBurn.TargetSelector);
        Assert.Contains(manaBurnAbility.Actions!, action =>
            action.Type == AbilityActionType.ResourceChange
            && action.ResourceTarget == AbilityResourceTarget.Target
            && action.Amount < 0);
        Assert.Equal("BLACK_STAR_LIVING_BOMB_MARK", bomb.Id);
        Assert.Equal("Живая Чёрная Звезда", bomb.DisplayName);
        Assert.Null(bomb.DispelCategory);
        Assert.Contains(bomb.OnExpireActions!, action =>
            action.Type == EffectExpirationActionType.Damage
            && action.TargetScope == EffectExpirationTargetScope.EffectTargetAndAllies);

        EncounterDefinition encounter = indexes.EncountersByMonsterId[magistrate.Id];
        Assert.Contains(encounter.Phases, phase =>
            phase.Id == "ARMAGEDDON"
            && phase.Trigger.Threshold == 5
            && phase.AbilityIds!.Contains("BLACK_STAR_ARMAGEDDON"));
    }

    [Fact]
    public async Task MorKarFinalWatchRemovesAvatarAndAddsTremor()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        EncounterDefinition encounter = indexes.EncountersByMonsterId[
            "BLACK_BASTION_BOSS_PERVYI_STRAZH_MOR_KAR_L40"];
        EncounterPhaseDefinition finalWatch = encounter.Phases.Single(phase => phase.Id == "LAST_WATCH");

        Assert.Equal(20, finalWatch.Trigger.Threshold);
        Assert.Contains("MOR_KAR_BASTION_TREMOR", finalWatch.AbilityIds!);
        Assert.DoesNotContain("MOR_KAR_AVATAR", finalWatch.AbilityIds!);
        Assert.Contains(finalWatch.Actions, action => action.EffectId == "MOR_KAR_LAST_WATCH_HASTE");
    }

    [Fact]
    public async Task EncounterAddsNeverGrantSeparateRewards()
    {
        GameContentPackage package = await LoadAsync();
        GameContentIndexes indexes = GameContentIndexes.For(package);
        SummonDefinition[] summons = package.Encounters!
            .Where(encounter => BossIds.Contains(encounter.MonsterId))
            .SelectMany(encounter => encounter.Phases)
            .SelectMany(phase => phase.Actions)
            .Where(action => action.Summon is not null)
            .Select(action => action.Summon!)
            .ToArray();

        Assert.NotEmpty(summons);
        Assert.All(summons, summon =>
        {
            MonsterDefinition add = indexes.MonstersById[summon.MonsterId];
            Assert.True(summon.NoReward);
            Assert.Equal(0, add.XpReward);
            Assert.Equal(0, add.GoldRewardMin);
            Assert.Equal(0, add.GoldRewardMax);
            Assert.Null(add.LootTableId);
        });
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
