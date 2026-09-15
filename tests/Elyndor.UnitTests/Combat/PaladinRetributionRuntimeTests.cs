using Elyndor.Core.Combat.Paladin;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinRetributionRuntimeTests
{
    [Fact]
    public void VengeanceCapsAtThreeStacksAndCriticalHitsRefreshDuration()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        PaladinRetributionRuntime runtime = new();
        TimeSpan duration = TimeSpan.FromSeconds(12);

        runtime.RecordPhysicalOrHolyCritical(now, duration);
        runtime.RecordPhysicalOrHolyCritical(now.AddSeconds(1), duration);
        runtime.RecordPhysicalOrHolyCritical(now.AddSeconds(2), duration);
        runtime.RecordPhysicalOrHolyCritical(now.AddSeconds(3), duration);

        Assert.Equal(3, runtime.VengeanceStacks);
        Assert.Equal(now.AddSeconds(15), runtime.VengeanceEndsAtUtc);

        runtime.ExpireVengeance(now.AddSeconds(14.999));
        Assert.Equal(3, runtime.VengeanceStacks);
        runtime.ExpireVengeance(now.AddSeconds(15));
        Assert.Equal(0, runtime.VengeanceStacks);
        Assert.Null(runtime.VengeanceEndsAtUtc);
    }

    [Fact]
    public void EveryThirdSuccessfulJudgementArmsDivinePurpose()
    {
        PaladinRetributionRuntime runtime = new();

        Assert.False(runtime.RecordSuccessfulJudgement(hasDivinePurpose: true));
        Assert.False(runtime.RecordSuccessfulJudgement(hasDivinePurpose: true));
        Assert.True(runtime.RecordSuccessfulJudgement(hasDivinePurpose: true));
        Assert.True(runtime.DivinePurposeArmed);
        Assert.True(runtime.ConsumeDivinePurposeForTemplarsVerdict());
        Assert.False(runtime.ConsumeDivinePurposeForTemplarsVerdict());
    }

    [Fact]
    public void JudgementAlwaysArmsZealButDoesNotCountDivinePurposeWithoutTalent()
    {
        PaladinRetributionRuntime runtime = new();

        for (var index = 0; index < 5; index++)
            Assert.False(runtime.RecordSuccessfulJudgement(hasDivinePurpose: false));

        Assert.False(runtime.DivinePurposeArmed);
        Assert.True(runtime.ZealArmed);
        Assert.True(runtime.ConsumeZealForNextAutoAttack());
        Assert.False(runtime.ConsumeZealForNextAutoAttack());
    }

    [Fact]
    public void IncarnationBonusesAreEachConsumedOnceInsideAvengingWrath()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        PaladinRetributionRuntime runtime = new();
        runtime.ActivateAvengingWrath(
            now,
            TimeSpan.FromSeconds(20),
            hasIncarnationOfRetribution: true);

        Assert.True(runtime.ConsumeIncarnationTemplarCritical(now.AddSeconds(1)));
        Assert.False(runtime.ConsumeIncarnationTemplarCritical(now.AddSeconds(2)));
        Assert.True(runtime.ConsumeIncarnationJudgementCrusaderReset(now.AddSeconds(3)));
        Assert.False(runtime.ConsumeIncarnationJudgementCrusaderReset(now.AddSeconds(4)));
    }

    [Fact]
    public void IncarnationBonusesCannotBeConsumedAfterAvengingWrathEnds()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        PaladinRetributionRuntime runtime = new();
        runtime.ActivateAvengingWrath(
            now,
            TimeSpan.FromSeconds(20),
            hasIncarnationOfRetribution: true);

        Assert.False(runtime.ConsumeIncarnationTemplarCritical(now.AddSeconds(20)));
        Assert.False(runtime.ConsumeIncarnationJudgementCrusaderReset(now.AddSeconds(20)));
    }

    [Fact]
    public void AvengingWrathWithoutCapstoneDoesNotArmIncarnationBonuses()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        PaladinRetributionRuntime runtime = new();
        runtime.ActivateAvengingWrath(
            now,
            TimeSpan.FromSeconds(10),
            hasIncarnationOfRetribution: false);

        Assert.True(runtime.IsAvengingWrathActive(now));
        Assert.False(runtime.ConsumeIncarnationTemplarCritical(now));
        Assert.False(runtime.ConsumeIncarnationJudgementCrusaderReset(now));
    }
}
