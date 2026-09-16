using Elyndor.Core.Combat.Encounters;

namespace Elyndor.UnitTests.Combat;

public sealed class MirrorBarrierEncounterRuntimeTests
{
    [Fact]
    public void WaveDoesNotBeginAboveConfiguredHpThreshold()
    {
        MirrorBarrierEncounterRuntime runtime = new(MirrorBarrierEncounterDefinition.Default);

        bool started = runtime.TryBeginNextWave(701, 1000, out MirrorEncounterWave? wave);

        Assert.False(started);
        Assert.Null(wave);
        Assert.False(runtime.BarrierActive);
        Assert.Equal(2, runtime.RemainingWaves);
    }

    [Fact]
    public void FirstWaveBeginsAtSeventyPercentHp()
    {
        MirrorBarrierEncounterRuntime runtime = new(MirrorBarrierEncounterDefinition.Default);

        bool started = runtime.TryBeginNextWave(700, 1000, out MirrorEncounterWave? wave);

        Assert.True(started);
        Assert.NotNull(wave);
        Assert.Equal(0.70m, wave.TriggerHpPercent);
        Assert.Equal(
            [
                MirrorEncounterAddRole.Guardian,
                MirrorEncounterAddRole.Priest,
                MirrorEncounterAddRole.Executioner
            ],
            wave.RequiredRoles);
        Assert.True(runtime.BarrierActive);
        Assert.Equal(1, runtime.RemainingWaves);
    }

    [Fact]
    public void GuardianControlsAdditionalReflectionWhileBarrierIsActive()
    {
        (MirrorBarrierEncounterRuntime runtime, Guid guardian, Guid priest, Guid executioner) =
            StartFirstWave();

        Assert.Equal(0.70m, runtime.ReflectionRatio);

        MirrorBarrierBroken? broken = runtime.RegisterAddDeath(guardian, UtcNow());

        Assert.Null(broken);
        Assert.True(runtime.BarrierActive);
        Assert.Equal(0.50m, runtime.ReflectionRatio);
        Assert.Equal(MirrorEncounterAddRole.Priest, runtime.GetRole(priest));
        Assert.Equal(MirrorEncounterAddRole.Executioner, runtime.GetRole(executioner));
    }

    [Fact]
    public void LastAddDeathBreaksBarrierAndOpensEightSecondBurstWindow()
    {
        (MirrorBarrierEncounterRuntime runtime, Guid guardian, Guid priest, Guid executioner) =
            StartFirstWave();
        DateTimeOffset now = UtcNow();

        Assert.Null(runtime.RegisterAddDeath(guardian, now));
        Assert.Null(runtime.RegisterAddDeath(priest, now));
        MirrorBarrierBroken? broken = runtime.RegisterAddDeath(executioner, now);

        Assert.NotNull(broken);
        Assert.False(runtime.BarrierActive);
        Assert.Equal(0m, runtime.ReflectionRatio);
        Assert.Equal(now, broken.StartedAtUtc);
        Assert.Equal(now.AddSeconds(8), broken.ExpiresAtUtc);
        Assert.Equal(1.25m, broken.DamageTakenMultiplier);
        Assert.True(runtime.IsBrokenMirrorActive(now.AddSeconds(7.999)));
        Assert.Equal(1.25m, runtime.GetBossDamageTakenMultiplier(now.AddSeconds(7.999)));
        Assert.False(runtime.IsBrokenMirrorActive(now.AddSeconds(8)));
        Assert.Equal(1m, runtime.GetBossDamageTakenMultiplier(now.AddSeconds(8)));
    }

    [Fact]
    public void SecondWaveBeginsOnlyAfterFirstBarrierResolvedAndAtThirtyFivePercentHp()
    {
        (MirrorBarrierEncounterRuntime runtime, Guid guardian, Guid priest, Guid executioner) =
            StartFirstWave();

        Assert.False(runtime.TryBeginNextWave(300, 1000, out _));

        DateTimeOffset now = UtcNow();
        runtime.RegisterAddDeath(guardian, now);
        runtime.RegisterAddDeath(priest, now);
        runtime.RegisterAddDeath(executioner, now);

        Assert.False(runtime.TryBeginNextWave(351, 1000, out _));
        Assert.True(runtime.TryBeginNextWave(350, 1000, out MirrorEncounterWave? wave));
        Assert.NotNull(wave);
        Assert.Equal(0.35m, wave.TriggerHpPercent);
        Assert.Equal(0, runtime.RemainingWaves);
        Assert.True(runtime.BarrierActive);
    }

    [Fact]
    public void DuplicateRoleCannotBeRegisteredWithinSameWave()
    {
        MirrorBarrierEncounterRuntime runtime = new(MirrorBarrierEncounterDefinition.Default);
        Assert.True(runtime.TryBeginNextWave(700, 1000, out _));
        runtime.RegisterAdd(Guid.NewGuid(), MirrorEncounterAddRole.Guardian);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            runtime.RegisterAdd(Guid.NewGuid(), MirrorEncounterAddRole.Guardian));

        Assert.Contains("already registered", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static (
        MirrorBarrierEncounterRuntime Runtime,
        Guid Guardian,
        Guid Priest,
        Guid Executioner) StartFirstWave()
    {
        MirrorBarrierEncounterRuntime runtime = new(MirrorBarrierEncounterDefinition.Default);
        Assert.True(runtime.TryBeginNextWave(700, 1000, out _));

        Guid guardian = Guid.NewGuid();
        Guid priest = Guid.NewGuid();
        Guid executioner = Guid.NewGuid();
        runtime.RegisterAdd(guardian, MirrorEncounterAddRole.Guardian);
        runtime.RegisterAdd(priest, MirrorEncounterAddRole.Priest);
        runtime.RegisterAdd(executioner, MirrorEncounterAddRole.Executioner);
        return (runtime, guardian, priest, executioner);
    }

    private static DateTimeOffset UtcNow() =>
        new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);
}
