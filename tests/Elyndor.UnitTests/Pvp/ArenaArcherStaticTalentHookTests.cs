using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaArcherStaticTalentHookTests
{
    [Fact]
    public void StatelessArcherAbilityHooksAreAcceptedAndApplied()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                ArcherStaticAbilityHookResolver.EfficiencyTalentId,
                "PHYSICAL_FOCUS_COST",
                20),
            Hook(
                ArcherStaticAbilityHookResolver.FlawlessAimTalentId,
                "AIMED_ACCURACY_CRIT",
                5,
                secondaryValue: 3));
        var aimedShot = new AbilityDefinition(
            "AIMED_SHOT",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            50,
            TimeSpan.FromSeconds(6),
            TimeSpan.FromSeconds(2),
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions: [new AbilityActionDefinition(
                AbilityActionType.Damage,
                10,
                DamageType.Physical)]);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            Player(aimedShot.Id, talents),
            20,
            new Dictionary<string, AbilityDefinition> { [aimedShot.Id] = aimedShot },
            hasCompanion: false);

        AbilityDefinition resolved = entrant.Fighter.Abilities[aimedShot.Id];
        Assert.Equal(40m, resolved.ResourceCost);
        Assert.Equal(5m, resolved.AccuracyBonus);
        Assert.Equal(3m, resolved.CriticalChanceBonus);
    }

    [Fact]
    public void ArcherHookWithoutAnArenaAdapterIsPreservedForRuntimeAudit()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("M-1-1", "PHYSICAL_SHOT_CRIT", 5));
        AbilityDefinition shot = PhysicalShot("SHOCKING_SHOT");

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            Player(shot.Id, talents),
            20,
            new Dictionary<string, AbilityDefinition> { [shot.Id] = shot },
            hasCompanion: false);

        Assert.Contains(entrant.Fighter.EffectiveTalentModifiers.EventHooks,
            hook => hook.TalentId == "M-1-1");
    }

    [Fact]
    public void ArcherStatefulHookWithoutItsRequiredDescriptorIsRejectedAtAdmission()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("M-2-4", "HAWK_SPIRIT", 10));
        AbilityDefinition shot = PhysicalShot("SHOCKING_SHOT");

        Assert.Contains(ArenaTalentRuntimeSupport.ForOneVsOne(talents).EventHooks,
            hook => hook.TalentId == "M-2-4");
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            Player(shot.Id, talents),
            20,
            new Dictionary<string, AbilityDefinition> { [shot.Id] = shot },
            hasCompanion: false));
    }

    private static CombatPlayerDefinition Player(
        string abilityId,
        ResolvedTalentModifiers talents)
    {
        Guid character = Guid.NewGuid();
        var actor = new CombatActorState(character, 100, 100, 100, 100, CombatStats.Default);
        var participant = new CombatParticipantDefinition(
            actor,
            CombatActorKind.Player,
            "ARCHER",
            "Test Archer",
            "FOCUS",
            new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0),
            new HashSet<string>(StringComparer.Ordinal) { abilityId },
            GenderId: "FEMALE");
        return new CombatPlayerDefinition(Guid.NewGuid(), participant, talents);
    }

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks,
        []);

    private static ResolvedTalentEventHook Hook(
        string talentId,
        string targetId,
        decimal value,
        decimal secondaryValue = 0) => new(
            talentId,
            TalentModifierKeys.OnAbilityUsed,
            1,
            value,
            targetId,
            TimeSpan.Zero,
            false,
            SecondaryValue: secondaryValue);

    private static AbilityDefinition PhysicalShot(string id) => new(
        id,
        AbilityType.Instant,
        AbilityTargetType.SingleEnemy,
        10,
        TimeSpan.Zero,
        TimeSpan.Zero,
        false,
        GlobalCooldownCategory.None,
        false,
        "PHYSICAL",
        Actions: [new AbilityActionDefinition(
            AbilityActionType.Damage,
            10,
            DamageType.Physical)]);
}
