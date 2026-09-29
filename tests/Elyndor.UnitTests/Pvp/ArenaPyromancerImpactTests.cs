using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaPyromancerImpactTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RealImpactTalentTurnsDamageOnlyFireballIntoStunThroughArenaDr()
    {
        Guid accountA = Guid.NewGuid();
        Guid accountB = Guid.NewGuid();
        Guid actorA = Guid.NewGuid();
        Guid actorB = Guid.NewGuid();
        var abilities = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
        {
            ["STUN_A"] = ControlAbility(),
            ["MAGE_FIREBALL"] = Fireball()
        };
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0);
        var firstActor = new CombatActorState(actorA, 100, 100, 100, 100, CombatStats.Default);
        var secondActor = new CombatActorState(actorB, 100, 100, 100, 100, CombatStats.Default);
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            new ArenaFighter(accountA, actorA, firstActor, abilities, auto, ImpactTalents()),
            new ArenaFighter(accountB, actorB, secondActor, abilities, auto),
            new SeededGameRandom(42),
            Start);

        ArenaCommandResult firstStun = session.UseAbility(
            accountA, "stun-1", "STUN_A", actorB, Start);
        Assert.True(firstStun.Succeeded);
        Assert.True(EffectEngine.HasControl(secondActor, EffectKind.Stun, Start.AddSeconds(3.9)));

        ArenaCommandResult fireballStart = session.UseAbility(
            accountA, "fireball-1", "MAGE_FIREBALL", actorB, Start.AddSeconds(4.1));
        Assert.True(fireballStart.Succeeded);
        session.AdvanceTo(Start.AddSeconds(5.1));

        ActiveEffect impact = Assert.Single(secondActor.ActiveEffects, effect =>
            effect.Definition.Id == PyromancerImpactRuntime.StunEffectId
            && effect.ExpiresAtUtc > Start.AddSeconds(5.1));
        Assert.Equal(EffectKind.Stun, impact.Definition.Kind);
        Assert.Equal(TimeSpan.FromSeconds(2), impact.Definition.Duration);
        Assert.Equal(Start.AddSeconds(7.1), impact.ExpiresAtUtc);
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.EffectApplied
            && combatEvent.DefinitionId == PyromancerImpactRuntime.StunEffectId);
    }

    private static ResolvedTalentModifiers ImpactTalents() => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        [
            new ResolvedTalentEventHook(
                PyromancerImpactRuntime.TalentId,
                TalentModifierKeys.OnAbilityUsed,
                3,
                100,
                null,
                TimeSpan.Zero,
                false,
                Duration: TimeSpan.FromSeconds(4))
        ],
        []);

    private static AbilityDefinition Fireball() => new(
        "MAGE_FIREBALL",
        AbilityType.Casted,
        AbilityTargetType.SingleEnemy,
        0,
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        false,
        GlobalCooldownCategory.None,
        true,
        "FIRE",
        Actions:
        [
            new AbilityActionDefinition(
                AbilityActionType.Damage,
                1,
                DamageType.Magical,
                CanMiss: false,
                CanCrit: false,
                CanDodge: false)
        ]);

    private static AbilityDefinition ControlAbility() => new(
        "STUN_A",
        AbilityType.Instant,
        AbilityTargetType.SingleEnemy,
        0,
        TimeSpan.Zero,
        TimeSpan.Zero,
        false,
        GlobalCooldownCategory.None,
        false,
        "Physical",
        Actions:
        [
            new AbilityActionDefinition(
                AbilityActionType.ApplyEffect,
                Effect: new EffectDefinition(
                    "STUN_A_EFFECT",
                    EffectKind.Stun,
                    TimeSpan.FromSeconds(4),
                    1,
                    EffectStackPolicy.Replace,
                    0))
        ]);
}
