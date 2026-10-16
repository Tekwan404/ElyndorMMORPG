using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Resources;
using Elyndor.Infrastructure.Content;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinSupportEffectTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task WisdomDoublesRegenOnlyDuringItsLifetimeEvenAcrossALateTick()
    {
        EffectDefinition effect = await LoadEffect("BLESSING_OF_WISDOM");
        CombatActorState actor = CombatActorState.CreateDummy(100, maxResource: 1000);
        actor.TrySpendResource(1000);
        EffectEngine.Apply(actor, actor.ActorId, effect, Now.AddSeconds(2));
        var resources = new CombatResourceRuntime(_ => { });
        resources.Regenerate(actor, Now, Now.AddSeconds(15), _ => 3, baseManaRegenPerSecond: 3);
        // 5 seconds normal + 10 seconds doubled, including application/expiry boundaries.
        Assert.Equal(75, actor.CurrentResource);
    }

    [Fact]
    public async Task ReplenishmentReducesHealingByTenPercentAndExpiresAfterEightSeconds()
    {
        EffectDefinition effect = await LoadEffect("DIVINE_REPLENISHMENT");
        CombatActorState actor = CombatActorState.CreateDummy(1000);
        CombatActorState target = CombatActorState.CreateDummy(1000);
        EffectEngine.Apply(actor, actor.ActorId, effect, Now);
        decimal Heal(DateTimeOffset at)
        {
            target.SetCurrentHp(1);
            return HealingPipeline.Resolve(new HealingRequest(target, 100,
                OccurredAtUtc: at, Source: actor, CanCrit: false)).Events
                .Single(e => e.Type == CombatEventType.HealingApplied).Amount;
        }
        Assert.Equal(90, Heal(Now.AddSeconds(7)));
        Assert.Equal(100, Heal(Now.AddSeconds(8)));
    }

    [Fact]
    public async Task ConcentrationShortensSpellsButDoesNotSpeedUpPhysicalCastsOrChannels()
    {
        EffectDefinition effect = await LoadEffect("CONCENTRATION_AURA");
        foreach (var (type, spell, duration) in new[]
        {
            (AbilityType.Casted, true, 1.84),
            (AbilityType.Casted, false, 2.0),
            (AbilityType.Channelled, true, 2.0)
        })
        {
            CombatActorState actor = CombatActorState.CreateDummy(100, maxResource: 100);
            EffectEngine.Apply(actor, actor.ActorId, effect, Now);
            CombatRuntimeState runtime = new(actor);
            AbilityDefinition ability = new("TEST", type, AbilityTargetType.Self, 0,
                TimeSpan.Zero, TimeSpan.FromSeconds(2), false, GlobalCooldownCategory.None,
                IsSpell: spell, School: "HOLY", Actions: [],
                ChannelTickInterval: type == AbilityType.Channelled ? TimeSpan.FromSeconds(1) : null);
            Assert.True(AbilityEngine.Execute(runtime, ability,
                new AbilityIntent("cast", ability.Id, actor.ActorId), Now).Succeeded);
            Assert.Equal(Now.AddSeconds(duration), runtime.ActiveCast!.ResolvesAtUtc);
        }
    }

    [Theory]
    [InlineData("HAMMER_OF_JUSTICE", false, EffectKind.Stun)]
    [InlineData("HAMMER_OF_JUSTICE", true, null)]
    [InlineData("REPENTANCE", false, EffectKind.Stun)]
    [InlineData("REPENTANCE", true, EffectKind.StatModifier)]
    public async Task ControlDistinguishesOrdinaryEnemiesAndBosses(string id, bool boss, EffectKind? kind)
    {
        var abilities = await LoadAbilities();
        var (session, paladin, _, enemy) = Party(abilities, boss);
        Assert.True(session.Handle(paladin.ActorId, new UseAbilityCommand("control", id, enemy.ActorId), Now).Succeeded);
        if (kind is null) Assert.Empty(enemy.ActiveEffects);
        else
        {
            var effect = Assert.Single(enemy.ActiveEffects);
            Assert.Equal(kind, effect.Definition.Kind);
            Assert.Equal(Now.AddSeconds(id == "REPENTANCE" ? 4 : 3), effect.ExpiresAtUtc);
            if (boss) Assert.Equal(.9m, EffectEngine.CalculateStat(enemy, EffectStat.OutgoingDamageMultiplier, 1, Now));
        }
    }

    [Fact]
    public async Task TwoConcentrationAurasDoNotStackAndKeepTheRemainingCasterWhenOneSwitches()
    {
        var abilities = await LoadAbilities();
        var (session, first, second, enemy) = Party(abilities, false);
        Assert.True(session.Handle(first.ActorId, new UseAbilityCommand("aura-1", "CONCENTRATION_AURA", first.ActorId), Now).Succeeded);
        Assert.True(session.Handle(second.ActorId, new UseAbilityCommand("aura-2", "CONCENTRATION_AURA", second.ActorId), Now).Succeeded);
        Assert.Equal(.92m, EffectEngine.CalculateStat(second, EffectStat.CastTimeMultiplier, 1, Now));
        Assert.Empty(enemy.ActiveEffects);
        Assert.True(session.Handle(first.ActorId, new UseAbilityCommand("switch-1", "DEVOTION_AURA", first.ActorId), Now.AddSeconds(1)).Succeeded);
        Assert.Equal(.92m, EffectEngine.CalculateStat(second, EffectStat.CastTimeMultiplier, 1, Now.AddSeconds(1)));
        Assert.True(session.Handle(second.ActorId, new UseAbilityCommand("switch-2", "DEVOTION_AURA", second.ActorId), Now.AddSeconds(1)).Succeeded);
        Assert.Equal(1, EffectEngine.CalculateStat(second, EffectStat.CastTimeMultiplier, 1, Now.AddSeconds(1)));
    }

    [Theory]
    [InlineData("MANA", 80)]
    [InlineData("FOCUS", 50)]
    public async Task WisdomAddsOnlyBaseManaRegenAndNeverChangesFocus(string resource, decimal expected)
    {
        var abilities = await LoadAbilities();
        var (session, paladin, ally, _) = Party(abilities, false, resource);
        Assert.True(session.Handle(paladin.ActorId, new UseAbilityCommand("wisdom", "BLESSING_OF_WISDOM", ally.ActorId), Now).Succeeded);
        session.AdvanceTo(Now.AddSeconds(10));
        Assert.Equal(expected, ally.CurrentResource);
    }

    [Fact]
    public async Task WisdomDoesNotMultiplyOtherManaBonuses()
    {
        CombatActorState actor = CombatActorState.CreateDummy(100, maxResource: 1000);
        actor.TrySpendResource(1000);
        EffectEngine.Apply(actor, actor.ActorId, await LoadEffect("BLESSING_OF_WISDOM"), Now);
        new CombatResourceRuntime(_ => { }).Regenerate(actor, Now, Now.AddSeconds(10),
            _ => 4, baseManaRegenPerSecond: 3);
        Assert.Equal(70, actor.CurrentResource); // (3 base + 1 existing bonus + 3 Wisdom) * 10.
    }

    [Fact]
    public async Task ProductionArenaAppliesWisdomAndControlThroughTheSharedClassRuntime()
    {
        var content = await LoadAbilities();
        ArenaFighter Fighter(string classId, IReadOnlyDictionary<string, AbilityDefinition> abilities)
        {
            var actor = new CombatActorState(Guid.NewGuid(), 1000, 1000, 1000, 100, CombatStats.Default);
            var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
            var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player, classId, classId,
                "MANA", auto, abilities.Keys.ToHashSet(), ResourceRegenPerSecond: 3, CanAutoAttack: false);
            var player = new CombatPlayerDefinition(Guid.NewGuid(), participant, ResolvedTalentModifiers.Empty);
            return new(player.AccountId, actor.ActorId, actor, abilities, auto,
                ResourceRegenPerSecond: 3, CanAutoAttack: false, PlayerDefinition: player, BaseAbilities: abilities);
        }
        var abilities = new Dictionary<string, AbilityDefinition>
        {
            ["BLESSING_OF_WISDOM"] = content["BLESSING_OF_WISDOM"],
            ["REPENTANCE"] = content["REPENTANCE"],
            ["CONCENTRATION_AURA"] = content["CONCENTRATION_AURA"]
        };
        var paladin = Fighter("PALADIN", abilities);
        var enemy = Fighter("MAGE", new Dictionary<string, AbilityDefinition>());
        var session = new ArenaCombatSession(Guid.NewGuid(), paladin, enemy, new SeededGameRandom(1), Now);
        Assert.True(session.UseAbility(paladin.AccountId, "wisdom", "BLESSING_OF_WISDOM", paladin.Actor.ActorId, Now).Succeeded);
        Assert.True(session.UseAbility(paladin.AccountId, "aura", "CONCENTRATION_AURA", paladin.Actor.ActorId, Now.AddSeconds(1)).Succeeded);
        Assert.Equal(.92m, EffectEngine.CalculateStat(paladin.Actor, EffectStat.CastTimeMultiplier, 1, Now.AddSeconds(1)));
        Assert.Equal(1, EffectEngine.CalculateStat(enemy.Actor, EffectStat.CastTimeMultiplier, 1, Now.AddSeconds(1)));
        Assert.True(session.UseAbility(paladin.AccountId, "control", "REPENTANCE", enemy.Actor.ActorId, Now.AddSeconds(2)).Succeeded);
        Assert.Contains(enemy.Actor.ActiveEffects, effect => effect.Definition.Kind == EffectKind.Stun);
        session.AdvanceTo(Now.AddSeconds(10));
        Assert.Equal(100 - content["BLESSING_OF_WISDOM"].ResourceCost - content["REPENTANCE"].ResourceCost + 60,
            paladin.Actor.CurrentResource);
        Assert.Equal(130, enemy.Actor.CurrentResource);
    }

    private static (CombatSession, CombatActorState, CombatActorState, CombatActorState) Party(
        IReadOnlyDictionary<string, AbilityDefinition> abilities, bool boss, string allyResource = "MANA")
    {
        CombatParticipantDefinition Participant(string id, CombatActorKind kind, string resource = "MANA") => new(
            CombatActorState.CreateDummy(10000, maxResource: 1000), kind, id, id, resource,
            new(TimeSpan.FromHours(1), 0, 0, 0), abilities.Keys.ToHashSet(),
            ResourceRegenPerSecond: 3, CanAutoAttack: false, MonsterRank: boss ? MonsterRank.Boss : MonsterRank.Normal);
        var player = Participant("PALADIN", CombatActorKind.Player);
        var ally = Participant("PALADIN", CombatActorKind.Player, allyResource);
        ally.Actor.TrySpendResource(980);
        var enemy = Participant("ENEMY", CombatActorKind.Monster);
        var session = new CombatSession(Guid.NewGuid(), player, enemy, abilities,
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty, new SeededGameRandom(1), Now,
            additionalPlayers: [new(Guid.NewGuid(), ally, ResolvedTalentModifiers.Empty)]);
        return (session, player.Actor, ally.Actor, enemy.Actor);
    }

    private static async Task<EffectDefinition> LoadEffect(string abilityId) =>
        (await LoadAbilities())[abilityId].Actions!.Single(action => action.Effect is not null).Effect!;

    private static async Task<IReadOnlyDictionary<string, AbilityDefinition>> LoadAbilities()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(path))
            {
                var package = await GameContentPackageLoader.LoadAsync(path);
                return package.Abilities!.ToDictionary(ability => ability.Id);
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository content was not found.");
    }
}

