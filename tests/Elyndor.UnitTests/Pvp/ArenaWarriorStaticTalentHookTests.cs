using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaWarriorStaticTalentHookTests
{
    [Theory]
    [InlineData("BATTLE_CRY", 10)]
    [InlineData("ENDURANCE_CRY", 10)]
    [InlineData("CRY_OF_VENGEANCE", 10)]
    [InlineData("RALLY_CRY", 10)]
    public void VoiceOfCommandReducesCryRageCostButNotBelowZero(string abilityId, decimal expectedCost)
    {
        AbilityDefinition resolved = ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(
            Ability(abilityId, resourceCost: 25),
            Talents(Hook("W-1-1", 15)));

        Assert.Equal(expectedCost, resolved.ResourceCost);
        Assert.Equal(0, ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(
            Ability(abilityId, resourceCost: 10), Talents(Hook("W-1-1", 15))).ResourceCost);
    }

    [Fact]
    public void EchoingCommandExtendsTimedCryEffectWithoutChangingCooldownOrOtherAbility()
    {
        AbilityDefinition cry = Ability("BATTLE_CRY", duration: TimeSpan.FromSeconds(6));
        ResolvedTalentModifiers talents = Talents(Hook("W-4-4", 3));

        AbilityDefinition resolved = ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(cry, talents);

        Assert.Equal(TimeSpan.FromSeconds(9), Assert.Single(resolved.Actions!).Effect!.Duration);
        Assert.Equal(TimeSpan.FromSeconds(20), resolved.Cooldown);
        Assert.Equal(TimeSpan.FromSeconds(6), Assert.Single(cry.Actions!).Effect!.Duration);
        Assert.Equal(TimeSpan.FromSeconds(6), Assert.Single(
            ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(
                Ability("BATTLE_FOCUS", duration: TimeSpan.FromSeconds(6)), talents).Actions!).Effect!.Duration);
    }

    [Theory]
    [InlineData("WAR_BANNER")]
    [InlineData("VICTORY_FLAG")]
    [InlineData("BATTLE_STANDARD")]
    public void FearlessnessReducesFlagCooldownWithoutGoingNegative(string abilityId)
    {
        AbilityDefinition resolved = ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(
            Ability(abilityId, cooldown: TimeSpan.FromSeconds(12)),
            Talents(Hook("W-5-2", 20)));

        Assert.Equal(TimeSpan.Zero, resolved.Cooldown);
    }

    [Fact]
    public void OnlyExactWarlordDescriptorsAreAdmitted()
    {
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(Hook("W-1-1", 5)));
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(Hook("W-4-4", 1)));
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(Hook("W-5-2", 7)));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(Hook("W-1-2", 5)));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(
            Hook("W-1-1", 5) with { Key = TalentModifierKeys.OnAbilityUsed }));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(
            Hook("W-1-1", 5) with { TargetId = "OTHER" }));
    }

    [Fact]
    public void ArenaEntrantCapturesResolvedWarriorCryAndRejectsPartyAura()
    {
        AbilityDefinition cry = Ability("BATTLE_CRY", resourceCost: 25,
            duration: TimeSpan.FromSeconds(6));
        var known = new HashSet<string>(StringComparer.Ordinal) { cry.Id };
        CombatPlayerDefinition player = Player(known, Talents(
            Hook("W-1-1", 15), Hook("W-4-4", 3)));

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(player, 30,
            new Dictionary<string, AbilityDefinition> { [cry.Id] = cry },
            hasCompanion: false);

        AbilityDefinition resolved = entrant.Fighter.Abilities[cry.Id];
        Assert.Equal(10m, resolved.ResourceCost);
        Assert.Equal(TimeSpan.FromSeconds(9), Assert.Single(resolved.Actions!).Effect!.Duration);

        CombatPlayerDefinition unsupported = Player(known, Talents(Hook("W-1-2", 5)));
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            unsupported, 30, new Dictionary<string, AbilityDefinition> { [cry.Id] = cry },
            hasCompanion: false));
    }

    private static CombatPlayerDefinition Player(
        IReadOnlySet<string> abilityIds,
        ResolvedTalentModifiers talents)
    {
        var actor = new CombatActorState(Guid.NewGuid(), 100, 100, 100, 100,
            CombatStats.Default);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "WARRIOR", "Test Warrior", "RAGE",
            new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0), abilityIds);
        return new CombatPlayerDefinition(Guid.NewGuid(), participant, talents);
    }

    private static AbilityDefinition Ability(
        string id,
        decimal resourceCost = 0,
        TimeSpan? duration = null,
        TimeSpan? cooldown = null) => new(
        id,
        AbilityType.Instant,
        AbilityTargetType.Self,
        resourceCost,
        cooldown ?? TimeSpan.FromSeconds(20),
        TimeSpan.Zero,
        false,
        GlobalCooldownCategory.None,
        false,
        "PHYSICAL",
        Actions: duration is null ? [] : [new AbilityActionDefinition(
            AbilityActionType.ApplyEffect,
            0,
            Effect: new EffectDefinition(
                "TEST_CRY_EFFECT", EffectKind.Buff, duration.Value, 1,
                EffectStackPolicy.Refresh, 1))]);

    private static ResolvedTalentEventHook Hook(string talentId, decimal value) => new(
        talentId, TalentModifierKeys.OnPartyEvent, 1, value, null,
        TimeSpan.Zero, false);

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(), new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks, []);
}
