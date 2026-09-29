using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Talents;
using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaTalentRuntimeSupportTests
{
    [Fact]
    public void StaticAbilityResolversAreAppliedThroughSingleArenaEntryPoint()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                ArcherStaticAbilityHookResolver.EfficiencyTalentId,
                TalentModifierKeys.OnAbilityUsed,
                "PHYSICAL_FOCUS_COST",
                20));
        var shot = new AbilityDefinition(
            "TEST_SHOT",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            50,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions: []);

        AbilityDefinition resolved = ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(
            shot,
            talents);

        Assert.Equal(40m, resolved.ResourceCost);
    }

    [Fact]
    public void CapabilityRegistryAcceptsKnownRuntimeAndStaticHooks()
    {
        ResolvedTalentEventHook impact = Hook(
            PyromancerImpactRuntime.TalentId,
            TalentModifierKeys.OnAbilityUsed,
            "FIRE_DAMAGE",
            10,
            duration: TimeSpan.FromSeconds(2));
        ResolvedTalentEventHook archerStatic = Hook(
            ArcherStaticAbilityHookResolver.EfficiencyTalentId,
            TalentModifierKeys.OnAbilityUsed,
            "PHYSICAL_FOCUS_COST",
            10);

        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(impact));
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(archerStatic));
    }

    [Fact]
    public void UnknownGameplayHookRemainsUnsupportedAndReportsMechanic()
    {
        ResolvedTalentEventHook unsupported = Hook(
            "TEST-1",
            TalentModifierKeys.OnDamageTaken,
            "BLOCK",
            5);
        ResolvedTalentModifiers talents = Talents(unsupported);

        IReadOnlyList<ResolvedTalentEventHook> result =
            ArenaTalentRuntimeSupport.UnsupportedEventHooks(talents);

        ResolvedTalentEventHook hook = Assert.Single(result);
        Assert.Same(unsupported, hook);
        Assert.Equal($"{TalentModifierKeys.OnDamageTaken}:BLOCK",
            ArenaTalentRuntimeSupport.DescribeUnsupportedHook(hook));
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
        string key,
        string targetId,
        decimal value,
        decimal secondaryValue = 0,
        TimeSpan? duration = null) => new(
            talentId,
            key,
            1,
            value,
            targetId,
            duration ?? TimeSpan.Zero,
            false,
            SecondaryValue: secondaryValue);
}
