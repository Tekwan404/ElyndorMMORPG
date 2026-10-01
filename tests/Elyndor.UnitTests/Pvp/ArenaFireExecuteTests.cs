using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaFireExecuteTests
{
    [Theory]
    [InlineData(29, 1.12)]
    [InlineData(30, 1)]
    public void FireballExecuteUsesTargetHpAtImpact(decimal startingHpPercent, decimal multiplier)
    {
        var (plain, plainMage, plainTarget, now) = Duel(null, startingHpPercent);
        var (talented, mage, target, _) = Duel(Hook(), startingHpPercent);

        Assert.True(plain.UseAbility(plainMage.AccountId, "plain", "MAGE_FIREBALL", plainTarget.Actor.ActorId, now).Succeeded);
        Assert.True(talented.UseAbility(mage.AccountId, "talented", "MAGE_FIREBALL", target.Actor.ActorId, now).Succeeded);
        decimal beforePlain = plainTarget.Actor.CurrentHp;
        decimal beforeTalented = target.Actor.CurrentHp;
        plain.AdvanceTo(now + TimeSpan.FromSeconds(2));
        talented.AdvanceTo(now + TimeSpan.FromSeconds(2));

        decimal plainDamage = beforePlain - plainTarget.Actor.CurrentHp;
        Assert.True(plainDamage > 0);
        Assert.Equal(plainDamage * multiplier, beforeTalented - target.Actor.CurrentHp);
    }

    [Fact]
    public void CrossingExecuteThresholdBeforeCastResolvesAppliesTheExecuteBonus()
    {
        var (plain, plainMage, plainTarget, now) = Duel(null, 31);
        var (talented, mage, target, _) = Duel(Hook(), 31);
        Assert.True(plain.UseAbility(plainMage.AccountId, "plain", "MAGE_FIREBALL", plainTarget.Actor.ActorId, now).Succeeded);
        Assert.True(talented.UseAbility(mage.AccountId, "talented", "MAGE_FIREBALL", target.Actor.ActorId, now).Succeeded);
        plainTarget.Actor.ApplyDamage(200);
        target.Actor.ApplyDamage(200);
        decimal beforePlain = plainTarget.Actor.CurrentHp;
        decimal beforeTalented = target.Actor.CurrentHp;

        plain.AdvanceTo(now + TimeSpan.FromSeconds(2));
        talented.AdvanceTo(now + TimeSpan.FromSeconds(2));

        Assert.Equal((beforePlain - plainTarget.Actor.CurrentHp) * 1.12m,
            beforeTalented - target.Actor.CurrentHp);
    }

    private static (ArenaCombatSession Session, ArenaFighter Mage, ArenaFighter Target, DateTimeOffset Now)
        Duel(ResolvedTalentEventHook? hook, decimal targetHpPercent)
    {
        DateTimeOffset now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var source = new CombatActorState(Guid.NewGuid(), 10000, 10000, 100, 100, CombatStats.Default);
        var target = new CombatActorState(Guid.NewGuid(), 10000, 10000, 100, 100, CombatStats.Default);
        target.ApplyDamage(10000 * (1 - targetHpPercent / 100m));
        var spell = new AbilityDefinition("MAGE_FIREBALL", AbilityType.Casted,
            AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.FromSeconds(2),
            false, GlobalCooldownCategory.None, true, "FIRE",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 100,
                DamageType: DamageType.Magical)]);
        ResolvedTalentModifiers talents = hook is null ? ResolvedTalentModifiers.Empty : new(
            new TalentStatModifiers(), new TalentCombatModifiers(), new HashSet<string>(),
            new Dictionary<string, TalentAbilityModifiers>(), [hook], []);
        var abilities = new Dictionary<string, AbilityDefinition> { [spell.Id] = spell };
        ArenaFighter Fighter(CombatActorState actor, ResolvedTalentModifiers modifiers,
            IReadOnlySet<string> known) => ArenaFighterAssembler.Create(new CombatPlayerDefinition(
                Guid.NewGuid(), new CombatParticipantDefinition(actor, CombatActorKind.Player,
                    "MAGE", "Mage", "MANA", new AutoAttackProfile(TimeSpan.FromSeconds(3), 0, 0, 0),
                    known, CanAutoAttack: false), modifiers), 30, abilities, false).Fighter;
        ArenaFighter mage = Fighter(source, talents, new HashSet<string> { spell.Id });
        ArenaFighter enemy = Fighter(target, ResolvedTalentModifiers.Empty, new HashSet<string>());
        return (new ArenaCombatSession(Guid.NewGuid(), mage, enemy, new SeededGameRandom(42), now), mage, enemy, now);
    }

    private static ResolvedTalentEventHook Hook() => new("F-7-4",
        TalentModifierKeys.OnAbilityUsed, 2, 12, null, TimeSpan.Zero,
        false, Threshold: 30);
}
