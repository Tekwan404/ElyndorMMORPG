using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaAbilitySnapshotTests
{
    [Fact]
    public void SnapshotShowsFreeCastWithoutConsumingProcOrPublishingEvents()
    {
        DateTimeOffset now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var spell = new AbilityDefinition("MAGE_FIREBALL", AbilityType.Casted,
            AbilityTargetType.SingleEnemy, 20, TimeSpan.Zero, TimeSpan.FromSeconds(1),
            false, GlobalCooldownCategory.None, true, "FIRE",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10,
                DamageType.Magical, CanMiss: false, CanCrit: false, CanDodge: false)]);
        var abilities = new Dictionary<string, AbilityDefinition> { [spell.Id] = spell };
        ArenaFighter Fighter()
        {
            var actor = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 1, CombatStats.Default);
            var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player, "MAGE",
                "Mage", "MANA", new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
                new HashSet<string> { spell.Id }, CanAutoAttack: false);
            return ArenaFighterAssembler.Create(new CombatPlayerDefinition(Guid.NewGuid(), participant,
                ResolvedTalentModifiers.Empty), 1, abilities, false).Fighter;
        }
        ArenaFighter first = Fighter();
        ArenaFighter second = Fighter();
        var session = new ArenaCombatSession(Guid.NewGuid(), first, second, new SeededGameRandom(1), now);
        EffectEngine.Apply(first.Actor, first.Actor.ActorId, new EffectDefinition("MAGE_CLEARCASTING",
            EffectKind.Buff, TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 0), now);
        long sequence = session.Snapshot.Sequence;
        Assert.Equal(0m, Assert.Single(session.AbilitySnapshotsFor(first.AccountId)).ResourceCost);
        Assert.Equal(0m, Assert.Single(session.AbilitySnapshotsFor(first.AccountId)).ResourceCost);
        Assert.Equal(sequence, session.Snapshot.Sequence);
        Assert.Contains(first.Actor.ActiveEffects, effect => effect.Definition.Id == "MAGE_CLEARCASTING");
        Assert.True(session.UseAbility(first.AccountId, "free", spell.Id, second.Actor.ActorId, now).Succeeded);
        Assert.Equal(1m, first.Actor.CurrentResource);
        Assert.DoesNotContain(first.Actor.ActiveEffects, effect => effect.Definition.Id == "MAGE_CLEARCASTING");
        Assert.Equal(20m, Assert.Single(session.AbilitySnapshotsFor(first.AccountId)).ResourceCost);
        session.AdvanceTo(now.AddSeconds(1));
        Assert.True(second.Actor.CurrentHp < 1000m);
    }
}
