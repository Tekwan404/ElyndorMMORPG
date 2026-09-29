using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaTalentStateEffectExecutorTests
{
    [Fact]
    public void ModifyCooldownChangesOnlyTargetAbilityAndClampsExpiredCooldown()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var runtime = new CombatRuntimeState(Actor());
        runtime.Cooldowns["A"] = now + TimeSpan.FromSeconds(10);
        runtime.Cooldowns["B"] = now + TimeSpan.FromSeconds(20);
        var reduce = new ArenaTalentRuntimeEffect(
            ArenaTalentEffectKind.ModifyCooldown,
            "TEST",
            runtime.Actor.ActorId,
            runtime.Actor.ActorId,
            AbilityId: "A",
            CooldownDelta: TimeSpan.FromSeconds(-4));

        Assert.True(ArenaTalentStateEffectExecutor.ExecuteCooldownMutation(runtime, reduce, now));
        Assert.Equal(now + TimeSpan.FromSeconds(6), runtime.Cooldowns["A"]);
        Assert.Equal(now + TimeSpan.FromSeconds(20), runtime.Cooldowns["B"]);

        var finish = reduce with { CooldownDelta = TimeSpan.FromSeconds(-10) };
        Assert.True(ArenaTalentStateEffectExecutor.ExecuteCooldownMutation(runtime, finish, now));
        Assert.False(runtime.Cooldowns.ContainsKey("A"));
        Assert.Equal(now + TimeSpan.FromSeconds(20), runtime.Cooldowns["B"]);
    }

    [Fact]
    public void ResetCooldownUsesCanonicalRuntimeStateApi()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var runtime = new CombatRuntimeState(Actor());
        runtime.Cooldowns["A"] = now + TimeSpan.FromSeconds(10);
        var reset = new ArenaTalentRuntimeEffect(
            ArenaTalentEffectKind.ModifyCooldown,
            "TEST",
            runtime.Actor.ActorId,
            runtime.Actor.ActorId,
            AbilityId: "A",
            ResetCooldown: true);

        Assert.True(ArenaTalentStateEffectExecutor.ExecuteCooldownMutation(runtime, reset, now));
        Assert.False(runtime.Cooldowns.ContainsKey("A"));
        Assert.False(ArenaTalentStateEffectExecutor.ExecuteCooldownMutation(runtime, reset, now));
    }

    [Fact]
    public void ApplyBuffUsesSharedEffectLifecycleAndRefreshSemantics()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState actor = Actor();
        var definition = new EffectDefinition(
            "TEST_BUFF",
            EffectKind.Buff,
            TimeSpan.FromSeconds(5),
            1,
            EffectStackPolicy.Refresh,
            0);
        var effect = new ArenaTalentRuntimeEffect(
            ArenaTalentEffectKind.ApplyBuff,
            "TEST",
            actor.ActorId,
            actor.ActorId,
            Effect: definition);

        IReadOnlyList<CombatEvent> applied =
            ArenaTalentStateEffectExecutor.ExecuteStatusEffect(actor, effect, now);
        Assert.Contains(applied, x => x.Type == CombatEventType.EffectApplied);
        Assert.Single(actor.ActiveEffects);

        IReadOnlyList<CombatEvent> refreshed = ArenaTalentStateEffectExecutor.ExecuteStatusEffect(
            actor,
            effect,
            now + TimeSpan.FromSeconds(2));
        Assert.Contains(refreshed, x => x.Type == CombatEventType.EffectRefreshed);
        ActiveEffect active = Assert.Single(actor.ActiveEffects);
        Assert.Equal(now + TimeSpan.FromSeconds(7), active.ExpiresAtUtc);

        IReadOnlyList<CombatEvent> expired =
            EffectEngine.Process(actor, now + TimeSpan.FromSeconds(8));
        Assert.Contains(expired, x => x.Type == CombatEventType.EffectExpired);
        Assert.Empty(actor.ActiveEffects);
    }

    [Fact]
    public void ApplyDebuffUsesSharedEffectLifecycleButRejectsCrowdControlBypass()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState source = Actor();
        CombatActorState target = Actor();
        var debuff = new ArenaTalentRuntimeEffect(
            ArenaTalentEffectKind.ApplyDebuff,
            "TEST",
            source.ActorId,
            target.ActorId,
            Effect: new EffectDefinition(
                "TEST_DEBUFF",
                EffectKind.Debuff,
                TimeSpan.FromSeconds(4),
                1,
                EffectStackPolicy.Refresh,
                0));

        IReadOnlyList<CombatEvent> events =
            ArenaTalentStateEffectExecutor.ExecuteStatusEffect(target, debuff, now);
        Assert.Contains(events, x => x.Type == CombatEventType.EffectApplied);
        Assert.Single(target.ActiveEffects);

        var crowdControl = debuff with
        {
            Effect = new EffectDefinition(
                "TEST_STUN",
                EffectKind.Stun,
                TimeSpan.FromSeconds(2),
                1,
                EffectStackPolicy.Refresh,
                0)
        };
        Assert.Throws<NotSupportedException>(() =>
            ArenaTalentStateEffectExecutor.ExecuteStatusEffect(target, crowdControl, now));
    }

    private static CombatActorState Actor() => new(
        Guid.NewGuid(),
        100,
        100,
        100,
        100,
        new CombatStats(20, 0, 0, 0, 1, 0, 0, 0, 0));
}
