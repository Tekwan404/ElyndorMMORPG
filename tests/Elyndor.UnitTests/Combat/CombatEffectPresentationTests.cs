using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatEffectPresentationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void RuntimeCombustionHasAHumanNameDescriptionAndSpellIcon()
    {
        CombatEffectSnapshot snapshot = Project(new("MAGE_COMBUSTION_ACTIVE", EffectKind.Buff,
            TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 0));
        Assert.Equal("Возгорание", snapshot.DisplayName);
        Assert.Contains("крит", snapshot.Description);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.IconId));
    }

    [Theory]
    [InlineData(EffectKind.DamageOverTime, "урона")]
    [InlineData(EffectKind.HealingOverTime, "здоровья")]
    public void PeriodicDescriptionUsesActualTickMagnitudeAndInterval(EffectKind kind, string noun)
    {
        CombatEffectSnapshot snapshot = Project(new("TEST_PERIODIC", kind,
            TimeSpan.FromSeconds(12), 3, EffectStackPolicy.Stack, 25, TimeSpan.FromSeconds(2)));
        Assert.Contains("25", snapshot.Description);
        Assert.Contains(noun, snapshot.Description);
        Assert.Contains("2", snapshot.Description);
        Assert.DoesNotContain("TEST", snapshot.DisplayName);
    }

    [Fact]
    public void MultiplierDescriptionReportsReductionRatherThanRawMultiplier()
    {
        CombatEffectSnapshot snapshot = Project(new("TEST_GUARD", EffectKind.StatModifier,
            TimeSpan.FromSeconds(5), 1, EffectStackPolicy.Replace, .8m,
            ModifiedStat: EffectStat.IncomingDamageMultiplier, ModifierMode: EffectModifierMode.Multiplicative));
        Assert.Contains("20%", snapshot.Description);
        Assert.Contains("Получаем", snapshot.Description);
    }

    [Fact]
    public void ExplicitMetadataIsPreserved()
    {
        CombatEffectSnapshot snapshot = Project(new("CUSTOM", EffectKind.Buff,
            TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 0,
            DisplayName: "Печать испытания", Description: "Победите отражение до окончания эффекта.", IconId: "test-icon"));
        Assert.Equal("Печать испытания", snapshot.DisplayName);
        Assert.Equal("Победите отражение до окончания эффекта.", snapshot.Description);
        Assert.Equal("test-icon", snapshot.IconId);
    }

    [Theory]
    [InlineData(EffectStat.IncomingDamageMultiplier, .8, "Beneficial")]
    [InlineData(EffectStat.IncomingDamageMultiplier, 1.2, "Harmful")]
    [InlineData(EffectStat.AttackSpeed, .8, "Harmful")]
    [InlineData(EffectStat.AttackSpeed, 1.2, "Beneficial")]
    public void DispositionDependsOnTheStatAndItsActualDirection(EffectStat stat, decimal multiplier, string expected)
    {
        var snapshot = Project(new("DIRECTION", EffectKind.StatModifier, TimeSpan.FromSeconds(5), 1,
            EffectStackPolicy.Replace, multiplier, ModifiedStat: stat, ModifierMode: EffectModifierMode.Multiplicative));
        Assert.Equal(expected, snapshot.Disposition);
        Assert.NotNull(snapshot.InstanceId);
        Assert.NotNull(snapshot.SourceActorId);
        Assert.Equal(Now, snapshot.AppliedAtUtc);
    }

    [Fact]
    public void StackPercentUsesTheSameFractionUnitsAsTheEffectEngine()
    {
        var snapshot = Project(new("PERCENT", EffectKind.StatModifier, TimeSpan.FromSeconds(5), 1,
            EffectStackPolicy.Replace, .15m, ModifiedStat: EffectStat.Armor, ModifierMode: EffectModifierMode.Percent));
        Assert.Contains("+15%", snapshot.Description);
    }

    [Theory]
    [InlineData("MAGE_CLEARCASTING", "не расходует ману")]
    [InlineData("MAGE_HOT_STREAK", "на 50% меньше маны")]
    [InlineData("MAGE_ARCANE_POWER_FREE_COST", "Базовая стоимость маны сохраняется")]
    [InlineData("MAGE_DEEP_CHILL", "Не оглушает")]
    public void RuntimeMarkersExplainTheirDifferentMechanics(string id, string detail)
    {
        var snapshot = Project(new(id, EffectKind.Buff, TimeSpan.FromSeconds(10), 2,
            EffectStackPolicy.Stack, 0));
        Assert.Contains(detail, snapshot.Description);
    }

    [Fact]
    public void ShieldInspectionReportsRemainingAbsorptionAndItsOwner()
    {
        var actor = CombatActorState.CreateDummy(1000);
        Guid owner = Guid.NewGuid();
        EffectEngine.Apply(actor, owner, new("TEST_SHIELD", EffectKind.Shield,
            TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 100, Description: "Щит небесной защиты."), Now);
        Elyndor.Core.Combat.Damage.DamagePipeline.Resolve(new(actor, actor, 25,
            Elyndor.Core.Combat.Damage.DamageType.True, CanMiss: false, CanCrit: false, CanDodge: false),
            new SeededGameRandom(1), Now);
        var active = Assert.Single(actor.ActiveEffects);
        var snapshot = CombatEffectSnapshot.From(active);
        Assert.Contains("75", snapshot.Description);
        Assert.Equal(owner, snapshot.SourceActorId);
        Assert.Equal(active.InstanceId, snapshot.InstanceId);
        Assert.Equal("Shield", snapshot.Kind);
    }

    [Fact]
    public void BookkeepingMarkersAreHiddenButRealControlImmunityRemainsVisible()
    {
        var actor = CombatActorState.CreateDummy(1000);
        foreach (string id in new[] { "MAGE_FROST_EXTENDED", "PALADIN_CLEANSE_CAST" })
        {
            EffectEngine.Apply(actor, actor.ActorId, new(id, EffectKind.Buff,
                TimeSpan.FromSeconds(1), 1, EffectStackPolicy.Replace, 1), Now);
            Assert.False(CombatEffectPresentation.IsVisible(actor.ActiveEffects.Single(e => e.Definition.Id == id)));
        }
        EffectEngine.Apply(actor, actor.ActorId, new("WARLORD_VICTORY_CONTROL_IMMUNITY", EffectKind.Buff,
            TimeSpan.FromSeconds(5), 1, EffectStackPolicy.Replace, 1, ControlImmunities: [EffectKind.Fear]), Now);
        var effect = actor.ActiveEffects.Single(e => e.Definition.Id == "WARLORD_VICTORY_CONTROL_IMMUNITY");
        Assert.True(CombatEffectPresentation.IsVisible(effect));
        Assert.Contains("Страх", CombatEffectSnapshot.From(effect).Description);
    }

    [Fact]
    public void EventDrivenBuffExplainsTheActualTriggerChanceAndDamage()
    {
        var snapshot = Project(new("WARLORD_BATTLE_STANDARD", EffectKind.Buff,
            TimeSpan.FromSeconds(15), 1, EffectStackPolicy.Replace, 1, EventActions:
            [new(CombatEventType.DamageDealt, new(AbilityActionType.Damage), "AUTO_ATTACK",
                ChancePercent: 10, EventAmountPercent: 25, UseBaseDamage: true)]));
        Assert.Contains("Автоатаки", snapshot.Description);
        Assert.Contains("10%", snapshot.Description);
        Assert.Contains("25% базового", snapshot.Description);
    }

    private static CombatEffectSnapshot Project(EffectDefinition effect)
    {
        CombatActorState actor = new(Guid.NewGuid(), 1000, 1000, 100, 100, CombatStats.Default);
        CombatActorState enemy = new(Guid.NewGuid(), 1000, 1000, 0, 0, CombatStats.Default);
        CombatParticipantDefinition Participant(CombatActorState state, CombatActorKind kind) => new(state,
            kind, "TEST", "Test", "MANA", new(TimeSpan.FromHours(1), 0, 0, 0), new HashSet<string>(), CanAutoAttack: false);
        CombatSession session = new(Guid.NewGuid(), Participant(actor, CombatActorKind.Player),
            Participant(enemy, CombatActorKind.Monster), new Dictionary<string, AbilityDefinition>(),
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty, new SeededGameRandom(1), Now);
        EffectEngine.Apply(actor, actor.ActorId, effect, Now);
        return Assert.Single(session.Snapshot(actor.ActorId).Player.Effects);
    }
}
