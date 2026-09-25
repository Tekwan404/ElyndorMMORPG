using Elyndor.Core.Combat.Encounters;

namespace Elyndor.UnitTests.Combat;

public sealed class EncounterPhaseEngineTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HpThresholdPhaseFiresOnceAndReturnsSummonAction()
    {
        EncounterDefinition definition = new(
            "STONE_WATCHER",
            "ANCIENT_MINE_BOSS_KAMENNYI_SMOTRITEL_L16",
            [
                new EncounterPhaseDefinition(
                    "AWAKEN_70",
                    new EncounterTriggerDefinition(
                        EncounterTriggerType.HpAtOrBelow,
                        Threshold: 70),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Summon,
                            Summon: new SummonDefinition(
                                "STONE_ADD",
                                Count: 2,
                                MaxActive: 2,
                                LinkToCaster: true))
                    ])
            ]);
        EncounterPhaseRuntimeState state = new();
        EncounterRuntimeSnapshot atSeventy = Snapshot(hp: 70);

        IReadOnlyList<EncounterPhaseResolution> first =
            EncounterPhaseEngine.Evaluate(definition, atSeventy, state);
        IReadOnlyList<EncounterPhaseResolution> second =
            EncounterPhaseEngine.Evaluate(definition, atSeventy, state);

        EncounterPhaseResolution resolved = Assert.Single(first);
        Assert.Equal("AWAKEN_70", resolved.PhaseId);
        Assert.Equal(EncounterActionType.Summon, Assert.Single(resolved.Actions).Type);
        Assert.Empty(second);
    }

    [Fact]
    public void ResourceThresholdCanDriveBossPhase()
    {
        EncounterDefinition definition = new(
            "RESOURCE_BOSS",
            "RESOURCE_BOSS_MONSTER",
            [
                new EncounterPhaseDefinition(
                    "MANA_EMPTY",
                    new EncounterTriggerDefinition(
                        EncounterTriggerType.ResourceAtOrBelow,
                        Threshold: 10),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Shield,
                            Magnitude: 500)
                    ])
            ]);
        EncounterPhaseRuntimeState state = new();
        EncounterRuntimeSnapshot snapshot = new(
            1000,
            1000,
            5,
            100,
            Start,
            Start.AddSeconds(20));

        EncounterPhaseResolution resolution = Assert.Single(
            EncounterPhaseEngine.Evaluate(definition, snapshot, state));

        Assert.Equal("MANA_EMPTY", resolution.PhaseId);
        Assert.Equal(500, Assert.Single(resolution.Actions).Magnitude);
    }

    [Fact]
    public void AddDeathTriggerRequiresMatchingEncounterEvent()
    {
        EncounterDefinition definition = new(
            "LINKED_ADD_BOSS",
            "LINKED_ADD_BOSS_MONSTER",
            [
                new EncounterPhaseDefinition(
                    "WARDER_DEAD",
                    new EncounterTriggerDefinition(
                        EncounterTriggerType.AddDeath,
                        DefinitionId: "WARDER",
                        Once: false),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Vulnerability,
                            Magnitude: 0.20m,
                            Duration: TimeSpan.FromSeconds(8))
                    ])
            ]);
        EncounterPhaseRuntimeState state = new();

        EncounterRuntimeSnapshot unrelated = Snapshot(hp: 100) with
        {
            EventType = EncounterTriggerType.AddDeath,
            EventDefinitionId = "OTHER_ADD"
        };
        EncounterRuntimeSnapshot warderDeath = Snapshot(hp: 100) with
        {
            EventType = EncounterTriggerType.AddDeath,
            EventDefinitionId = "WARDER"
        };

        Assert.Empty(EncounterPhaseEngine.Evaluate(definition, unrelated, state));
        Assert.Single(EncounterPhaseEngine.Evaluate(definition, warderDeath, state));
    }

    [Fact]
    public void ValidatorRejectsInvalidSummonAndDuplicatePhaseIds()
    {
        EncounterDefinition invalid = new(
            "INVALID",
            "BOSS",
            [
                new EncounterPhaseDefinition(
                    "PHASE",
                    new EncounterTriggerDefinition(EncounterTriggerType.HpAtOrBelow, 150),
                    [new EncounterActionDefinition(EncounterActionType.Summon)]),
                new EncounterPhaseDefinition(
                    "PHASE",
                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),
                    [new EncounterActionDefinition(EncounterActionType.SetPhase)])
            ]);

        IReadOnlyList<string> errors = EncounterDefinitionValidator.Validate(invalid);

        Assert.Contains(errors, item => item.Contains("Duplicate", StringComparison.Ordinal));
        Assert.Contains(errors, item => item.Contains("HP threshold", StringComparison.Ordinal));
        Assert.Contains(errors, item => item.Contains("summon action", StringComparison.Ordinal));
        Assert.Contains(errors, item => item.Contains("target phase", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRejectsRepeatableStateAndTimeTriggersButAllowsRepeatableEvents()
    {
        EncounterTriggerDefinition[] unsafeTriggers =
        [
            new(EncounterTriggerType.CombatStart, Once: false),
            new(EncounterTriggerType.HpAtOrBelow, Threshold: 50, Once: false),
            new(
                EncounterTriggerType.ElapsedTime,
                Elapsed: TimeSpan.FromSeconds(5),
                Once: false),
            new(EncounterTriggerType.ResourceAtOrBelow, Threshold: 20, Once: false),
            new(EncounterTriggerType.ResourceAtOrAbove, Threshold: 80, Once: false),
            new(
                EncounterTriggerType.PhaseStart,
                PhaseId: "SOURCE_PHASE",
                Once: false)
        ];

        foreach (EncounterTriggerDefinition trigger in unsafeTriggers)
        {
            EncounterDefinition invalid = new(
                $"INVALID_{trigger.Type}",
                "BOSS",
                [
                    new EncounterPhaseDefinition(
                        "PHASE",
                        trigger,
                        [
                            new EncounterActionDefinition(
                                EncounterActionType.ResourceChange,
                                ResourceAmount: -1)
                        ])
                ]);

            Assert.Contains(
                EncounterDefinitionValidator.Validate(invalid),
                error => error.Contains("once-per-combat", StringComparison.Ordinal));
        }

        EncounterDefinition repeatableEvent = new(
            "REPEATABLE_EVENT",
            "BOSS",
            [
                new EncounterPhaseDefinition(
                    "ADD_DIED",
                    new EncounterTriggerDefinition(
                        EncounterTriggerType.AddDeath,
                        DefinitionId: "ADD",
                        Once: false),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.ResourceChange,
                            ResourceAmount: -1)
                    ])
            ]);

        Assert.DoesNotContain(
            EncounterDefinitionValidator.Validate(repeatableEvent),
            error => error.Contains("once-per-combat", StringComparison.Ordinal));
    }

    [Fact]
    public void OptionalPhaseGateStopsElapsedTriggerAfterEncounterChangesPhase()
    {
        EncounterDefinition definition = new(
            "PHASE_GATED_TIMER",
            "BOSS",
            [
                new EncounterPhaseDefinition(
                    "SUMMON",
                    new EncounterTriggerDefinition(
                        EncounterTriggerType.ElapsedTime,
                        Elapsed: TimeSpan.FromSeconds(5),
                        PhaseId: "SUMMONING"),
                    [new EncounterActionDefinition(EncounterActionType.ResourceChange, ResourceAmount: -1)])
            ]);
        EncounterPhaseRuntimeState state = new();
        state.SetCurrentPhase("FINAL");

        IReadOnlyList<EncounterPhaseResolution> resolutions = EncounterPhaseEngine.Evaluate(
            definition,
            Snapshot(100),
            state);

        Assert.Empty(resolutions);
    }

    private static EncounterRuntimeSnapshot Snapshot(decimal hp) =>
        new(
            hp,
            100,
            100,
            100,
            Start,
            Start.AddSeconds(10));
}
