using Elyndor.Core.Combat;
using Elyndor.Core.Combat.SetPassives;

namespace Elyndor.UnitTests.Combat;

public sealed class SetPassiveEvaluatorTests
{
    private const string GuardianSetId = "SET_ANCIENT_MINE_WARRIOR_GUARDIAN";
    private const string GuardianTwoPieceId = "SET_ANCIENT_MINE_WARRIOR_GUARDIAN_2PC_PASSIVE";
    private const string GuardianFourPieceId = "SET_ANCIENT_MINE_WARRIOR_GUARDIAN_4PC_PASSIVE";

    [Fact]
    public void BelowRequiredPiecesDoesNotProcOrCreateRuntimeState()
    {
        Guid attacker = Guid.NewGuid();
        Guid defender = Guid.NewGuid();
        SetPassiveEvaluator evaluator = new([GuardianTwoPiece()]);
        SetPassiveRuntimeState state = new();

        IReadOnlyList<SetPassiveActionInvocation> actions = evaluator.Evaluate(
            BlockEvent(attacker, defender, tick: 10),
            PieceCounts((defender, GuardianSetId, 1)),
            state);

        Assert.Empty(actions);
        Assert.Equal(0, state.Count);
    }

    [Fact]
    public void TwoPieceBlockProcsImmediatelyForDefender()
    {
        Guid attacker = Guid.NewGuid();
        Guid defender = Guid.NewGuid();
        SetPassiveEvaluator evaluator = new([GuardianTwoPiece()]);
        SetPassiveRuntimeState state = new();

        SetPassiveActionInvocation action = Assert.Single(evaluator.Evaluate(
            BlockEvent(attacker, defender, tick: 10),
            PieceCounts((defender, GuardianSetId, 2)),
            state));

        Assert.Equal(GuardianTwoPieceId, action.PassiveId);
        Assert.Equal(defender, action.ActorId);
        Assert.Equal(SetPassiveActionKind.ApplyEffect, action.Action.Kind);
        Assert.Equal("EFFECT_GUARDIAN_BLOCK_ARMOR", action.Action.ReferenceId);
    }

    [Fact]
    public void FourPieceProcsOnlyOnEveryThirdEligibleBlock()
    {
        Guid attacker = Guid.NewGuid();
        Guid defender = Guid.NewGuid();
        SetPassiveEvaluator evaluator = new([GuardianFourPiece()]);
        SetPassiveRuntimeState state = new();
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces =
            PieceCounts((defender, GuardianSetId, 4));

        Assert.Empty(evaluator.Evaluate(BlockEvent(attacker, defender, 1), pieces, state));
        Assert.Empty(evaluator.Evaluate(BlockEvent(attacker, defender, 2), pieces, state));
        Assert.Single(evaluator.Evaluate(BlockEvent(attacker, defender, 3), pieces, state));
        Assert.Empty(evaluator.Evaluate(BlockEvent(attacker, defender, 4), pieces, state));
        Assert.Empty(evaluator.Evaluate(BlockEvent(attacker, defender, 5), pieces, state));
        Assert.Single(evaluator.Evaluate(BlockEvent(attacker, defender, 6), pieces, state));

        Assert.Equal(0, state.Get(defender, GuardianFourPieceId).Occurrences);
    }

    [Fact]
    public void UnrelatedEventsDoNotAdvanceOccurrenceCounter()
    {
        Guid attacker = Guid.NewGuid();
        Guid defender = Guid.NewGuid();
        SetPassiveEvaluator evaluator = new([GuardianFourPiece()]);
        SetPassiveRuntimeState state = new();
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces =
            PieceCounts((defender, GuardianSetId, 4));

        Assert.Empty(evaluator.Evaluate(BlockEvent(attacker, defender, 1), pieces, state));
        Assert.Empty(evaluator.Evaluate(
            new CombatEvent(2, 2, CombatEventType.DamageDealt, attacker, defender, Value: 12m),
            pieces,
            state));
        Assert.Empty(evaluator.Evaluate(BlockEvent(attacker, defender, 3), pieces, state));

        Assert.Equal(2, state.Get(defender, GuardianFourPieceId).Occurrences);
    }

    [Fact]
    public void InternalCooldownBlocksRetriggerUntilEligibleTick()
    {
        Guid attacker = Guid.NewGuid();
        Guid defender = Guid.NewGuid();
        SetPassiveDefinition definition = GuardianTwoPiece() with { InternalCooldownTicks = 10 };
        SetPassiveEvaluator evaluator = new([definition]);
        SetPassiveRuntimeState state = new();
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces =
            PieceCounts((defender, GuardianSetId, 2));

        Assert.Single(evaluator.Evaluate(BlockEvent(attacker, defender, 10), pieces, state));
        Assert.Empty(evaluator.Evaluate(BlockEvent(attacker, defender, 19), pieces, state));
        Assert.Single(evaluator.Evaluate(BlockEvent(attacker, defender, 20), pieces, state));

        Assert.Equal(30, state.Get(defender, GuardianTwoPieceId).CooldownUntilTick);
    }

    [Fact]
    public void RuntimeStateIsIndependentPerActor()
    {
        Guid attacker = Guid.NewGuid();
        Guid firstDefender = Guid.NewGuid();
        Guid secondDefender = Guid.NewGuid();
        SetPassiveEvaluator evaluator = new([GuardianFourPiece()]);
        SetPassiveRuntimeState state = new();
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces = PieceCounts(
            (firstDefender, GuardianSetId, 4),
            (secondDefender, GuardianSetId, 4));

        evaluator.Evaluate(BlockEvent(attacker, firstDefender, 1), pieces, state);
        evaluator.Evaluate(BlockEvent(attacker, firstDefender, 2), pieces, state);
        evaluator.Evaluate(BlockEvent(attacker, secondDefender, 3), pieces, state);

        Assert.Equal(2, state.Get(firstDefender, GuardianFourPieceId).Occurrences);
        Assert.Equal(1, state.Get(secondDefender, GuardianFourPieceId).Occurrences);
    }

    [Fact]
    public void DefinitionAndActionOrderIsDeterministic()
    {
        Guid attacker = Guid.NewGuid();
        Guid defender = Guid.NewGuid();
        SetPassiveDefinition first = GuardianTwoPiece() with
        {
            Actions =
            [
                new(SetPassiveActionKind.ApplyEffect, "FIRST_A"),
                new(SetPassiveActionKind.GrantProcToken, "FIRST_B")
            ]
        };
        SetPassiveDefinition second = first with
        {
            Id = "SECOND_PASSIVE",
            Actions = [new(SetPassiveActionKind.ApplyShield, "SECOND_A")]
        };
        SetPassiveEvaluator evaluator = new([first, second]);

        IReadOnlyList<SetPassiveActionInvocation> actions = evaluator.Evaluate(
            BlockEvent(attacker, defender, 1),
            PieceCounts((defender, GuardianSetId, 2)),
            new SetPassiveRuntimeState());

        Assert.Collection(
            actions,
            action => Assert.Equal("FIRST_A", action.Action.ReferenceId),
            action => Assert.Equal("FIRST_B", action.Action.ReferenceId),
            action => Assert.Equal("SECOND_A", action.Action.ReferenceId));
    }

    [Fact]
    public void BlockTargetRoleNeverUsesAttackersSetPieces()
    {
        Guid attacker = Guid.NewGuid();
        Guid defender = Guid.NewGuid();
        SetPassiveEvaluator evaluator = new([GuardianTwoPiece()]);

        IReadOnlyList<SetPassiveActionInvocation> actions = evaluator.Evaluate(
            BlockEvent(attacker, defender, 1),
            PieceCounts((attacker, GuardianSetId, 4)),
            new SetPassiveRuntimeState());

        Assert.Empty(actions);
    }

    private static SetPassiveDefinition GuardianTwoPiece() => new(
        GuardianTwoPieceId,
        GuardianSetId,
        RequiredPieces: 2,
        new SetPassiveTriggerDefinition(CombatEventType.Block, SetPassiveActorRole.Target),
        [new SetPassiveActionDefinition(SetPassiveActionKind.ApplyEffect, "EFFECT_GUARDIAN_BLOCK_ARMOR")]);

    private static SetPassiveDefinition GuardianFourPiece() => new(
        GuardianFourPieceId,
        GuardianSetId,
        RequiredPieces: 4,
        new SetPassiveTriggerDefinition(
            CombatEventType.Block,
            SetPassiveActorRole.Target,
            RequiredOccurrences: 3),
        [new SetPassiveActionDefinition(SetPassiveActionKind.ApplyShield, "SHIELD_GUARDIAN_THIRD_BLOCK")]);

    private static CombatEvent BlockEvent(Guid attacker, Guid defender, int tick) =>
        new(tick, tick, CombatEventType.Block, attacker, defender, Value: 5m);

    private static IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> PieceCounts(
        params (Guid ActorId, string SetId, int Pieces)[] entries)
    {
        Dictionary<Guid, Dictionary<string, int>> mutable = new();
        foreach ((Guid actorId, string setId, int pieces) in entries)
        {
            if (!mutable.TryGetValue(actorId, out Dictionary<string, int>? sets))
            {
                sets = new Dictionary<string, int>(StringComparer.Ordinal);
                mutable[actorId] = sets;
            }

            sets[setId] = pieces;
        }

        return mutable.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, int>)pair.Value);
    }
}
