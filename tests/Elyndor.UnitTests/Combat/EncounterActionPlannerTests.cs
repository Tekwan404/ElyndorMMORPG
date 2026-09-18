using Elyndor.Core.Combat.Encounters;

namespace Elyndor.UnitTests.Combat;

public sealed class EncounterActionPlannerTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 9, 18, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SummonActionHonorsMaxActiveAndPreservesLinkedSummonMetadata()
    {
        EncounterDefinition encounter = SinglePhase(
            new EncounterActionDefinition(
                EncounterActionType.Summon,
                Summon: new SummonDefinition(
                    "TEST_ADD",
                    Count: 4,
                    MaxActive: 5,
                    Lifetime: TimeSpan.FromSeconds(12),
                    LinkToCaster: true,
                    DespawnOnBossDeath: true,
                    NoReward: true)));

        IReadOnlyList<EncounterPlannedAction> planned = EncounterActionPlanner.Plan(
            encounter,
            Snapshot(StartedAt),
            new EncounterPhaseRuntimeState(),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["TEST_ADD"] = 3
            });

        EncounterPlannedAction action = Assert.Single(planned);
        Assert.NotNull(action.Action.Summon);
        Assert.Equal(2, action.Action.Summon!.Count);
        Assert.Equal(5, action.Action.Summon.MaxActive);
        Assert.Equal(TimeSpan.FromSeconds(12), action.Action.Summon.Lifetime);
        Assert.True(action.Action.Summon.LinkToCaster);
        Assert.True(action.Action.Summon.DespawnOnBossDeath);
        Assert.True(action.Action.Summon.NoReward);
    }

    [Fact]
    public void MultipleSummonActionsShareTheSameResolvedActiveCap()
    {
        EncounterDefinition encounter = SinglePhase(
            new EncounterActionDefinition(
                EncounterActionType.Summon,
                Summon: new SummonDefinition("TEST_ADD", Count: 2, MaxActive: 3)),
            new EncounterActionDefinition(
                EncounterActionType.Summon,
                Summon: new SummonDefinition("TEST_ADD", Count: 2, MaxActive: 3)));

        IReadOnlyList<EncounterPlannedAction> planned = EncounterActionPlanner.Plan(
            encounter,
            Snapshot(StartedAt),
            new EncounterPhaseRuntimeState());

        Assert.Equal(2, planned.Count);
        Assert.Equal(2, planned[0].Action.Summon!.Count);
        Assert.Equal(1, planned[1].Action.Summon!.Count);
    }

    [Fact]
    public void SummonActionIsSkippedWhenMaxActiveIsAlreadyReached()
    {
        EncounterDefinition encounter = SinglePhase(
            new EncounterActionDefinition(
                EncounterActionType.Summon,
                Summon: new SummonDefinition("TEST_ADD", Count: 2, MaxActive: 2)));

        IReadOnlyList<EncounterPlannedAction> planned = EncounterActionPlanner.Plan(
            encounter,
            Snapshot(StartedAt),
            new EncounterPhaseRuntimeState(),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["TEST_ADD"] = 2
            });

        Assert.Empty(planned);
    }

    [Fact]
    public void DelayedActionCarriesDeterministicExecutionTimestamp()
    {
        EncounterDefinition encounter = SinglePhase(
            new EncounterActionDefinition(
                EncounterActionType.ApplyEffect,
                EffectId: "TEST_BOMB",
                Delay: TimeSpan.FromSeconds(4)));
        DateTimeOffset now = StartedAt.AddSeconds(10);

        EncounterPlannedAction planned = Assert.Single(EncounterActionPlanner.Plan(
            encounter,
            Snapshot(now),
            new EncounterPhaseRuntimeState()));

        Assert.Equal(now.AddSeconds(4), planned.ExecuteAtUtc);
        Assert.Equal("TEST_BOMB", planned.Action.EffectId);
    }

    [Fact]
    public void OncePhaseDoesNotPlanActionsTwice()
    {
        EncounterDefinition encounter = SinglePhase(
            new EncounterActionDefinition(
                EncounterActionType.ApplyEffect,
                EffectId: "TEST_EFFECT"));
        EncounterPhaseRuntimeState state = new();
        EncounterRuntimeSnapshot snapshot = Snapshot(StartedAt);

        IReadOnlyList<EncounterPlannedAction> first = EncounterActionPlanner.Plan(
            encounter,
            snapshot,
            state);
        IReadOnlyList<EncounterPlannedAction> second = EncounterActionPlanner.Plan(
            encounter,
            snapshot,
            state);

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public void NegativeDelayAndNonPositiveSummonLifetimeAreRejected()
    {
        EncounterDefinition encounter = SinglePhase(
            new EncounterActionDefinition(
                EncounterActionType.Summon,
                Summon: new SummonDefinition(
                    "TEST_ADD",
                    Count: 1,
                    Lifetime: TimeSpan.Zero),
                Delay: TimeSpan.FromSeconds(-1)));

        IReadOnlyList<string> errors = EncounterDefinitionValidator.Validate(encounter);

        Assert.Contains(errors, error => error.Contains("delay", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("summon", StringComparison.OrdinalIgnoreCase));
    }

    private static EncounterDefinition SinglePhase(
        params EncounterActionDefinition[] actions) =>
        new(
            "TEST_ENCOUNTER",
            "TEST_BOSS",
            [
                new EncounterPhaseDefinition(
                    "START",
                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),
                    actions)
            ]);

    private static EncounterRuntimeSnapshot Snapshot(DateTimeOffset now) =>
        new(
            CurrentHp: 100,
            MaxHp: 100,
            CurrentResource: 100,
            MaxResource: 100,
            StartedAtUtc: StartedAt,
            Now: now);
}
