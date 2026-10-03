using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaTalentEventDispatcherTests
{
    [Fact]
    public void ArenaBlockProcHonorsCooldownAndReplayAcrossEventConversion()
    {
        Guid defender = Guid.NewGuid();
        var guard = new ProcGuard();
        var hook = Hook("G-2-5", TalentModifierKeys.OnDamageTaken, "BLOCK", 3)
            with { InternalCooldown = TimeSpan.FromSeconds(5) };
        var talents = Talents(hook);
        CombatEvent block = new(CombatEventType.DamageBlocked, DateTimeOffset.UnixEpoch,
            defender, Amount: 10, SourceActorId: Guid.NewGuid(), TargetActorId: defender);
        CombatEvent damage = block with { Type = CombatEventType.DamageDealt, Amount = 0 };
        ArenaTalentCombatEvent input = Assert.Single(ArenaTalentEventDispatcher.FromDamageEvents([block, damage]));
        Assert.Single(ArenaTalentEventDispatcher.Dispatch(talents, input, new SequenceGameRandom(0), guard));
        ArenaTalentCombatEvent replay = Assert.Single(ArenaTalentEventDispatcher.FromDamageEvents([block, damage]));
        Assert.Empty(ArenaTalentEventDispatcher.Dispatch(talents, replay, new SequenceGameRandom(0), guard));
        var duringCooldown = new ArenaTalentCombatEvent(ArenaTalentEventType.OnDamageTaken,
            block.SourceActorId!.Value, defender, DateTimeOffset.UnixEpoch.AddSeconds(4), WasBlocked: true);
        Assert.Empty(ArenaTalentEventDispatcher.Dispatch(talents, duringCooldown, new SequenceGameRandom(0), guard));
        var ready = new ArenaTalentCombatEvent(ArenaTalentEventType.OnDamageTaken,
            block.SourceActorId.Value, defender, DateTimeOffset.UnixEpoch.AddSeconds(5), WasBlocked: true);
        Assert.Single(ArenaTalentEventDispatcher.Dispatch(talents, ready, new SequenceGameRandom(0), guard));
    }

    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 2)]
    public void UnsafeDamageDoesNotCreateArenaTriggers(bool periodic, bool proc, int depth)
    {
        CombatEvent input = new(CombatEventType.DamageDealt, DateTimeOffset.UnixEpoch,
            Guid.NewGuid(), Amount: 10, SourceActorId: Guid.NewGuid(),
            TargetActorId: Guid.NewGuid(), IsPeriodic: periodic, IsProc: proc, ProcDepth: depth);
        Assert.Empty(ArenaTalentEventDispatcher.FromDamageEvents([input]));
    }

    [Fact]
    public void SuccessfulAbilityProducesCastHitCritAndDamageTakenWithDamageContext()
    {
        Guid source = Guid.NewGuid();
        Guid target = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition ability = DamageAbility();
        CombatEvent[] events =
        [
            new(CombatEventType.DamageBlocked, now, target, Amount: 4,
                SourceActorId: source, TargetActorId: target, DamageType: DamageType.Physical),
            new(CombatEventType.CriticalHit, now, source, Amount: 16,
                SourceActorId: source, TargetActorId: target, DamageType: DamageType.Physical),
            new(CombatEventType.DamageDealt, now, target, Amount: 16,
                SourceActorId: source, TargetActorId: target, DamageType: DamageType.Physical)
        ];

        IReadOnlyList<ArenaTalentCombatEvent> result =
            ArenaTalentEventDispatcher.FromSuccessfulAbility(source, target, ability, events, now);

        Assert.Single(result, x => x.Type == ArenaTalentEventType.OnCast);
        ArenaTalentCombatEvent hit = Assert.Single(result, x => x.Type == ArenaTalentEventType.OnHit);
        ArenaTalentCombatEvent crit = Assert.Single(result, x => x.Type == ArenaTalentEventType.OnCrit);
        ArenaTalentCombatEvent taken = Assert.Single(result, x => x.Type == ArenaTalentEventType.OnDamageTaken);
        Assert.True(hit.WasBlocked);
        Assert.True(hit.WasCritical);
        Assert.True(crit.WasCritical);
        Assert.True(taken.WasBlocked);
        Assert.True(taken.WasCritical);
        Assert.Equal(16m, taken.FinalDamage);
        Assert.Equal(source, taken.SourceActorId);
        Assert.Equal(target, taken.TargetActorId);
    }

    [Fact]
    public void MissOrDodgeWithoutDamageDoesNotProduceHitCritOrDamageTaken()
    {
        Guid source = Guid.NewGuid();
        Guid target = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatEvent[] events =
        [
            new(CombatEventType.Dodge, now, target,
                SourceActorId: source, TargetActorId: target)
        ];

        IReadOnlyList<ArenaTalentCombatEvent> result =
            ArenaTalentEventDispatcher.FromSuccessfulAbility(source, target, DamageAbility(), events, now);

        Assert.Single(result, x => x.Type == ArenaTalentEventType.OnCast);
        Assert.DoesNotContain(result, x => x.Type == ArenaTalentEventType.OnHit);
        Assert.DoesNotContain(result, x => x.Type == ArenaTalentEventType.OnCrit);
        Assert.DoesNotContain(result, x => x.Type == ArenaTalentEventType.OnDamageTaken);
    }

    [Fact]
    public void FullyBlockedZeroDamageStillProducesDamageTakenBlockReaction()
    {
        Guid source = Guid.NewGuid();
        Guid target = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatEvent[] events =
        [
            new(CombatEventType.DamageBlocked, now, target, Amount: 20,
                SourceActorId: source, TargetActorId: target, DamageType: DamageType.Physical),
            new(CombatEventType.DamageDealt, now, target, Amount: 0,
                SourceActorId: source, TargetActorId: target, DamageType: DamageType.Physical)
        ];

        IReadOnlyList<ArenaTalentCombatEvent> result = ArenaTalentEventDispatcher.FromDamageEvents(events);
        ArenaTalentCombatEvent taken = Assert.Single(result, x => x.Type == ArenaTalentEventType.OnDamageTaken);
        Assert.True(taken.WasBlocked);
        Assert.Equal(0m, taken.FinalDamage);
    }

    [Fact]
    public void BlockHookNormalizesToGenericResourceGainAndRespectsProcChance()
    {
        Guid defender = Guid.NewGuid();
        var combatEvent = new ArenaTalentCombatEvent(
            ArenaTalentEventType.OnDamageTaken,
            Guid.NewGuid(),
            defender,
            DateTimeOffset.UnixEpoch,
            FinalDamage: 5,
            DamageType: DamageType.Physical,
            WasBlocked: true);
        ResolvedTalentModifiers talents = Talents(
            Hook("G-2-5", TalentModifierKeys.OnDamageTaken, "BLOCK", 3, chancePercent: 50));

        ArenaTalentRuntimeEffect success = Assert.Single(ArenaTalentEventDispatcher.Dispatch(
            talents,
            combatEvent,
            new SequenceGameRandom(0.49m)));
        Assert.Equal(ArenaTalentEffectKind.GainResource, success.Kind);
        Assert.Equal(3m, success.Amount);
        Assert.Equal(defender, success.TargetActorId);

        Assert.Empty(ArenaTalentEventDispatcher.Dispatch(
            talents,
            combatEvent,
            new SequenceGameRandom(0.50m)));
    }

    [Fact]
    public void CapabilityIsGranularByTriggerConditionAndExecutor()
    {
        Assert.True(ArenaTalentEventDispatcher.Supports(
            Hook("G-2-5", TalentModifierKeys.OnDamageTaken, "BLOCK", 3)));
        Assert.True(ArenaTalentEventDispatcher.Supports(
            Hook("G-1-5", TalentModifierKeys.OnDamageTaken, "INCOMING_CRITICAL_DAMAGE", 20)));
        Assert.False(ArenaTalentEventDispatcher.Supports(
            Hook("G-6-5", TalentModifierKeys.OnDamageTaken, "GUARDIAN_CAPSTONE", 1)));
    }

    private static AbilityDefinition DamageAbility() => new(
        "TEST_STRIKE",
        AbilityType.Instant,
        AbilityTargetType.SingleEnemy,
        0,
        TimeSpan.Zero,
        TimeSpan.Zero,
        false,
        GlobalCooldownCategory.None,
        false,
        "PHYSICAL",
        Actions: [new AbilityActionDefinition(
            AbilityActionType.Damage,
            20,
            DamageType.Physical,
            CanMiss: false,
            CanCrit: false,
            CanDodge: false)]);

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
        decimal chancePercent = 100) => new(
        talentId,
        key,
        1,
        value,
        targetId,
        TimeSpan.Zero,
        false,
        ChancePercent: chancePercent);
}
