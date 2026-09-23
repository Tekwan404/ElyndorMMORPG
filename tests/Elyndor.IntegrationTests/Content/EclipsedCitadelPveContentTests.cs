using Elyndor.Core.Content;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class EclipsedCitadelPveContentTests
{
    [Fact]
    public async Task EclipsedCitadelUsesAuthoredAbilitiesAndGenericEncounters()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        GameContentIndexes indexes = GameContentIndexes.For(package);

        Assert.Equal(
            "ECLIPSED_CITADEL_SENTINEL_AI",
            indexes.MonstersById["ECLIPSED_CITADEL_SENTINEL_L25"].AiProfileId);
        Assert.Equal(
            ["SENTINEL_ASHEN_THRUST", "SENTINEL_OATH_OF_OUTER_SEAL"],
            indexes.MonstersById["ECLIPSED_CITADEL_SENTINEL_L25"].AbilityIds);
        Assert.Equal(
            "ECLIPSED_CITADEL_GOLEM_AI",
            indexes.MonstersById["ECLIPSED_CITADEL_GOLEM_L25"].AiProfileId);
        Assert.Equal(
            ["VOID_WEAVER_VOID_THREAD", "VOID_WEAVER_VEIL_RUPTURE"],
            indexes.MonstersById["ECLIPSED_CITADEL_WEAVER_L25"].AbilityIds);
        Assert.DoesNotContain(
            "BITE",
            indexes.MonstersById["ECLIPSED_CITADEL_WEAVER_L25"].AbilityIds);
        Assert.Equal(
            ["EXECUTIONER_BLACK_CONSTELLATION_MARK", "EXECUTIONER_BLACK_VERDICT"],
            indexes.MonstersById["ECLIPSED_CITADEL_EXECUTIONER_L25"].AbilityIds);
        Assert.DoesNotContain(
            "BITE",
            indexes.MonstersById["ECLIPSED_CITADEL_EXECUTIONER_L25"].AbilityIds);
        Assert.Equal(
            [
                "ARCHON_DEAD_STAR_BRAND",
                "ARCHON_ECLIPSE_ASH",
                "ARCHON_STAR_FRACTURE"
            ],
            indexes.MonstersById["ECLIPSED_CITADEL_ARCHON_L25"].AbilityIds);
        Assert.Contains("ECLIPSED_CITADEL_GOLEM_ENCOUNTER", indexes.EncountersById.Keys);
        Assert.Contains("ECLIPSED_CITADEL_ARCHON_ENCOUNTER", indexes.EncountersById.Keys);
        Assert.Equal(2500, indexes.MonstersById["ARCHON_DEAD_STAR_CORE"].MaxHp);
    }

    [Fact]
    public async Task EclipsedCitadelMechanicsKeepTheirAuthoredGatesAndRewards()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        GameContentIndexes indexes = GameContentIndexes.For(package);

        Assert.Equal(160, indexes.MonstersById["ECLIPSED_CITADEL_WEAVER_L25"].Stats.SpellPower);
        Assert.Equal(215, indexes.MonstersById["ECLIPSED_CITADEL_ARCHON_L25"].Stats.SpellPower);
        Assert.Equal(0, indexes.MonstersById["ARCHON_DEAD_STAR_CORE"].XpReward);
        Assert.Equal(0, indexes.MonstersById["ARCHON_DEAD_STAR_CORE"].GoldRewardMax);
        Assert.Null(indexes.MonstersById["ARCHON_DEAD_STAR_CORE"].LootTableId);

        Assert.Equal(TimeSpan.FromSeconds(1.8), indexes.AbilitiesById[
            "SENTINEL_OATH_OF_OUTER_SEAL"].CastTime);
        Assert.True(indexes.AbilitiesById["SENTINEL_OATH_OF_OUTER_SEAL"].Interruptible);
        Assert.Equal(TimeSpan.FromSeconds(2), indexes.AbilitiesById[
            "EXECUTIONER_BLACK_VERDICT"].CastTime);
        Assert.True(indexes.AbilitiesById["EXECUTIONER_BLACK_VERDICT"].Interruptible);
        Assert.True(indexes.AbilitiesById["ARCHON_DEAD_STAR_COLLAPSE"].Interruptible);
        Assert.Equal(TimeSpan.FromSeconds(3), indexes.AbilitiesById[
            "ARCHON_DEAD_STAR_COLLAPSE"].CastTime);

        Assert.Contains(
            indexes.MonsterAiProfilesById["ECLIPSED_CITADEL_EXECUTIONER_AI"].AbilityRules!,
            rule => rule.AbilityId == "EXECUTIONER_BLACK_VERDICT"
                    && rule.MaxHpPercent == 35);
        Assert.Contains(
            indexes.MonsterAiProfilesById["ECLIPSED_CITADEL_VOID_WEAVER_AI"].AbilityRules!,
            rule => rule.AbilityId == "VOID_WEAVER_VEIL_RUPTURE"
                    && rule.InitialDelay == TimeSpan.FromSeconds(7));

        EncounterDefinition golem = indexes.EncountersById[
            "ECLIPSED_CITADEL_GOLEM_ENCOUNTER"];
        EncounterPhaseDefinition carapace = Assert.Single(golem.Phases);
        Assert.Equal(EncounterTriggerType.HpAtOrBelow, carapace.Trigger.Type);
        Assert.Equal(50, carapace.Trigger.Threshold);
        Assert.True(carapace.Trigger.Once);
        EncounterActionDefinition shield = Assert.Single(carapace.Actions);
        Assert.Equal(EncounterActionType.Shield, shield.Type);
        Assert.Equal(896, shield.Magnitude);
        Assert.Equal(TimeSpan.FromSeconds(10), shield.Duration);

        EncounterDefinition archon = indexes.EncountersById[
            "ECLIPSED_CITADEL_ARCHON_ENCOUNTER"];
        EncounterPhaseDefinition corePhase = Assert.Single(
            archon.Phases,
            phase => phase.Id == "DEAD_STAR_CORE");
        Assert.Equal(70, corePhase.Trigger.Threshold);
        SummonDefinition core = Assert.Single(corePhase.Actions).Summon!;
        Assert.Equal("ARCHON_DEAD_STAR_CORE", core.MonsterId);
        Assert.True(core.IsCombatObject);
        Assert.True(core.NoReward);
        Assert.True(core.LinkToCaster);
        Assert.Equal("ARCHON_DEAD_STAR_VEIL", core.AuraEffectId);

        EncounterPhaseDefinition brokenVeil = Assert.Single(
            archon.Phases,
            phase => phase.Id == "BROKEN_VEIL");
        Assert.Equal(EncounterTriggerType.AddDeath, brokenVeil.Trigger.Type);
        Assert.Equal("ARCHON_DEAD_STAR_CORE", brokenVeil.Trigger.DefinitionId);
        Assert.Contains(brokenVeil.Actions, action =>
            action.Type == EncounterActionType.ApplyEffect
            && action.EffectId == "ARCHON_BROKEN_VEIL");

        EncounterPhaseDefinition finalEclipse = Assert.Single(
            archon.Phases,
            phase => phase.Id == "FINAL_ECLIPSE");
        Assert.Equal(35, finalEclipse.Trigger.Threshold);
        Assert.Contains(finalEclipse.Actions, action =>
            action.Type == EncounterActionType.ChangeAbilitySet
            && action.AbilityIds!.Contains("ARCHON_DEAD_STAR_COLLAPSE"));

        EncounterPhaseDefinition unstableCore = Assert.Single(
            archon.Phases,
            phase => phase.Id == "UNSTABLE_CORE");
        Assert.Equal(EncounterTriggerType.CastInterrupted, unstableCore.Trigger.Type);
        Assert.Equal("ARCHON_DEAD_STAR_COLLAPSE", unstableCore.Trigger.DefinitionId);
        Assert.False(unstableCore.Trigger.Once);
        Assert.Equal("ARCHON_UNSTABLE_CORE", Assert.Single(unstableCore.Actions).EffectId);
    }
}
