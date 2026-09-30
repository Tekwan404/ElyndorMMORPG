using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaPaladinJudgementTalentTests
{
    [Theory]
    [InlineData("R-6-4", "ON_ABILITY_USED", "PALADIN_R_6_4")]
    [InlineData("R-3-1", "ON_ABILITY_USED", "PALADIN_R_3_1")]
    [InlineData("P-4-3", "ON_DAMAGE_TAKEN", "PALADIN_P_4_3")]
    public void CapabilityAcceptsOnlyMappedJudgementHooks(string talentId, string key, string targetId)
    {
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(talentId, key, targetId)));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(talentId, key, "PALADIN_UNKNOWN")));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(talentId, "ON_AUTO_ATTACK", targetId)));
    }

    [Fact]
    public void SuccessfulJudgementRestoresRankScaledManaAndAppliesTwoEnemyDebuffs()
    {
        Guid paladinId = Guid.NewGuid();
        Guid enemyId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var talents = Talents(
            Hook("R-6-4", TalentModifierKeys.OnAbilityUsed, "PALADIN_R_6_4", rank: 2),
            Hook("R-3-1", TalentModifierKeys.OnAbilityUsed, "PALADIN_R_3_1"),
            Hook("P-4-3", TalentModifierKeys.OnDamageTaken, "PALADIN_P_4_3"));

        IReadOnlyList<ArenaTalentRuntimeEffect> effects = ArenaTalentEventDispatcher.Dispatch(
            talents, Cast(paladinId, enemyId, "JUDGEMENT", now), new SequenceGameRandom(0));

        ArenaTalentRuntimeEffect mana = Assert.Single(effects, x => x.Kind == ArenaTalentEffectKind.GainResource);
        Assert.Equal(8m, mana.Amount);
        Assert.Equal(paladinId, mana.TargetActorId);

        ArenaTalentRuntimeEffect magical = Assert.Single(effects, x => x.Effect?.Id == "PALADIN_CRUSADERS_JUDGEMENT");
        Assert.Equal(enemyId, magical.TargetActorId);
        Assert.Equal(TimeSpan.FromSeconds(12), magical.Effect!.Duration);
        Assert.Equal(EffectStat.IncomingMagicalDamageMultiplier, magical.Effect.ModifiedStat);
        Assert.Equal(1.10m, magical.Effect.Magnitude);

        ArenaTalentRuntimeEffect slow = Assert.Single(effects, x => x.Effect?.Id == "PALADIN_JUDGEMENT_OF_JUSTICE");
        Assert.Equal(enemyId, slow.TargetActorId);
        Assert.Equal(TimeSpan.FromSeconds(8), slow.Effect!.Duration);
        Assert.Equal(EffectStat.AttackSpeed, slow.Effect.ModifiedStat);
        Assert.Equal(0.82m, slow.Effect.Magnitude);

        var enemy = new CombatActorState(enemyId, 100, 100, 100, 100, CombatStats.Default);
        foreach (ArenaTalentRuntimeEffect effect in effects.Where(x => x.Kind == ArenaTalentEffectKind.ApplyDebuff))
        {
            Assert.True(ArenaTalentStateEffectExecutor.SupportsStatusEffect(effect));
            Assert.Contains(ArenaTalentStateEffectExecutor.ExecuteStatusEffect(enemy, effect, now),
                x => x.Type == CombatEventType.EffectApplied);
        }
        Assert.Equal(2, enemy.ActiveEffects.Count);

        Assert.Contains(ArenaTalentStateEffectExecutor.ExecuteStatusEffect(
                enemy, slow, now + TimeSpan.FromSeconds(2)),
            x => x.Type == CombatEventType.EffectRefreshed);
        Assert.Equal(2, enemy.ActiveEffects.Count);
        Assert.Equal(now + TimeSpan.FromSeconds(10),
            enemy.ActiveEffects.Single(x => x.Definition.Id == "PALADIN_JUDGEMENT_OF_JUSTICE").ExpiresAtUtc);
    }

    [Fact]
    public void OtherAbilitiesDoNotTriggerJudgementTalents()
    {
        Guid paladinId = Guid.NewGuid();
        Guid enemyId = Guid.NewGuid();
        var talents = Talents(Hook("R-6-4", TalentModifierKeys.OnAbilityUsed, "PALADIN_R_6_4"));

        Assert.Empty(ArenaTalentEventDispatcher.Dispatch(
            talents, Cast(paladinId, enemyId, "CRUSADER_STRIKE", DateTimeOffset.UnixEpoch),
            new SequenceGameRandom(0)));
    }

    private static ArenaTalentCombatEvent Cast(Guid source, Guid target, string abilityId, DateTimeOffset now) =>
        new(ArenaTalentEventType.OnCast, source, target, now,
            new AbilityDefinition(abilityId, AbilityType.Instant, AbilityTargetType.SingleEnemy,
                0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "HOLY"));

    private static ResolvedTalentEventHook Hook(string id, string key, string target, int rank = 1) =>
        new(id, key, rank, 1, target, TimeSpan.Zero, false);

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(), new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal), hooks, []);
}
