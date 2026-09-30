using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaAutoAttackTalentEventTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountA = Guid.Parse("11000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountB = Guid.Parse("22000000-0000-0000-0000-000000000002");
    private static readonly Guid CharacterA = Guid.Parse("33000000-0000-0000-0000-000000000003");
    private static readonly Guid CharacterB = Guid.Parse("44000000-0000-0000-0000-000000000004");
    private static readonly Guid ActorA = Guid.Parse("55000000-0000-0000-0000-000000000005");
    private static readonly Guid ActorB = Guid.Parse("66000000-0000-0000-0000-000000000006");
    private const string HawkEffectId = "ARENA_TALENT_M-2-4_ATTACK_SPEED";

    [Fact]
    public void BeastMasteryOwnerHawkProcsFromOwnAutoAttackWithoutCompanion()
    {
        CombatActorState source = SourceActor();
        ResolvedTalentEventHook ownerHawk = new("B-1-3", TalentModifierKeys.OnAutoAttack,
            1, 6, "OWNER_HAWK_SPIRIT", TimeSpan.Zero, false,
            ChancePercent: 5, Duration: TimeSpan.FromSeconds(6));
        ArenaCombatSession session = Create(source, TargetActor(), TimeSpan.FromSeconds(4),
            Talents(ownerHawk), new SequenceGameRandom(0.90m, 0.90m, 0.01m));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.Equal(1.06m, EffectEngine.CalculateStat(
            source, EffectStat.AttackSpeed, 1m, Start.AddSeconds(4)));
        Assert.Single(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == "ARENA_TALENT_B-1-3_ATTACK_SPEED");
    }

    [Fact]
    public void SuccessfulAutoAttackProcsHawkSpiritExactlyOnceAndAcceleratesNextSwing()
    {
        TimeSpan interval = TimeSpan.FromSeconds(4);
        CombatActorState source = SourceActor();
        ArenaCombatSession session = Create(
            source,
            TargetActor(),
            interval,
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(
                0.90m, 0.90m, 0.01m,
                0.90m, 0.90m, 0.99m));

        DateTimeOffset firstAttackAt = Start + interval;
        session.AdvanceTo(firstAttackAt);

        ActiveEffect active = Assert.Single(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
        Assert.Equal(ActorA, active.SourceId);
        Assert.Equal(ActorA, active.TargetId);
        Assert.Equal(firstAttackAt + TimeSpan.FromSeconds(6), active.ExpiresAtUtc);
        Assert.Equal(1.10m, EffectEngine.CalculateStat(
            source, EffectStat.AttackSpeed, 1m, firstAttackAt));
        Assert.DoesNotContain(session.ActiveEffectsFor(AccountB), effect =>
            effect.Definition.Id == HawkEffectId);
        Assert.Single(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.EffectApplied
            && combatEvent.DefinitionId == HawkEffectId);

        long acceleratedTicks = Math.Max(
            1,
            (long)Math.Ceiling(interval.Ticks / 1.10d));
        DateTimeOffset acceleratedAttackAt = firstAttackAt + TimeSpan.FromTicks(acceleratedTicks);
        session.AdvanceTo(acceleratedAttackAt);

        CombatEvent[] sourceDamage = session.GetEventsAfter(0)
            .Where(combatEvent => combatEvent.Type == CombatEventType.DamageDealt
                && combatEvent.SourceActorId == ActorA
                && combatEvent.TargetActorId == ActorB)
            .ToArray();
        Assert.Equal(2, sourceDamage.Length);
        Assert.Equal(firstAttackAt, sourceDamage[0].OccurredAtUtc);
        Assert.Equal(acceleratedAttackAt, sourceDamage[1].OccurredAtUtc);
        Assert.True(acceleratedAttackAt < Start + interval + interval);
        Assert.Single(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.EffectApplied
            && combatEvent.DefinitionId == HawkEffectId);
    }

    [Fact]
    public void HawkSpiritProcChanceUsesInjectedArenaRandom()
    {
        ArenaCombatSession session = Create(
            SourceActor(),
            TargetActor(),
            TimeSpan.FromSeconds(4),
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(0.90m, 0.90m, 0.99m));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.DoesNotContain(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
    }

    [Fact]
    public void MissDoesNotDispatchSuccessfulAutoAttackTalentEvent()
    {
        ArenaCombatSession session = Create(
            SourceActor(accuracy: 0),
            TargetActor(),
            TimeSpan.FromSeconds(4),
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(0.01m));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.DoesNotContain(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
    }

    [Fact]
    public void DodgeDoesNotDispatchSuccessfulAutoAttackTalentEvent()
    {
        ArenaCombatSession session = Create(
            SourceActor(),
            TargetActor(dodge: 100),
            TimeSpan.FromSeconds(4),
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(0.50m));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.DoesNotContain(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
    }

    [Fact]
    public void FullBlockWithZeroHpDamageDoesNotDispatchSuccessfulAutoAttackTalentEvent()
    {
        ArenaCombatSession session = Create(
            SourceActor(),
            TargetActor(blockChance: 60, blockValue: 500),
            TimeSpan.FromSeconds(4),
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(0.90m, 0.90m, 0.10m));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.Equal(500, session.Snapshot.ActorB.CurrentHp);
        Assert.DoesNotContain(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.DamageBlocked
            && combatEvent.TargetActorId == ActorB);
    }

    [Fact]
    public void FullyAbsorbedAutoAttackDoesNotDispatchSuccessfulAutoAttackTalentEvent()
    {
        CombatActorState target = TargetActor();
        EffectEngine.Apply(
            target,
            target.ActorId,
            new EffectDefinition(
                "TEST_FULL_ABSORB",
                EffectKind.Shield,
                TimeSpan.FromSeconds(30),
                1,
                EffectStackPolicy.Replace,
                100),
            Start);
        ArenaCombatSession session = Create(
            SourceActor(),
            target,
            TimeSpan.FromSeconds(4),
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(0.90m, 0.90m));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.Equal(500, session.Snapshot.ActorB.CurrentHp);
        Assert.DoesNotContain(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
    }

    [Fact]
    public void CriticalAutoAttackProducesOneDamageHitAndCritPathWithoutDuplicateHawkProc()
    {
        ArenaCombatSession session = Create(
            SourceActor(criticalChance: 100),
            TargetActor(),
            TimeSpan.FromSeconds(4),
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(0.90m, 0.01m, 0.01m));

        session.AdvanceTo(Start.AddSeconds(4));

        IReadOnlyList<CombatEvent> combatEvents = session.GetEventsAfter(0);
        Assert.Single(combatEvents, combatEvent =>
            combatEvent.Type == CombatEventType.EffectApplied
            && combatEvent.DefinitionId == HawkEffectId);
        IReadOnlyList<ArenaTalentCombatEvent> damageTalentEvents =
            ArenaTalentEventDispatcher.FromDamageEvents(combatEvents);
        Assert.Single(damageTalentEvents, talentEvent =>
            talentEvent.Type == ArenaTalentEventType.OnHit
            && talentEvent.SourceActorId == ActorA);
        Assert.Single(damageTalentEvents, talentEvent =>
            talentEvent.Type == ArenaTalentEventType.OnCrit
            && talentEvent.SourceActorId == ActorA);
        Assert.Single(damageTalentEvents, talentEvent =>
            talentEvent.Type == ArenaTalentEventType.OnDamageTaken
            && talentEvent.TargetActorId == ActorB);
    }

    [Fact]
    public void AutoAttackDoesNotTriggerOnCastOnlyTalent()
    {
        ArenaCombatSession session = Create(
            SourceActor(),
            TargetActor(),
            TimeSpan.FromSeconds(4),
            Talents(BlastWaveHook()),
            new SequenceGameRandom(0.90m, 0.90m));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.DoesNotContain(session.ActiveEffectsFor(AccountB), effect =>
            effect.Definition.Id == "ARENA_TALENT_F-5-1_ATTACK_SPEED");
    }

    [Fact]
    public void HawkSpiritExpiresAndAttackSpeedReturnsToBaseline()
    {
        CombatActorState source = SourceActor();
        ArenaCombatSession session = Create(
            source,
            TargetActor(),
            TimeSpan.FromSeconds(30),
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(0.90m, 0.90m, 0.01m));
        DateTimeOffset procAt = Start.AddSeconds(30);

        session.AdvanceTo(procAt);
        Assert.Equal(1.10m, EffectEngine.CalculateStat(
            source, EffectStat.AttackSpeed, 1m, procAt));

        DateTimeOffset afterExpiration = procAt.AddSeconds(6).AddTicks(1);
        session.AdvanceTo(afterExpiration);

        Assert.DoesNotContain(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
        Assert.Equal(1m, EffectEngine.CalculateStat(
            source, EffectStat.AttackSpeed, 1m, afterExpiration));
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.EffectExpired
            && combatEvent.DefinitionId == HawkEffectId);
    }

    [Fact]
    public void HawkSpiritReapplyRefreshesSingleSourceSpecificBuff()
    {
        TimeSpan interval = TimeSpan.FromSeconds(4);
        CombatActorState source = SourceActor();
        ArenaCombatSession session = Create(
            source,
            TargetActor(),
            interval,
            Talents(HawkSpiritHook()),
            new SequenceGameRandom(
                0.90m, 0.90m, 0.01m,
                0.90m, 0.90m, 0.01m));
        DateTimeOffset firstAttackAt = Start + interval;
        long acceleratedTicks = Math.Max(
            1,
            (long)Math.Ceiling(interval.Ticks / 1.10d));
        DateTimeOffset secondAttackAt = firstAttackAt + TimeSpan.FromTicks(acceleratedTicks);

        session.AdvanceTo(secondAttackAt);

        ActiveEffect active = Assert.Single(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
        Assert.Equal(secondAttackAt + TimeSpan.FromSeconds(6), active.ExpiresAtUtc);
        Assert.Equal(1.10m, EffectEngine.CalculateStat(
            source, EffectStat.AttackSpeed, 1m, secondAttackAt));
    }

    private static ArenaCombatSession Create(
        CombatActorState source,
        CombatActorState target,
        TimeSpan sourceInterval,
        ResolvedTalentModifiers sourceTalents,
        IGameRandom random)
    {
        var sourceAutoAttack = new AutoAttackProfile(sourceInterval, 20, 0, 0);
        var targetAutoAttack = new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0);
        return new ArenaCombatSession(
            Guid.NewGuid(),
            new ArenaFighter(
                AccountA,
                CharacterA,
                source,
                new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
                sourceAutoAttack,
                sourceTalents),
            new ArenaFighter(
                AccountB,
                CharacterB,
                target,
                new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
                targetAutoAttack,
                ResolvedTalentModifiers.Empty),
            random,
            Start);
    }

    private static CombatActorState SourceActor(
        decimal accuracy = 100,
        decimal criticalChance = 0) => new(
        ActorA,
        500,
        500,
        100,
        100,
        new CombatStats(
            20,
            accuracy,
            0,
            criticalChance,
            1,
            0,
            0,
            0,
            0));

    private static CombatActorState TargetActor(
        decimal dodge = 0,
        decimal blockChance = 0,
        decimal blockValue = 0) => new(
        ActorB,
        500,
        500,
        100,
        100,
        new CombatStats(
            20,
            0,
            dodge,
            0,
            1,
            0,
            0,
            0,
            0,
            BlockChance: blockChance,
            BlockValueMin: blockValue,
            BlockValueMax: blockValue));

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

    private static ResolvedTalentEventHook BlastWaveHook() => new(
        ArenaTalentEventDispatcher.PyromancerBlastWaveTalentId,
        TalentModifierKeys.OnAbilityUsed,
        1,
        15,
        null,
        TimeSpan.Zero,
        false,
        Duration: TimeSpan.FromSeconds(4));

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks,
        []);
}
