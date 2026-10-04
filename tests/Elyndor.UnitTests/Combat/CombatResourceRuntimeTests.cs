using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Resources;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatResourceRuntimeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static CombatActorState Actor(decimal current = 50) =>
        new(Guid.NewGuid(), 100, 100, 100, current, CombatStats.Default);

    [Theory]
    [InlineData(200, 100, 50)]
    [InlineData(-200, 0, -50)]
    [InlineData(0, 50, 0)]
    public void ChangeReturnsActualClampedDeltaWithoutReplayingIt(decimal amount, decimal resource, decimal delta)
    {
        CombatActorState actor = Actor();
        CombatEvent e = CombatResourceRuntime.Change(actor, amount, Now, "SOURCE", actor.ActorId);
        Assert.Equal(resource, actor.CurrentResource);
        Assert.Equal(delta, e.Amount);
        Assert.Equal(actor.ActorId, e.SourceActorId);
        Assert.Equal(actor.ActorId, e.TargetActorId);
        Assert.Equal("SOURCE", e.DefinitionId);
        Assert.Equal(Now, e.OccurredAtUtc);
    }

    [Theory]
    [InlineData(20, true, 30)]
    [InlineData(0, true, 50)]
    [InlineData(51, false, 50)]
    [InlineData(-1, false, 50)]
    public void SpendProducesOneResultOnlyAfterSuccessfulAtomicAdmission(decimal cost, bool success, decimal remaining)
    {
        CombatActorState actor = Actor();
        Assert.Equal(success, CombatResourceRuntime.TrySpend(actor, cost, Now, "ABILITY", out CombatEvent? e));
        Assert.Equal(remaining, actor.CurrentResource);
        if (success)
        {
            Assert.NotNull(e);
            Assert.Equal(-cost, e.Amount);
            // AbilityEngine historically normalizes identity later.
            Assert.Null(e.SourceActorId);
            Assert.Null(e.TargetActorId);
        }
        else Assert.Null(e);
    }

    [Fact]
    public void ClassRuleIsAppliedOnceAndPublicationObservesAlreadyMutatedState()
    {
        CombatActorState actor = Actor(95);
        var events = new List<CombatEvent>();
        var runtime = new CombatResourceRuntime(e =>
        {
            Assert.Equal(100, actor.CurrentResource);
            events.Add(e);
        }, (_, amount) => amount * 2, id => id == "TALENT");
        Assert.Equal(5, runtime.Grant(actor, 4, Now, "TALENT"));
        Assert.Equal(0, runtime.Grant(actor, 4, Now, "TALENT"));
        CombatEvent result = Assert.Single(events);
        Assert.Equal(5, result.Amount);
        Assert.True(result.IsProc);
        Assert.Equal(1, result.ProcDepth);
        Assert.Equal("TALENT", result.ProcOriginId);
    }

    [Fact]
    public void RegenSortsDeduplicatesAndClipsBoundariesAndUsesTicksWithoutFloatingPointRounding()
    {
        CombatActorState actor = Actor(0);
        var events = new List<CombatEvent>();
        var runtime = new CombatResourceRuntime(events.Add);
        runtime.Regenerate(actor, Now, Now.AddTicks(3),
            time => time == Now ? 10_000_000m : 20_000_000m,
            [Now.AddTicks(2), Now.AddTicks(2), Now.AddTicks(-1), Now.AddTicks(4)]);
        Assert.Equal(4, actor.CurrentResource);
        Assert.Equal(4, Assert.Single(events).Amount);
        runtime.Regenerate(actor, Now.AddTicks(3), Now.AddTicks(3), _ => 10);
        Assert.Single(events);
    }

    [Fact]
    public void DeadActorDoesNotRegenerateAndZeroGrantDoesNotPublish()
    {
        CombatActorState actor = Actor();
        actor.ApplyDamage(100);
        var events = new List<CombatEvent>();
        var runtime = new CombatResourceRuntime(events.Add);
        runtime.Regenerate(actor, Now, Now.AddSeconds(10), _ => 4);
        Assert.Equal(50, actor.CurrentResource);
        Assert.Equal(0, runtime.Grant(actor, 0, Now, "ZERO"));
        Assert.Empty(events);
    }
}
