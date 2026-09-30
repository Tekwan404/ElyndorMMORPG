using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaCharacterStatsParityTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ArenaEntryRetainsCapturedCharacterStatsVitalsAndCombatModifiers()
    {
        var stats = new CombatStats(30, 83, 12, 17, 1.5m, 240, 155,
            0.2m, 0.15m, 175, 210, 25, 9, 16);
        var combatModifiers = new TalentCombatModifiers(DamageDealtPercent: 6);
        CombatActorState actor = new(Guid.NewGuid(), 1200, 917, 320, 74,
            stats, combatModifiers);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "PALADIN", "Tester", "MANA", new AutoAttackProfile(TimeSpan.FromSeconds(2), 10, 10, 0),
            new HashSet<string>(StringComparer.Ordinal), 3m);
        var player = new CombatPlayerDefinition(Guid.NewGuid(), participant, ResolvedTalentModifiers.Empty);

        ArenaFighter fighter = ArenaFighterAssembler.Create(player, 30,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal), false).Fighter;

        Assert.Same(actor, fighter.Actor);
        Assert.Same(stats, fighter.Actor.Stats);
        Assert.Same(combatModifiers, fighter.Actor.TalentModifiers);
        Assert.Equal(917, fighter.Actor.CurrentHp);
        Assert.Equal(74, fighter.Actor.CurrentResource);
        Assert.Equal(3m, fighter.ResourceRegenPerSecond);
    }

    [Fact]
    public void ArenaFighterDamageUsesSameCharacterArmorAndPenetrationAsCombatPipeline()
    {
        CombatActorState attacker = Actor(armor: 0, penetration: 0.25m);
        CombatActorState defender = Actor(armor: 300, penetration: 0);
        ArenaFighter source = Assemble(attacker);
        ArenaFighter target = Assemble(defender);

        DamageRequest arenaRequest = new(source.Actor, target.Actor, 200, DamageType.Physical,
            CanMiss: false, CanDodge: false, CanCrit: false, CanBlock: false);
        DamageResult actual = DamagePipeline.Resolve(arenaRequest, new SeededGameRandom(17), Now);

        CombatActorState controlAttacker = Actor(armor: 0, penetration: 0.25m);
        CombatActorState controlDefender = Actor(armor: 300, penetration: 0);
        DamageResult expected = DamagePipeline.Resolve(
            arenaRequest with { Source = controlAttacker, Target = controlDefender },
            new SeededGameRandom(17), Now);

        Assert.Equal(expected.HpDamage, actual.HpDamage);
        Assert.Equal(expected.AfterMitigation, actual.AfterMitigation);
        Assert.Equal(expected.ResultingHp, actual.ResultingHp);
        Assert.InRange(actual.HpDamage, 1, 199);
    }

    private static ArenaFighter Assemble(CombatActorState actor)
    {
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "WARRIOR", "Tester", "RAGE", new AutoAttackProfile(TimeSpan.FromSeconds(2), 10, 10, 0),
            new HashSet<string>(StringComparer.Ordinal), 0m);
        return ArenaFighterAssembler.Create(
            new CombatPlayerDefinition(Guid.NewGuid(), participant, ResolvedTalentModifiers.Empty),
            30, new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal), false).Fighter;
    }

    private static CombatActorState Actor(decimal armor, decimal penetration) => new(
        Guid.NewGuid(), 1000, 1000, 100, 100,
        new CombatStats(30, 100, 0, 0, 1, armor, 0, penetration, 0));
}
