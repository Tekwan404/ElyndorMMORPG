using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaTriggeredAbilityActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TriggeredDamageUsesProductionAbilityResolutionWithoutSpendingResourceOrStartingCooldown()
    {
        CombatActorState source = Actor(1000);
        CombatActorState target = Actor(1000);
        var runtime = new CombatRuntimeState(source);
        runtime.AddActor(target);
        var action = new AbilityActionDefinition(AbilityActionType.Damage, 100,
            DamageType.True, CanMiss: false, CanCrit: false, CanDodge: false);
        var ability = new AbilityDefinition("TALENT_PROC", AbilityType.Instant,
            AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero, false,
            GlobalCooldownCategory.None, false, "PHYSICAL", Actions: [action]);

        IReadOnlyList<CombatEvent> events = AbilityEngine.ResolveTriggeredAction(
            runtime, ability, action, target.ActorId, Now, new SeededGameRandom(42));

        Assert.Equal(900m, target.CurrentHp);
        Assert.Equal(100m, source.CurrentResource);
        Assert.Empty(runtime.Cooldowns);
        Assert.Contains(events, item => item.Type == CombatEventType.DamageDealt
            && item.SourceActorId == source.ActorId
            && item.TargetActorId == target.ActorId);
    }

    private static CombatActorState Actor(decimal hp) => new(Guid.NewGuid(), hp, hp,
        100, 100, new CombatStats(30, 100, 0, 0, 1, 0, 0, 0, 0));
}
