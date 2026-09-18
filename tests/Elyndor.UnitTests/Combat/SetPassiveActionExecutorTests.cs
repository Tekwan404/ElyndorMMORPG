using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.SetPassives;

namespace Elyndor.UnitTests.Combat;

public sealed class SetPassiveActionExecutorTests
{
    private const string GuardianSetId = "SET_ANCIENT_MINE_WARRIOR_GUARDIAN";
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GuardianTwoPieceRealBlockedHitAppliesAndRefreshesArmorThroughEffectEngine()
    {
        CombatActorState attacker = Actor(Guid.NewGuid(), armor: 0);
        CombatActorState defender = Actor(
            Guid.NewGuid(),
            armor: 100,
            blockChance: 100,
            blockValueMin: 10,
            blockValueMax: 10);
        SetPassiveRuntime runtime = new([TestGuardianTwoPiece()]);
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces =
            PieceCounts(defender.ActorId, 2);

        CombatEvent firstBlock = ResolveRealBlock(attacker, defender, Now);
        SetPassiveActionInvocation first = Assert.Single(runtime.Evaluate(firstBlock, pieces));
        Assert.Single(SetPassiveActionExecutor.Execute(first, defender));
        Assert.Equal(
            125m,
            EffectEngine.CalculateStat(defender, EffectStat.Armor, 100m, Now));

        DateTimeOffset refreshedAt = Now.AddSeconds(4);
        CombatEvent secondBlock = ResolveRealBlock(attacker, defender, refreshedAt);
        SetPassiveActionInvocation second = Assert.Single(runtime.Evaluate(secondBlock, pieces));
        Assert.Single(SetPassiveActionExecutor.Execute(second, defender));

        ActiveEffect effect = Assert.Single(defender.ActiveEffects.Where(item =>
            item.Definition.Id == "TEST_GUARDIAN_BLOCK_ARMOR"));
        Assert.Equal(refreshedAt.AddSeconds(10), effect.ExpiresAtUtc);
        Assert.Equal(
            125m,
            EffectEngine.CalculateStat(defender, EffectStat.Armor, 100m, refreshedAt));
    }

    [Fact]
    public void GuardianFourthPieceThirdBlockCreatesRealAbsorbForDamagePipeline()
    {
        Guid attackerId = Guid.NewGuid();
        CombatActorState attacker = Actor(attackerId, armor: 0);
        CombatActorState defender = Actor(Guid.NewGuid(), armor: 0);
        SetPassiveRuntime runtime = new([TestGuardianFourPiece()]);
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces =
            PieceCounts(defender.ActorId, 4);

        Assert.Empty(runtime.Evaluate(BlockEvent(attackerId, defender.ActorId, Now), pieces));
        Assert.Empty(runtime.Evaluate(BlockEvent(attackerId, defender.ActorId, Now.AddSeconds(1)), pieces));
        SetPassiveActionInvocation proc = Assert.Single(runtime.Evaluate(
            BlockEvent(attackerId, defender.ActorId, Now.AddSeconds(2)),
            pieces));
        Assert.Single(SetPassiveActionExecutor.Execute(proc, defender));

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                attacker,
                defender,
                BaseAmount: 100m,
                Type: DamageType.True,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                CanBlock: false),
            new SeededGameRandom(1),
            Now.AddSeconds(3));

        Assert.Equal(40m, result.AbsorbedByShields);
        Assert.Equal(60m, result.HpDamage);
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.ShieldAbsorbed
            && item.DefinitionId == "TEST_GUARDIAN_THIRD_BLOCK_SHIELD"
            && item.Amount == 40m);
    }

    [Fact]
    public void UnconfiguredProductionBalanceActionDoesNotCreateDecorativeEffect()
    {
        CombatActorState defender = Actor(Guid.NewGuid(), armor: 100);
        SetPassiveDefinition definition = Assert.Single(SetPassiveCatalog.Definitions.Where(item =>
            item.Id == SetPassiveCatalog.AncientMineGuardianTwoPieceId));
        SetPassiveRuntime runtime = new([definition]);

        SetPassiveActionInvocation invocation = Assert.Single(runtime.Evaluate(
            BlockEvent(Guid.NewGuid(), defender.ActorId, Now),
            PieceCounts(defender.ActorId, 2)));

        Assert.Empty(SetPassiveActionExecutor.Execute(invocation, defender));
        Assert.Empty(defender.ActiveEffects);
    }

    private static SetPassiveDefinition TestGuardianTwoPiece() => new(
        "TEST_GUARDIAN_2PC",
        GuardianSetId,
        RequiredPieces: 2,
        new SetPassiveTriggerDefinition(CombatEventType.DamageBlocked, SetPassiveActorRole.Target),
        new SetPassiveConditionDefinition(),
        [
            new SetPassiveActionDefinition(
                SetPassiveActionKind.ApplyEffect,
                "TEST_GUARDIAN_BLOCK_ARMOR",
                Magnitude: 0.25m,
                Duration: TimeSpan.FromSeconds(10),
                ModifiedStat: EffectStat.Armor,
                ModifierMode: EffectModifierMode.Percent,
                StackPolicy: EffectStackPolicy.Refresh)
        ]);

    private static SetPassiveDefinition TestGuardianFourPiece() => new(
        "TEST_GUARDIAN_4PC",
        GuardianSetId,
        RequiredPieces: 4,
        new SetPassiveTriggerDefinition(CombatEventType.DamageBlocked, SetPassiveActorRole.Target),
        new SetPassiveConditionDefinition(EveryNth: 3),
        [
            new SetPassiveActionDefinition(
                SetPassiveActionKind.AddShield,
                "TEST_GUARDIAN_THIRD_BLOCK_SHIELD",
                Magnitude: 40m,
                Duration: TimeSpan.FromSeconds(10),
                StackPolicy: EffectStackPolicy.Replace)
        ]);

    private static CombatActorState Actor(
        Guid id,
        decimal armor,
        decimal blockChance = 0,
        decimal blockValueMin = 0,
        decimal blockValueMax = 0) => new(
        id,
        maxHp: 500,
        currentHp: 500,
        maxResource: 100,
        currentResource: 0,
        new CombatStats(
            Level: 20,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: armor,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            BlockChance: blockChance,
            BlockValueMin: blockValueMin,
            BlockValueMax: blockValueMax));

    private static CombatEvent ResolveRealBlock(
        CombatActorState attacker,
        CombatActorState defender,
        DateTimeOffset occurredAt)
    {
        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                attacker,
                defender,
                BaseAmount: 40m,
                Type: DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom(0m),
            occurredAt);

        return Assert.Single(result.Events.Where(item => item.Type == CombatEventType.DamageBlocked));
    }

    private static CombatEvent BlockEvent(
        Guid attackerId,
        Guid defenderId,
        DateTimeOffset occurredAt) =>
        new(
            CombatEventType.DamageBlocked,
            occurredAt,
            defenderId,
            Amount: 5m,
            SourceActorId: attackerId,
            TargetActorId: defenderId);

    private static IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> PieceCounts(
        Guid actorId,
        int pieces) =>
        new Dictionary<Guid, IReadOnlyDictionary<string, int>>
        {
            [actorId] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [GuardianSetId] = pieces
            }
        };
}
