using Elyndor.Core.Combat.Encounters;

namespace Elyndor.UnitTests.Combat;

public sealed class ShatteredOrderEncounterRuntimeTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VelariusUsesRemainingManaToBuildFinalBarrier()
    {
        VelariusManaEncounterRuntime runtime = new(VelariusManaEncounterDefinition.Default);

        Assert.True(runtime.TryTriggerFeeders(5_500, 10_000));
        Assert.True(runtime.FeedersTriggered);
        Assert.False(runtime.TryTriggerFinalBarrier(1_001, 10_000, 80, out _));
        Assert.True(runtime.TryTriggerFinalBarrier(1_000, 10_000, 80, out decimal shield));

        Assert.Equal(4_000m, shield);
        Assert.True(runtime.FinalPhaseActive);
        Assert.False(runtime.TryTriggerFinalBarrier(500, 10_000, 50, out _));
    }

    [Fact]
    public void MorEtWavesAreSequentialAndSoulTimeoutIsDeterministic()
    {
        MorEtSoulEncounterRuntime runtime = new(MorEtSoulEncounterDefinition.Default);
        Guid first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid third = Guid.Parse("10000000-0000-0000-0000-000000000003");

        Assert.True(runtime.TryBeginNextWave(
            6_500,
            10_000,
            [first, second, third],
            out IReadOnlyList<Guid> firstWave));
        Assert.Equal([first, second], firstWave);

        Guid firstSoul = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid secondSoul = Guid.Parse("20000000-0000-0000-0000-000000000002");
        runtime.RegisterSoul(firstSoul, first, Now);
        runtime.RegisterSoul(secondSoul, second, Now);
        Assert.False(runtime.TryBeginNextWave(2_000, 10_000, [first, second, third], out _));

        Assert.NotNull(runtime.RegisterSoulDeath(firstSoul));
        Assert.Null(runtime.RegisterSoulTimeout(secondSoul, Now.AddSeconds(17)));
        Assert.NotNull(runtime.RegisterSoulTimeout(secondSoul, Now.AddSeconds(18)));

        Assert.True(runtime.TryBeginNextWave(
            3_000,
            10_000,
            [first, second, third],
            out IReadOnlyList<Guid> secondWave));
        Assert.Equal([first, second, third], secondWave);
        Assert.Equal(0, runtime.RemainingWaves);
    }

    [Fact]
    public void AzraelRevivesDeadClonesAfterWindowAndCompletesOnSynchronizedKill()
    {
        AzraelTriuneEncounterRuntime runtime = new(AzraelTriuneEncounterDefinition.Default);
        Assert.True(runtime.TryTriggerSplit(6_500, 10_000));

        Guid fire = Guid.Parse("30000000-0000-0000-0000-000000000001");
        Guid frost = Guid.Parse("30000000-0000-0000-0000-000000000002");
        Guid abyss = Guid.Parse("30000000-0000-0000-0000-000000000003");
        runtime.RegisterClone(fire, AzraelCloneRole.Fire);
        runtime.RegisterClone(frost, AzraelCloneRole.Frost);
        runtime.RegisterClone(abyss, AzraelCloneRole.Void);

        AzraelCloneDeathResult firstDeath = runtime.RegisterCloneDeath(fire, Now);
        Assert.True(firstDeath.WindowStarted);
        Assert.Equal(Now.AddSeconds(10), firstDeath.WindowExpiresAtUtc);
        Assert.Empty(runtime.ExpireReviveWindow(Now.AddSeconds(9)));
        Assert.Equal([fire], runtime.ExpireReviveWindow(Now.AddSeconds(10)));

        Assert.True(runtime.RegisterCloneDeath(fire, Now.AddSeconds(11)).WindowStarted);
        Assert.True(runtime.RegisterCloneDeath(frost, Now.AddSeconds(12)).Accepted);
        AzraelCloneDeathResult finalDeath = runtime.RegisterCloneDeath(abyss, Now.AddSeconds(13));
        Assert.True(finalDeath.SplitCompleted);
        Assert.True(runtime.SplitCompleted);
        Assert.Null(runtime.WindowExpiresAtUtc);
    }

    [Fact]
    public void KaelMorRunsThreeSequentialOwnerBoundTrials()
    {
        KaelMorTrialEncounterRuntime runtime = new(KaelMorTrialEncounterDefinition.Default);
        Guid owner = Guid.Parse("40000000-0000-0000-0000-000000000001");
        Guid defender = Guid.Parse("50000000-0000-0000-0000-000000000001");
        Guid caster = Guid.Parse("50000000-0000-0000-0000-000000000002");

        Assert.True(runtime.TryBeginNextTrial(7_500, 10_000, [owner], Now, out Guid selected));
        Assert.Equal(owner, selected);
        runtime.RegisterAdd(defender);
        runtime.RegisterAdd(caster);
        Assert.True(runtime.CanActorTargetEnemy(owner, defender));
        Assert.False(runtime.CanActorTargetEnemy(owner, Guid.NewGuid()));
        Assert.False(runtime.CanActorTargetEnemy(Guid.NewGuid(), defender));
        Assert.Null(runtime.RegisterAddDeath(defender));
        KaelMorTrialState success = Assert.IsType<KaelMorTrialState>(runtime.RegisterAddDeath(caster));
        Assert.Equal(owner, success.OwnerActorId);

        Assert.True(runtime.TryBeginNextTrial(5_000, 10_000, [owner], Now, out _));
        runtime.RegisterAdd(defender);
        runtime.RegisterAdd(caster);
        Assert.Null(runtime.RegisterTimeout(defender, Now.AddSeconds(14)));
        KaelMorTrialState failure = Assert.IsType<KaelMorTrialState>(
            runtime.RegisterTimeout(defender, Now.AddSeconds(15)));
        Assert.Equal(2, failure.ActiveAddActorIds.Count);

        Assert.True(runtime.TryBeginNextTrial(2_500, 10_000, [owner], Now, out _));
        Assert.Equal(0, runtime.RemainingTrials);
    }
}
