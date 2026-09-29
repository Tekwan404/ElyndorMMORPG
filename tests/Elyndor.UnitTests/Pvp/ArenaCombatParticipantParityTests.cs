using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaCombatParticipantParityTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountA = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountB = Guid.Parse("72000000-0000-0000-0000-000000000002");
    private static readonly Guid ActorA = Guid.Parse("73000000-0000-0000-0000-000000000003");
    private static readonly Guid ActorB = Guid.Parse("74000000-0000-0000-0000-000000000004");
    private const string HawkEffectId = "ARENA_TALENT_M-2-4_ATTACK_SPEED";

    [Fact]
    public void AssemblerPreservesAuthoritativeRegenAndAutoAttackEligibility()
    {
        var actor = Actor(ActorA, currentResource: 10);
        var participant = new CombatParticipantDefinition(
            actor,
            CombatActorKind.Player,
            "MAGE",
            "Test Mage",
            "MANA",
            AutoAttack(),
            new HashSet<string>(StringComparer.Ordinal),
            4m,
            CanAutoAttack: false);
        var player = new CombatPlayerDefinition(AccountA, participant, ResolvedTalentModifiers.Empty);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player,
            20,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            hasCompanion: false);

        Assert.Equal(4m, entrant.Fighter.ResourceRegenPerSecond);
        Assert.False(entrant.Fighter.CanAutoAttack);
    }

    [Fact]
    public void ResourceRegenUsesAuthoritativeScalar()
    {
        ArenaCombatSession session = Create(currentResource: 10, regenPerSecond: 4, canAutoAttack: false);

        session.AdvanceTo(Start.AddSeconds(3));

        Assert.Equal(22m, session.Snapshot.ActorA.CurrentResource);
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.ResourceChanged
            && combatEvent.ActorId == ActorA
            && combatEvent.DefinitionId == "COMBAT_REGEN"
            && combatEvent.Amount == 12m);
    }

    [Fact]
    public void ResourceRegenCapsAtMaxResource()
    {
        ArenaCombatSession session = Create(currentResource: 95, regenPerSecond: 4, canAutoAttack: false);

        session.AdvanceTo(Start.AddSeconds(10));

        Assert.Equal(100m, session.Snapshot.ActorA.CurrentResource);
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.ResourceChanged
            && combatEvent.ActorId == ActorA
            && combatEvent.Amount == 5m);
    }

    [Fact]
    public void ZeroResourceRegenDoesNothing()
    {
        ArenaCombatSession session = Create(currentResource: 10, regenPerSecond: 0, canAutoAttack: false);

        session.AdvanceTo(Start.AddSeconds(10));

        Assert.Equal(10m, session.Snapshot.ActorA.CurrentResource);
        Assert.DoesNotContain(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.ResourceChanged
            && combatEvent.ActorId == ActorA
            && combatEvent.DefinitionId == "COMBAT_REGEN");
    }

    [Fact]
    public void SequentialAdvanceToRegeneratesOnlyNewElapsedTime()
    {
        ArenaCombatSession session = Create(currentResource: 10, regenPerSecond: 4, canAutoAttack: false);

        session.AdvanceTo(Start.AddSeconds(2));
        Assert.Equal(18m, session.Snapshot.ActorA.CurrentResource);

        session.AdvanceTo(Start.AddSeconds(5));
        Assert.Equal(30m, session.Snapshot.ActorA.CurrentResource);
    }

    [Fact]
    public void RepeatedAdvanceToSameTimestampDoesNotDoubleRegenerate()
    {
        ArenaCombatSession session = Create(currentResource: 10, regenPerSecond: 4, canAutoAttack: false);
        DateTimeOffset now = Start.AddSeconds(2);

        session.AdvanceTo(now);
        long sequence = session.Snapshot.Sequence;
        session.AdvanceTo(now);

        Assert.Equal(18m, session.Snapshot.ActorA.CurrentResource);
        Assert.Equal(sequence, session.Snapshot.Sequence);
    }

    [Fact]
    public void CanAutoAttackFalseDealsNoAutoAttackDamage()
    {
        ArenaCombatSession session = Create(currentResource: 100, regenPerSecond: 0, canAutoAttack: false);

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.Equal(500m, session.Snapshot.ActorB.CurrentHp);
        Assert.DoesNotContain(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.SourceActorId == ActorA);
    }

    [Fact]
    public void CanAutoAttackTruePreservesAutoAttackDamage()
    {
        ArenaCombatSession session = Create(currentResource: 100, regenPerSecond: 0, canAutoAttack: true);

        session.AdvanceTo(Start.AddSeconds(2));

        Assert.True(session.Snapshot.ActorB.CurrentHp < 500m);
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.SourceActorId == ActorA
            && combatEvent.TargetActorId == ActorB);
    }

    [Fact]
    public void CanAutoAttackFalseDoesNotDispatchPhantomAutoAttackTalentProc()
    {
        ArenaCombatSession session = Create(
            currentResource: 100,
            regenPerSecond: 0,
            canAutoAttack: false,
            talents: Talents(HawkSpiritHook()));

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.DoesNotContain(session.ActiveEffectsFor(AccountA), effect =>
            effect.Definition.Id == HawkEffectId);
        Assert.DoesNotContain(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.EffectApplied
            && combatEvent.DefinitionId == HawkEffectId);
    }

    private static ArenaCombatSession Create(
        decimal currentResource,
        decimal regenPerSecond,
        bool canAutoAttack,
        ResolvedTalentModifiers? talents = null)
    {
        var source = new ArenaFighter(
            AccountA,
            ActorA,
            Actor(ActorA, currentResource),
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            AutoAttack(),
            talents ?? ResolvedTalentModifiers.Empty,
            regenPerSecond,
            canAutoAttack);
        var target = new ArenaFighter(
            AccountB,
            ActorB,
            Actor(ActorB, 100),
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            AutoAttack(),
            ResolvedTalentModifiers.Empty,
            0,
            false);
        return new ArenaCombatSession(
            Guid.NewGuid(),
            source,
            target,
            new SeededGameRandom(42),
            Start);
    }

    private static CombatActorState Actor(Guid actorId, decimal currentResource) => new(
        actorId,
        500,
        500,
        100,
        currentResource,
        new CombatStats(
            20,
            100,
            0,
            0,
            1,
            0,
            0,
            0,
            0));

    private static AutoAttackProfile AutoAttack() =>
        new(TimeSpan.FromSeconds(2), 20, 0, 0);

    private static ResolvedTalentEventHook HawkSpiritHook() => new(
        ArenaTalentEventDispatcher.ArcherHawkSpiritTalentId,
        TalentModifierKeys.OnAutoAttack,
        1,
        10,
        ArenaTalentEventDispatcher.HawkSpiritTargetId,
        TimeSpan.Zero,
        false,
        ChancePercent: 100,
        Duration: TimeSpan.FromSeconds(6));

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks,
        []);
}
