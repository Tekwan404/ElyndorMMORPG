using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaTalentTriggerCapabilityTests
{
    [Fact]
    public void HawkSpiritDescriptorIsSupportedAfterAutoAttackTriggerIsDispatched()
    {
        ResolvedTalentEventHook hook = HawkSpiritHook();

        Assert.True(ArenaTalentEventDispatcher.TryNormalize(hook, out ArenaTalentEventRule rule));
        Assert.Equal(ArenaTalentEventType.OnAutoAttack, rule.Trigger);
        Assert.Equal(ArenaTalentConditionKind.None, rule.Condition);
        Assert.Equal(ArenaTalentEffectKind.ApplyBuff, rule.Effect);
        Assert.Equal(ArenaTalentActorRole.Source, rule.EffectSource);
        Assert.Equal(ArenaTalentActorRole.Source, rule.EffectTarget);
        Assert.NotNull(rule.StatusEffect);
        Assert.Equal(EffectKind.StatModifier, rule.StatusEffect.Kind);
        Assert.Equal(EffectStat.AttackSpeed, rule.StatusEffect.ModifiedStat);
        Assert.Equal(1.10m, rule.StatusEffect.Magnitude);
        Assert.Equal(TimeSpan.FromSeconds(6), rule.StatusEffect.Duration);

        Assert.True(ArenaTalentEventDispatcher.Supports(hook));
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(hook));
    }

    [Fact]
    public void LookalikeAutoAttackHookWithUnsupportedSemanticsRemainsRejected()
    {
        ResolvedTalentEventHook hook = HawkSpiritHook() with { TalentId = "TEST-AUTO-ATTACK" };

        Assert.False(ArenaTalentEventDispatcher.TryNormalize(hook, out _));
        Assert.False(ArenaTalentEventDispatcher.Supports(hook));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(hook));
    }

    [Fact]
    public void ExecutableBlastWaveCastTriggerRemainsSupported()
    {
        ResolvedTalentEventHook hook = new(
            ArenaTalentEventDispatcher.PyromancerBlastWaveTalentId,
            TalentModifierKeys.OnAbilityUsed,
            1,
            15,
            null,
            TimeSpan.Zero,
            false,
            Duration: TimeSpan.FromSeconds(4));

        Assert.True(ArenaTalentEventDispatcher.TryNormalize(hook, out ArenaTalentEventRule rule));
        Assert.Equal(ArenaTalentEventType.OnCast, rule.Trigger);
        Assert.Equal(ArenaTalentEffectKind.ApplyDebuff, rule.Effect);
        Assert.True(ArenaTalentEventDispatcher.Supports(hook));
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(hook));
    }

    private static ResolvedTalentEventHook HawkSpiritHook() => new(
        ArenaTalentEventDispatcher.ArcherHawkSpiritTalentId,
        TalentModifierKeys.OnAutoAttack,
        1,
        10,
        ArenaTalentEventDispatcher.HawkSpiritTargetId,
        TimeSpan.Zero,
        false,
        ChancePercent: 5,
        Duration: TimeSpan.FromSeconds(6));
}
