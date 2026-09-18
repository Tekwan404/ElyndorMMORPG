using Elyndor.Core.Combat.Encounters;

namespace Elyndor.UnitTests.Combat;

public sealed class LinkedSummonRegistryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegistrationTracksOwnerLifetimeAndRewardPolicy()
    {
        LinkedSummonRegistry registry = new();
        Guid actorId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        SummonDefinition definition = new(
            "TEST_ADD",
            Count: 1,
            MaxActive: 3,
            Lifetime: TimeSpan.FromSeconds(12),
            LinkToCaster: true,
            DespawnOnBossDeath: true,
            NoReward: true);

        LinkedSummonRegistration registration = registry.Register(
            actorId,
            ownerId,
            definition,
            Now);

        Assert.Equal(actorId, registration.ActorId);
        Assert.Equal(ownerId, registration.OwnerActorId);
        Assert.Equal(Now.AddSeconds(12), registration.ExpiresAtUtc);
        Assert.False(registry.IsRewardEligible(actorId));
        Assert.Equal(1, registry.CountActive("TEST_ADD"));
    }

    [Fact]
    public void LifetimeExpiryUsesCombatTimestampAndRemovesOnlyDueSummons()
    {
        LinkedSummonRegistry registry = new();
        Guid ownerId = Guid.NewGuid();
        Guid shortLived = Guid.NewGuid();
        Guid longLived = Guid.NewGuid();
        registry.Register(
            shortLived,
            ownerId,
            new SummonDefinition("ADD", 1, Lifetime: TimeSpan.FromSeconds(5)),
            Now);
        registry.Register(
            longLived,
            ownerId,
            new SummonDefinition("ADD", 1, Lifetime: TimeSpan.FromSeconds(10)),
            Now);

        IReadOnlyList<LinkedSummonRegistration> expired =
            registry.CollectExpired(Now.AddSeconds(5));

        LinkedSummonRegistration removed = Assert.Single(expired);
        Assert.Equal(shortLived, removed.ActorId);
        Assert.Equal(1, registry.CountActive("ADD"));
        Assert.Contains(registry.Active, item => item.ActorId == longLived);
    }

    [Fact]
    public void OwnerDeathDespawnsOnlySummonsConfiguredForIt()
    {
        LinkedSummonRegistry registry = new();
        Guid ownerId = Guid.NewGuid();
        Guid linked = Guid.NewGuid();
        Guid persistent = Guid.NewGuid();
        registry.Register(
            linked,
            ownerId,
            new SummonDefinition("LINKED", 1, DespawnOnBossDeath: true),
            Now);
        registry.Register(
            persistent,
            ownerId,
            new SummonDefinition("PERSISTENT", 1, DespawnOnBossDeath: false),
            Now);

        IReadOnlyList<LinkedSummonRegistration> despawned =
            registry.CollectForOwnerDeath(ownerId);

        LinkedSummonRegistration removed = Assert.Single(despawned);
        Assert.Equal(linked, removed.ActorId);
        Assert.Contains(registry.Active, item => item.ActorId == persistent);
    }

    [Fact]
    public void RegistryEnforcesMaxActiveAtRegistrationBoundary()
    {
        LinkedSummonRegistry registry = new();
        Guid ownerId = Guid.NewGuid();
        SummonDefinition definition = new("CAPPED", 1, MaxActive: 2);
        registry.Register(Guid.NewGuid(), ownerId, definition, Now);
        registry.Register(Guid.NewGuid(), ownerId, definition, Now);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(Guid.NewGuid(), ownerId, definition, Now));

        Assert.Contains("cap", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, registry.CountActive("CAPPED"));
    }

    [Fact]
    public void NaturalDeathRemovalRestoresRewardEligibilityLookupAndCapacity()
    {
        LinkedSummonRegistry registry = new();
        Guid ownerId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        SummonDefinition definition = new(
            "CAPPED",
            1,
            MaxActive: 1,
            NoReward: true);
        registry.Register(actorId, ownerId, definition, Now);

        Assert.True(registry.TryRemove(actorId, out LinkedSummonRegistration? removed));
        Assert.NotNull(removed);
        Assert.True(registry.IsRewardEligible(actorId));

        registry.Register(Guid.NewGuid(), ownerId, definition, Now.AddSeconds(1));
        Assert.Equal(1, registry.CountActive("CAPPED"));
    }
}
