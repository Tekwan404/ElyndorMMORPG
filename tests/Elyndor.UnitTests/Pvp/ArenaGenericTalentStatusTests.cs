using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaGenericTalentStatusTests
{
    [Fact]
    public void MomentumRequiresRageCostAndProducesRefreshingSelfAttackSpeed()
    {
        var hook = new ResolvedTalentEventHook("B-2-4", TalentModifierKeys.OnAbilityUsed,
            2, 4, null, TimeSpan.Zero, false, Threshold: 20, Duration: TimeSpan.FromSeconds(3));
        var talents = Talents(hook);
        Guid owner = Guid.NewGuid();
        Guid enemy = Guid.NewGuid();
        var costly = Cast(owner, enemy, 20);

        Assert.True(ArenaGenericTalentStatusRuntime.Supports(hook));
        Assert.Empty(ArenaGenericTalentStatusRuntime.Dispatch(talents, Cast(owner, enemy, 19)));
        var effect = Assert.Single(ArenaGenericTalentStatusRuntime.Dispatch(talents, costly));
        Assert.Equal(owner, effect.TargetActorId);
        Assert.Equal(ArenaTalentEffectKind.ApplyBuff, effect.Kind);
        Assert.Equal(EffectStat.AttackSpeed, effect.Effect!.ModifiedStat);
        Assert.Equal(0.04m, effect.Effect.Magnitude);
        Assert.Equal(EffectStackPolicy.Refresh, effect.Effect.StackPolicy);
        Assert.Equal(TimeSpan.FromSeconds(3), effect.Effect.Duration);
    }

    [Fact]
    public void DevastatingBlowRequiresCriticalAutoAttackAndDebuffsEnemy()
    {
        var hook = new ResolvedTalentEventHook("B-6-2", TalentModifierKeys.OnCriticalHit,
            1, 5, "AUTO_ATTACK", TimeSpan.Zero, false, Duration: TimeSpan.FromSeconds(8));
        var talents = Talents(hook);
        Guid owner = Guid.NewGuid();
        Guid enemy = Guid.NewGuid();
        var hit = new ArenaTalentCombatEvent(ArenaTalentEventType.OnCrit, owner, enemy,
            DateTimeOffset.UnixEpoch,
            new AbilityDefinition("AUTO_ATTACK", AbilityType.Instant,
                AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero, false,
                GlobalCooldownCategory.None, true, "PHYSICAL"), WasCritical: true);

        Assert.True(ArenaGenericTalentStatusRuntime.Supports(hook));
        Assert.Empty(ArenaGenericTalentStatusRuntime.Dispatch(talents,
            hit with { Type = ArenaTalentEventType.OnHit }));
        Assert.Empty(ArenaGenericTalentStatusRuntime.Dispatch(talents,
            hit with { Ability = null }));
        Assert.Empty(ArenaGenericTalentStatusRuntime.Dispatch(talents,
            hit with { Ability = new AbilityDefinition("WILD_STRIKE", AbilityType.Instant,
                AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero, false,
                GlobalCooldownCategory.None, true, "PHYSICAL") }));
        var effect = Assert.Single(ArenaGenericTalentStatusRuntime.Dispatch(talents, hit));
        Assert.Equal(ArenaTalentEffectKind.ApplyDebuff, effect.Kind);
        Assert.Equal(enemy, effect.TargetActorId);
        Assert.Equal(EffectStat.IncomingPhysicalDamageMultiplier, effect.Effect!.ModifiedStat);
        Assert.Equal(1.05m, effect.Effect.Magnitude);
        Assert.True(effect.Effect.SourceSpecific);
    }

    [Fact]
    public void UnknownOrMalformedHooksRemainUnsupported()
    {
        var hook = new ResolvedTalentEventHook("B-2-4", TalentModifierKeys.OnAbilityUsed,
            1, 2, null, TimeSpan.Zero, false, Threshold: 20, Duration: TimeSpan.FromSeconds(3));
        Assert.False(ArenaGenericTalentStatusRuntime.Supports(hook with { TalentId = "UNKNOWN" }));
        Assert.False(ArenaGenericTalentStatusRuntime.Supports(hook with { Duration = TimeSpan.Zero }));
    }

    private static ArenaTalentCombatEvent Cast(Guid owner, Guid enemy, int cost) =>
        new(ArenaTalentEventType.OnCast, owner, enemy, DateTimeOffset.UnixEpoch,
            new AbilityDefinition("WILD_STRIKE", AbilityType.Instant,
                AbilityTargetType.SingleEnemy, cost, TimeSpan.Zero, TimeSpan.Zero, false,
                GlobalCooldownCategory.None, true, "PHYSICAL"));

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(), new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal), hooks, []);
}
