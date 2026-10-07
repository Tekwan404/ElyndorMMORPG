using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.ItemEffects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Combat;

public sealed class ItemSpecialEffectIntegrationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly AbilityDefinition Strike = new("TEST_STRIKE", AbilityType.Instant,
        AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero, false,
        GlobalCooldownCategory.None, false, "PHYSICAL",
        Actions: [new(AbilityActionType.Damage, 100, DamageType.True, CanMiss: false, CanDodge: false, CanCrit: false)]);

    [Fact]
    public void EveryNthAndCooldownAreIsolatedPerOwnerAndReplayIsIgnored()
    {
        var effect = Effect() with { Conditions = new(2, TimeSpan.FromSeconds(5)) };
        var runtime = new ItemSpecialEffectRuntime([effect]);
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        var loadout = new Dictionary<Guid, IReadOnlySet<string>> { [first] = new HashSet<string> { effect.Id }, [second] = new HashSet<string> { effect.Id } };
        CombatEvent Hit(Guid owner, long sequence, int seconds) => new(CombatEventType.DamageDealt,
            Now.AddSeconds(seconds), owner, Strike.Id, 100, Sequence: sequence, SourceActorId: owner, TargetActorId: Guid.NewGuid());
        var hit = Hit(first, 1, 0);
        Assert.Empty(runtime.Evaluate(hit, loadout));
        Assert.Empty(runtime.Evaluate(hit, loadout));
        Assert.Empty(runtime.Evaluate(Hit(second, 2, 0), loadout));
        Assert.Single(runtime.Evaluate(Hit(first, 3, 0), loadout));
        Assert.Empty(runtime.Evaluate(Hit(first, 4, 1), loadout));
        Assert.Single(runtime.Evaluate(Hit(second, 5, 1), loadout));
        Assert.Empty(runtime.Evaluate(Hit(first, 6, 5), loadout));
        Assert.Single(runtime.Evaluate(Hit(first, 7, 5), loadout));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void SecondaryOriginsCannotTriggerItemEffects(bool periodic, bool proc, bool reflected)
    {
        var runtime = new ItemSpecialEffectRuntime([Effect()]);
        Guid owner = Guid.NewGuid();
        var input = new CombatEvent(CombatEventType.DamageDealt, Now, owner, Strike.Id, 100,
            SourceActorId: owner, TargetActorId: Guid.NewGuid(), IsPeriodic: periodic, IsProc: proc, IsReflected: reflected);
        Assert.Empty(runtime.Evaluate(input, new Dictionary<Guid, IReadOnlySet<string>> { [owner] = new HashSet<string> { "TEST_UNIQUE" } }));
    }

    [Fact]
    public void SoloItemDamageUsesProcOriginAndCannotTriggerAnotherItem()
    {
        var owner = Participant(true, true);
        var enemy = Participant(false, false);
        var session = new CombatSession(Guid.NewGuid(), owner, enemy,
            new Dictionary<string, AbilityDefinition> { [Strike.Id] = Strike },
            new MonsterAiProfile("TEST", []), ResolvedTalentModifiers.Empty, Random(), Now,
            itemSpecialEffects: [Effect()]);
        var result = session.Handle(new UseAbilityCommand("strike", Strike.Id, enemy.Actor.ActorId), Now);
        Assert.True(result.Succeeded, result.ErrorCode);
        var damage = Assert.Single(result.Events, e => e.Type == CombatEventType.DamageDealt && e.DefinitionId == "TEST_UNIQUE");
        Assert.Equal(10, damage.Amount);
        Assert.True(damage.IsProc);
        Assert.Equal(890, enemy.Actor.CurrentHp);
    }

    [Fact]
    public void HostedArenaUsesCapturedItemDefinitionsForOnlyTheirOwner()
    {
        var owner = Participant(true, true);
        var opponent = Participant(true, false);
        var first = Fighter(owner);
        var second = Fighter(opponent);
        var session = new ArenaCombatSession(Guid.NewGuid(), first, second, Random(), Now);
        var result = session.UseAbility(first.AccountId, "strike", Strike.Id, opponent.Actor.ActorId, Now);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Single(result.Events, e => e.Type == CombatEventType.DamageDealt && e.DefinitionId == "TEST_UNIQUE");
        Assert.Equal(890, opponent.Actor.CurrentHp);
        var other = session.UseAbility(second.AccountId, "other", Strike.Id, owner.Actor.ActorId, Now);
        Assert.True(other.Succeeded, other.ErrorCode);
        Assert.DoesNotContain(other.Events, e => e.DefinitionId == "TEST_UNIQUE");
        Assert.Equal(900, owner.Actor.CurrentHp);
    }

    private static ItemSpecialEffectDefinition Effect() => new("TEST_UNIQUE",
        new(CombatEventType.DamageDealt, ItemSpecialEffectActorRole.Source), new(),
        [new(ItemSpecialEffectActionKind.TriggerDamage, ItemSpecialEffectTargetRole.EventTarget,
            Magnitude: 10, DamageType: DamageType.True)]);

    [Fact]
    public void ResourceProcClampsAndCooldownProcAffectsOnlyTheOwnersCooldown()
    {
        var owner = Participant(true, true);
        var enemy = Participant(false, false);
        var ability = Strike with { Cooldown = TimeSpan.FromSeconds(10) };
        var effect = Effect() with { Trigger = new(CombatEventType.AbilityCompleted, ItemSpecialEffectActorRole.Source),
            Actions = [new(ItemSpecialEffectActionKind.RestoreResource, Magnitude: 80),
                new(ItemSpecialEffectActionKind.ModifyCooldown, Magnitude: 3, AbilityId: Strike.Id)] };
        var session = new CombatSession(Guid.NewGuid(), owner with { ItemSpecialEffects = [effect] }, enemy,
            new Dictionary<string, AbilityDefinition> { [ability.Id] = ability },
            new MonsterAiProfile("TEST", []), ResolvedTalentModifiers.Empty, Random(), Now);
        Assert.True(session.Handle(new UseAbilityCommand("one", ability.Id, enemy.Actor.ActorId), Now).Succeeded);
        Assert.Equal(100, owner.Actor.CurrentResource);
        Assert.False(session.Handle(new UseAbilityCommand("early", ability.Id, enemy.Actor.ActorId), Now.AddSeconds(6)).Succeeded);
        Assert.True(session.Handle(new UseAbilityCommand("ready", ability.Id, enemy.Actor.ActorId), Now.AddSeconds(7)).Succeeded);
    }

    [Fact]
    public void ValidationReportsMissingReferencesAndMalformedEffectsWithoutThrowing()
    {
        var item = new ItemDefinition("TEST", "Test", ItemType.Equipment, ItemRarity.Unique, 60,
            false, 1, EquipmentSlot.Ring1, new(0, 0, 0, 0), "Test", SpecialEffectIds: ["MISSING"]);
        var malformed = Effect() with { Actions = null! };
        var package = new GameContentPackage("0.1.0", "0.1.0", Now, [], [], Items: [item], ItemSpecialEffects: [malformed]);
        var errors = ContentValidationPipeline.Default.Validate(package);
        Assert.Contains(errors, e => e.Code == "INVALID_ITEM_SPECIAL_EFFECT");
        Assert.Contains(errors, e => e.Code == "MISSING_ITEM_SPECIAL_EFFECT_REFERENCE");
        Assert.Throws<ArgumentException>(() => new ItemSpecialEffectEvaluator([Effect() with
            { Trigger = new(CombatEventType.DamageDealt, (ItemSpecialEffectActorRole)99) }]));
    }

    private static CombatParticipantDefinition Participant(bool player, bool equipped) => new(
        new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 50,
            CombatStats.Default with { Dodge = 0, CriticalChance = 0, Armor = 0 }),
        player ? CombatActorKind.Player : CombatActorKind.Monster, player ? "WARRIOR" : "TEST", "Test", "RAGE",
        new(TimeSpan.FromHours(1), 0, 0, 0), new HashSet<string> { Strike.Id }, CanAutoAttack: false,
        EquippedSpecialEffectIds: equipped ? new HashSet<string> { "TEST_UNIQUE" } : null,
        ItemSpecialEffects: [Effect()]);

    private static ArenaFighter Fighter(CombatParticipantDefinition participant)
    {
        Guid account = Guid.NewGuid();
        return new(account, Guid.NewGuid(), participant.Actor,
            new Dictionary<string, AbilityDefinition> { [Strike.Id] = Strike }, participant.AutoAttack,
            CanAutoAttack: false, PlayerDefinition: new(account, participant, ResolvedTalentModifiers.Empty));
    }

    private static SequenceGameRandom Random() => new(Enumerable.Repeat(.99m, 100).ToArray());
}
