using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaStatefulTalentEffectsTests
{
    [Fact]
    public void BlastWaveTalentNormalizesToSemanticNonCrowdControlDebuff()
    {
        ResolvedTalentEventHook hook = BlastWaveHook();

        Assert.True(ArenaTalentEventDispatcher.TryNormalize(hook, out ArenaTalentEventRule rule));
        Assert.Equal(ArenaTalentEventType.OnCast, rule.Trigger);
        Assert.Equal(ArenaTalentConditionKind.AbilityId, rule.Condition);
        Assert.Equal(ArenaTalentEffectKind.ApplyDebuff, rule.Effect);
        Assert.Equal(ArenaTalentEventDispatcher.MageBlastWaveAbilityId, rule.ConditionValue);
        Assert.NotNull(rule.StatusEffect);
        Assert.Equal(EffectKind.StatModifier, rule.StatusEffect.Kind);
        Assert.Equal(EffectStat.AttackSpeed, rule.StatusEffect.ModifiedStat);
        Assert.Equal(EffectModifierMode.Multiplicative, rule.StatusEffect.ModifierMode);
        Assert.Equal(0.85m, rule.StatusEffect.Magnitude);
        Assert.Equal(TimeSpan.FromSeconds(4), rule.StatusEffect.Duration);
        Assert.True(rule.StatusEffect.SourceSpecific);
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(hook));
    }

    [Fact]
    public void BlastWaveTalentRequiresTheActualBlastWaveAbility()
    {
        Guid source = Guid.NewGuid();
        Guid target = Guid.NewGuid();
        ResolvedTalentModifiers talents = Talents(BlastWaveHook());
        var wrongAbility = BlastWave() with { Id = "MAGE_FIREBALL" };
        var combatEvent = new ArenaTalentCombatEvent(
            ArenaTalentEventType.OnCast,
            source,
            target,
            DateTimeOffset.UnixEpoch,
            wrongAbility);

        IReadOnlyList<ArenaTalentRuntimeEffect> effects = ArenaTalentEventDispatcher.Dispatch(
            talents,
            combatEvent,
            new SequenceGameRandom());

        Assert.Empty(effects);
    }

    [Fact]
    public void BlastWaveTalentAppliesDebuffThroughArenaRuntimeAndExpires()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition blastWave = BlastWave();
        ArenaFighter mage = Fighter(
            Actor(),
            new Dictionary<string, AbilityDefinition> { [blastWave.Id] = blastWave },
            Talents(BlastWaveHook()));
        ArenaFighter enemy = Fighter(
            Actor(),
            new Dictionary<string, AbilityDefinition>(),
            ResolvedTalentModifiers.Empty);
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            mage,
            enemy,
            new SequenceGameRandom(),
            now);

        ArenaCommandResult result = session.UseAbility(
            mage.AccountId,
            "blast-wave",
            blastWave.Id,
            enemy.Actor.ActorId,
            now);

        Assert.True(result.Succeeded);
        CombatEvent applied = Assert.Single(result.Events, x =>
            x.Type == CombatEventType.EffectApplied
            && x.DefinitionId == "ARENA_TALENT_F-5-1_ATTACK_SPEED");
        Assert.Equal(mage.Actor.ActorId, applied.SourceActorId);
        Assert.Equal(enemy.Actor.ActorId, applied.TargetActorId);

        ActiveEffect active = Assert.Single(session.ActiveEffectsFor(enemy.AccountId), x =>
            x.Definition.Id == "ARENA_TALENT_F-5-1_ATTACK_SPEED");
        Assert.Equal(mage.Actor.ActorId, active.SourceId);
        Assert.Equal(enemy.Actor.ActorId, active.TargetId);
        Assert.Equal(now + TimeSpan.FromSeconds(4), active.ExpiresAtUtc);
        Assert.Equal(0.85m, EffectEngine.CalculateStat(
            enemy.Actor,
            EffectStat.AttackSpeed,
            1m,
            now));

        session.AdvanceTo(now + TimeSpan.FromSeconds(5));

        Assert.DoesNotContain(session.ActiveEffectsFor(enemy.AccountId), x =>
            x.Definition.Id == "ARENA_TALENT_F-5-1_ATTACK_SPEED");
        Assert.Equal(1m, EffectEngine.CalculateStat(
            enemy.Actor,
            EffectStat.AttackSpeed,
            1m,
            now + TimeSpan.FromSeconds(5)));
        Assert.Contains(session.GetEventsAfter(result.Snapshot.Sequence), x =>
            x.Type == CombatEventType.EffectExpired
            && x.DefinitionId == "ARENA_TALENT_F-5-1_ATTACK_SPEED");
    }

    [Fact]
    public void LookalikeBlastWaveHookFromUnknownTalentRemainsRejected()
    {
        ResolvedTalentEventHook unknown = BlastWaveHook() with { TalentId = "TEST-UNKNOWN" };

        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(unknown));
        Assert.False(ArenaTalentEventDispatcher.TryNormalize(unknown, out _));
    }

    private static AbilityDefinition BlastWave() => new(
        ArenaTalentEventDispatcher.MageBlastWaveAbilityId,
        AbilityType.Instant,
        AbilityTargetType.AllEnemiesInCombat,
        30,
        TimeSpan.FromSeconds(12),
        TimeSpan.Zero,
        true,
        GlobalCooldownCategory.Standard,
        true,
        "FIRE",
        Actions:
        [
            new AbilityActionDefinition(
                AbilityActionType.Damage,
                42,
                DamageType.Magical,
                CanMiss: false,
                CanCrit: false,
                CanDodge: false,
                SpellPowerCoefficient: 0.80m)
        ]);

    private static ResolvedTalentEventHook BlastWaveHook() => new(
        ArenaTalentEventDispatcher.PyromancerBlastWaveTalentId,
        TalentModifierKeys.OnAbilityUsed,
        1,
        15,
        null,
        TimeSpan.Zero,
        false,
        Duration: TimeSpan.FromSeconds(4));

    private static ArenaFighter Fighter(
        CombatActorState actor,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        ResolvedTalentModifiers talents) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        actor,
        abilities,
        new AutoAttackProfile(TimeSpan.FromSeconds(30), 1, 0, 0),
        talents);

    private static CombatActorState Actor() => new(
        Guid.NewGuid(),
        500,
        500,
        100,
        100,
        new CombatStats(20, 20, 0, 0, 1, 0, 0, 0, 0));

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks,
        []);
}
