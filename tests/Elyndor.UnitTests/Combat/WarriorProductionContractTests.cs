using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Combat;

public sealed class WarriorProductionContractTests
{
    private static readonly Task<GameContentPackage> Content = GameContentPackageLoader.LoadAsync(ContentPath());

    public static IEnumerable<object[]> AbilityModes()
    {
        string[] ids = ["STRIKE", "BATTLE_SHOUT", "HEAVY_BLOW", "LAST_STAND", "REVENGE", "SHIELD_BLOCK",
            "PROVOKE", "SUNDER_ARMOR", "CONCUSSION_BLOW", "SHIELD_BASH", "BASTION", "CHALLENGING_SHOUT",
            "SHIELD_SLAM", "WILD_STRIKE", "WHIRLWIND", "BERSERK", "BATTLE_CRY", "ENDURANCE_CRY",
            "WAR_BANNER", "CRY_OF_VENGEANCE", "VICTORY_FLAG", "RALLY_CRY", "BATTLE_STANDARD"];
        foreach (string id in ids)
            foreach (string mode in new[] { "solo", "party", "dungeon", "world-boss", "arena" })
                yield return [id, mode];
    }

    [Theory]
    [MemberData(nameof(AbilityModes))]
    public async Task ProductionAbilityExecutesWithSameResourceCooldownAndEffectContract(string id, string mode)
    {
        var content = await Content;
        var abilities = content.Abilities!.ToDictionary(a => a.Id);
        var ability = abilities[id];
        var owner = Actor(1000, 500);
        var enemy = Actor(10000, 10000);
        var known = new HashSet<string> { id };
        var talents = ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = known };
        var now = DateTimeOffset.UnixEpoch;
        if (id == "REVENGE")
            EffectEngine.Apply(owner, owner.ActorId, new("GUARDIAN_REVENGE_WINDOW", EffectKind.Buff,
                TimeSpan.FromSeconds(5), 1, EffectStackPolicy.Refresh, 1), now);
        var player = Participant(owner, CombatActorKind.Player, known);
        IReadOnlyList<CombatEvent> events;
        if (mode == "arena")
        {
            Guid account = Guid.NewGuid();
            var first = ArenaFighterAssembler.Create(new(account, player, talents), 60, abilities, false).Fighter;
            var second = ArenaFighterAssembler.Create(new(Guid.NewGuid(), Participant(enemy, CombatActorKind.Player, []),
                ResolvedTalentModifiers.Empty), 60, abilities, false).Fighter;
            var arena = new ArenaCombatSession(Guid.NewGuid(), first, second, Random(), now);
            Guid targetId = ability.TargetType is AbilityTargetType.Self or AbilityTargetType.SelfAndPartyMembersInCombat
                ? owner.ActorId : enemy.ActorId;
            var result = arena.UseAbility(account, "cast", id, targetId, now);
            Assert.True(result.Succeeded, result.ErrorCode);
            events = result.Events;
        }
        else
        {
            var opponent = Participant(enemy, CombatActorKind.Monster, []) with
            { MonsterRank = mode == "world-boss" ? MonsterRank.Boss : MonsterRank.Normal };
            var allies = mode is "party" or "dungeon"
                ? new[] { new CombatPlayerDefinition(Guid.NewGuid(), Participant(Actor(2000, 800), CombatActorKind.Player, []), ResolvedTalentModifiers.Empty) }
                : [];
            var session = new CombatSession(Guid.NewGuid(), player, opponent, abilities, new MonsterAiProfile("TEST", []),
                talents, Random(), now, additionalPlayers: allies);
            var result = session.Handle(owner.ActorId, new UseAbilityCommand("cast", id, enemy.ActorId), now);
            Assert.True(result.Succeeded, result.ErrorCode);
            events = result.Events;
            Assert.Equal(now + ability.Cooldown, result.Snapshot.Player.Cooldowns.GetValueOrDefault(id, now));
        }
        decimal generated = id == "STRIKE" ? 10 : id == "BATTLE_SHOUT" ? 20 : 0;
        Assert.Equal(Math.Min(100, 100 - ability.ResourceCost + generated), owner.CurrentResource);
        Assert.Contains(events, e => e.Type == CombatEventType.AbilityCompleted && e.DefinitionId == id && e.SourceActorId == owner.ActorId);
        foreach (var action in ability.Actions ?? [])
        {
            var target = ability.TargetType is AbilityTargetType.Self or AbilityTargetType.SelfAndPartyMembersInCombat ? owner : enemy;
            if (action.Type == AbilityActionType.Damage)
                Assert.Contains(events, e => e.Type == CombatEventType.DamageDealt && e.DefinitionId == id && e.Amount > 0 && e.TargetActorId == enemy.ActorId);
            if (action.Type == AbilityActionType.ApplyEffect)
                Assert.Contains(target.ActiveEffects, e => e.Definition.Id == action.Effect!.Id && e.ExpiresAtUtc == now + action.Effect.Duration);
        }
        if (id == "ENDURANCE_CRY") Assert.Equal(1060, owner.MaxHp);
        if (id == "RALLY_CRY") Assert.Equal(560, owner.CurrentHp);
        if (id == "LAST_STAND") { Assert.Equal(1200, owner.MaxHp); Assert.Equal(700, owner.CurrentHp); }
        if (id == "WAR_BANNER") Assert.Equal(4, EffectEngine.CalculateStat(owner, EffectStat.CriticalChance, 0, now));
        if (id == "VICTORY_FLAG") Assert.Equal(1.08m, EffectEngine.CalculateStat(owner, EffectStat.OutgoingDamageMultiplier, 1, now));
        if (id == "CONCUSSION_BLOW" && mode != "world-boss") Assert.True(EffectEngine.HasControl(enemy, EffectKind.Stun, now));
        decimal expectedDamage = id switch
        {
            "STRIKE" => 80, "HEAVY_BLOW" => 160, "REVENGE" => 85, "SHIELD_BASH" => 70,
            "SHIELD_SLAM" => 55, "CONCUSSION_BLOW" => 45, "WILD_STRIKE" => 135, "WHIRLWIND" => 70,
            _ => 0
        };
        Assert.Equal(10000 - expectedDamage, enemy.CurrentHp);
    }

    private static SequenceGameRandom Random() => new(Enumerable.Repeat(0.99m, 500).ToArray());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConditionalPartyMitigationCleansUpWhenItsOwnerDiesOrFlees(bool flee)
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 10);
        var ally = Actor(2000, 200);
        var enemy = Actor(10000, 10000);
        var now = DateTimeOffset.UnixEpoch;
        var talents = ResolvedTalentModifiers.Empty with
        { EventHooks = [new("W-7-4", TalentModifierKeys.OnPartyEvent, 3, 6, null, TimeSpan.Zero, false)] };
        var opponent = Participant(enemy, CombatActorKind.Monster, []) with
        { CanAutoAttack = true, AutoAttack = new(TimeSpan.FromSeconds(1), 100, 0, 0) };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["PROVOKE"]),
            opponent, abilities, new MonsterAiProfile("TEST", []), talents, Random(), now,
            additionalPlayers: [new(Guid.NewGuid(), Participant(ally, CombatActorKind.Player, []), ResolvedTalentModifiers.Empty)]);
        Assert.Equal(0.94m, EffectEngine.CalculateStat(ally, EffectStat.IncomingDamageMultiplier, 1, now));
        Assert.True(session.Handle(owner.ActorId, new UseAbilityCommand("taunt", "PROVOKE", enemy.ActorId), now).Succeeded);
        if (flee)
            Assert.True(session.Handle(owner.ActorId, new FleeCommand("leave"), now).Succeeded);
        else
        {
            session.AdvanceTo(now.AddSeconds(1));
            Assert.True(owner.IsDead);
        }
        Assert.DoesNotContain(ally.ActiveEffects, e => e.Definition.Id.StartsWith("WARLORD_STAND_TO_THE_END_", StringComparison.Ordinal));
        ally.SetCurrentHp(1000);
        session.AdvanceTo(now.AddSeconds(1.1));
        Assert.Equal(1, EffectEngine.CalculateStat(ally, EffectStat.IncomingDamageMultiplier, 1, now.AddSeconds(1.1)));
    }

    [Fact]
    public async Task LaterCryReplacesEarlierTalentUpgradeMagnitudeAndDuration()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var first = Actor(1000, 500);
        var second = Actor(1000, 500);
        var now = DateTimeOffset.UnixEpoch;
        ResolvedTalentModifiers Talents(int rank) => ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "BATTLE_CRY" },
            EventHooks = [new("W-2-4", TalentModifierKeys.OnPartyEvent, rank, rank, null, TimeSpan.Zero, false)]
        };
        var session = new CombatSession(Guid.NewGuid(), Participant(first, CombatActorKind.Player, ["BATTLE_CRY"]),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []),
            Talents(1), Random(), now,
            additionalPlayers: [new(Guid.NewGuid(), Participant(second, CombatActorKind.Player, ["BATTLE_CRY"]), Talents(3))]);
        Assert.True(session.Handle(first.ActorId, new UseAbilityCommand("first", "BATTLE_CRY", first.ActorId), now).Succeeded);
        Assert.Equal(101, EffectEngine.CalculateStat(first, EffectStat.Accuracy, 100, now));
        Assert.True(session.Handle(second.ActorId, new UseAbilityCommand("second", "BATTLE_CRY", second.ActorId), now.AddSeconds(1)).Succeeded);
        Assert.Equal(103, EffectEngine.CalculateStat(first, EffectStat.Accuracy, 100, now.AddSeconds(1)));
        Assert.Equal(now.AddSeconds(21), Assert.Single(first.ActiveEffects, e => e.Definition.Id == "WARLORD_UNIFIED_RHYTHM").ExpiresAtUtc);
    }

    [Fact]
    public async Task SharedBannerDoesNotRewardTwiceWhenTwoWarlordsKnowTheAbility()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var ally = Actor(2000, 800);
        ally.TrySpendResource(50);
        var enemy = Actor(50, 50);
        var now = DateTimeOffset.UnixEpoch;
        var talents = ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = new HashSet<string> { "WAR_BANNER" } };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["WAR_BANNER"]),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []), talents, Random(), now,
            additionalPlayers: [new(Guid.NewGuid(), Participant(ally, CombatActorKind.Player, ["STRIKE", "WAR_BANNER"]), talents)]);
        Assert.True(session.Handle(owner.ActorId, new UseAbilityCommand("banner", "WAR_BANNER", owner.ActorId), now).Succeeded);
        Assert.True(session.Handle(ally.ActorId, new UseAbilityCommand("kill", "STRIKE", enemy.ActorId), now).Succeeded);
        Assert.Equal(72, owner.CurrentResource);
        Assert.Equal(62, ally.CurrentResource);
    }

    [Fact]
    public async Task BattleStandardDoesNotInheritCriticalScalingOrReapplyOutgoingModifiers()
    {
        var ability = Assert.Single((await Content).Abilities!, a => a.Id == "BATTLE_STANDARD");
        var owner = Actor(1000, 500);
        var enemy = Actor(2000, 2000);
        var now = DateTimeOffset.UnixEpoch;
        var runtime = new CombatRuntimeState(owner);
        runtime.AddActor(enemy);
        EffectEngine.Apply(owner, owner.ActorId, new("DAMAGE", EffectKind.StatModifier, TimeSpan.FromSeconds(10),
            1, EffectStackPolicy.Refresh, 1.2m, ModifiedStat: EffectStat.OutgoingDamageMultiplier,
            ModifierMode: EffectModifierMode.Multiplicative), now);
        Assert.True(AbilityEngine.Execute(runtime, ability, new("banner", ability.Id, owner.ActorId, [owner.ActorId]), now, Random()).Succeeded);
        var critical = new CombatEvent(CombatEventType.DamageDealt, now, owner.ActorId, "AUTO_ATTACK", 240,
            SourceActorId: owner.ActorId, TargetActorId: enemy.ActorId, IsCritical: true) { BaseDamage = 100 };
        var events = EffectEngine.ResolveEventActions(runtime, critical, new SequenceGameRandom(0.01m), new ProcGuard());
        Assert.Contains(events, e => e.Type == CombatEventType.DamageDealt && e.Amount == 30 && !e.IsCritical);
        Assert.Equal(1970, enemy.CurrentHp);
    }

    [Fact]
    public async Task PurifyingCryRemovesOnePoisonInstanceFromOwnerAndOnePartyAlly()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        owner.TrySpendResource(50);
        var ally = Actor(2000, 800);
        var now = DateTimeOffset.UnixEpoch;
        var poison = new EffectDefinition("POISON", EffectKind.Debuff, TimeSpan.FromSeconds(10),
            1, EffectStackPolicy.Independent, 1, DispelCategory: "POISON");
        foreach (var actor in new[] { owner, ally })
        {
            EffectEngine.Apply(actor, actor.ActorId, poison, now);
            EffectEngine.Apply(actor, actor.ActorId, poison, now);
        }
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "BATTLE_CRY" },
            EventHooks = [new("W-6-3", TalentModifierKeys.OnPartyEvent, 1, 5, null, TimeSpan.Zero, false)]
        };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["BATTLE_CRY"]),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities,
            new MonsterAiProfile("TEST", []), talents, Random(), now,
            additionalPlayers: [new(Guid.NewGuid(), Participant(ally, CombatActorKind.Player, []), ResolvedTalentModifiers.Empty)]);
        var result = session.Handle(owner.ActorId, new UseAbilityCommand("cry", "BATTLE_CRY", owner.ActorId), now);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Single(owner.ActiveEffects, e => e.Definition.Id == "POISON");
        Assert.Single(ally.ActiveEffects, e => e.Definition.Id == "POISON");
        Assert.Equal(35, owner.CurrentResource);
    }

    [Theory]
    [InlineData(EffectStackPolicy.Replace)]
    [InlineData(EffectStackPolicy.StrongestWins)]
    public void MaximumHealthReplacementPreservesCurrentHealthAndHealsOnlyTheIncrease(EffectStackPolicy policy)
    {
        var owner = Actor(1000, 1000);
        var now = DateTimeOffset.UnixEpoch;
        var definition = new EffectDefinition("MAX_HP", EffectKind.TemporaryMaxHp, TimeSpan.FromSeconds(10),
            1, policy, 0.1m, HealByMaxHpIncrease: true);
        EffectEngine.Apply(owner, owner.ActorId, definition, now);
        var events = EffectEngine.Apply(owner, owner.ActorId, definition with { Magnitude = 0.2m }, now.AddSeconds(1));
        Assert.Equal(1200, owner.MaxHp);
        Assert.Equal(1200, owner.CurrentHp);
        Assert.Contains(events, e => e.Type == CombatEventType.HealingApplied && e.Amount == 100);
        EffectEngine.Remove(owner, definition.Id, now.AddSeconds(2));
        Assert.Equal(1000, owner.CurrentHp);
        Assert.Equal(1000, owner.MaxHp);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BattleStandardRoutesExactlyOneSecondaryHitFromRealAutoAttackInBothHosts(bool arenaMode, bool executioner)
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var enemy = Actor(10000, 1000);
        var now = DateTimeOffset.UnixEpoch;
        var player = Participant(owner, CombatActorKind.Player, ["BATTLE_STANDARD"]) with
        { CanAutoAttack = true, AutoAttack = new(TimeSpan.FromSeconds(2), 100, 0, 0) };
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "BATTLE_STANDARD" },
            EventHooks = executioner
                ? [new("B-7-4", TalentModifierKeys.OnHpThreshold, 2, 20, null, TimeSpan.Zero, false, Threshold: 20)]
                : []
        };
        var random = new SequenceGameRandom(Enumerable.Repeat(0.01m, 500).ToArray());
        decimal hpBeforeAttack;
        if (arenaMode)
        {
            var account = Guid.NewGuid();
            var first = ArenaFighterAssembler.Create(new(account, player, talents), 60, abilities, false).Fighter;
            var second = ArenaFighterAssembler.Create(new(Guid.NewGuid(), Participant(enemy, CombatActorKind.Player, []),
                ResolvedTalentModifiers.Empty), 60, abilities, false).Fighter;
            var arena = new ArenaCombatSession(Guid.NewGuid(), first, second, random, now);
            Assert.True(arena.UseAbility(account, "banner", "BATTLE_STANDARD", owner.ActorId, now).Succeeded);
            hpBeforeAttack = enemy.CurrentHp;
            arena.AdvanceTo(now.AddSeconds(2));
        }
        else
        {
            var session = new CombatSession(Guid.NewGuid(), player, Participant(enemy, CombatActorKind.Monster, []),
                abilities, new MonsterAiProfile("TEST", []), talents, random, now);
            Assert.True(session.Handle(new UseAbilityCommand("banner", "BATTLE_STANDARD", owner.ActorId), now).Succeeded);
            hpBeforeAttack = enemy.CurrentHp;
            session.AdvanceTo(now.AddSeconds(2));
        }
        Assert.Equal(hpBeforeAttack - (executioner ? 150 : 125), enemy.CurrentHp);
    }

    [Fact]
    public async Task FullyAbsorbedGuardianHitStillAppliesControl()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var enemy = Actor(10000, 10000);
        var now = DateTimeOffset.UnixEpoch;
        EffectEngine.Apply(enemy, enemy.ActorId, new("SHIELD", EffectKind.Shield, TimeSpan.FromSeconds(10),
            1, EffectStackPolicy.Refresh, 1000), now);
        var talents = ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = new HashSet<string> { "CONCUSSION_BLOW" } };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["CONCUSSION_BLOW"]),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []), talents, Random(), now);
        Assert.True(session.Handle(new UseAbilityCommand("hit", "CONCUSSION_BLOW", enemy.ActorId), now).Succeeded);
        Assert.Equal(10000, enemy.CurrentHp);
        Assert.True(EffectEngine.HasControl(enemy, EffectKind.Stun, now));
    }

    [Fact]
    public async Task ComposedWarriorHasExactlyTwentyTalentUnlocksAndReadablePlayerDescriptions()
    {
        var content = await Content;
        var tree = Assert.Single(content.TalentTrees!, t => t.Id == "WARRIOR_TREE");
        var unlocks = TalentModifierResolver.Resolve(tree, tree.Nodes.ToDictionary(n => n.Id, n => n.MaxRank)).UnlockedAbilityIds;
        Assert.Equal(20, unlocks.Count);
        foreach (string id in unlocks)
            Assert.Contains(AbilityModes(), row => (string)row[0] == id);
        var descriptions = tree.Nodes.Select(n => n.Description).Concat(content.Abilities!
            .Where(a => unlocks.Contains(a.Id) || a.Id is "STRIKE" or "BATTLE_SHOUT" or "HEAVY_BLOW")
            .Select(a => a.Description));
        foreach (string? description in descriptions)
        {
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.DoesNotContain("???", description);
            Assert.DoesNotMatch("(?i)trigger|condition|runtime|dispatcher|proc handler|modifier bucket|event router|V2|LEGACY", description!);
        }
    }

    [Fact]
    public async Task WarlordAuraInitializesWhenOwnerIsNotFirstPartyParticipant()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var first = Actor(1000, 500);
        var owner = Actor(1000, 500);
        var talents = ResolvedTalentModifiers.Empty with
        { EventHooks = [new("W-1-2", TalentModifierKeys.OnPartyEvent, 3, 3, null, TimeSpan.Zero, false)] };
        var now = DateTimeOffset.UnixEpoch;
        _ = new CombatSession(Guid.NewGuid(), Participant(first, CombatActorKind.Player, []),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []),
            ResolvedTalentModifiers.Empty, Random(), now,
            additionalPlayers: [new(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, []), talents)]);
        Assert.Equal(103, EffectEngine.CalculateStat(owner, EffectStat.AttackPower, 100, now));
        Assert.Equal(103, EffectEngine.CalculateStat(first, EffectStat.AttackPower, 100, now));
    }

    [Fact]
    public async Task AllyKillActivatesOwnersWarBannerAndCapstone()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var ally = Actor(2000, 800);
        ally.TrySpendResource(50);
        var enemy = Actor(50, 50);
        var now = DateTimeOffset.UnixEpoch;
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "WAR_BANNER" },
            EventHooks = [new("W-9-1", TalentModifierKeys.OnPartyEvent, 1, 4, null, TimeSpan.Zero, false, SecondaryValue: 6)]
        };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["WAR_BANNER", "BATTLE_CRY"]),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []), talents, Random(), now,
            initialPlayerCooldowns: new Dictionary<string, DateTimeOffset> { ["BATTLE_CRY"] = now.AddSeconds(60) },
            additionalPlayers: [new(Guid.NewGuid(), Participant(ally, CombatActorKind.Player, ["STRIKE"]), ResolvedTalentModifiers.Empty)]);
        Assert.True(session.Handle(owner.ActorId, new UseAbilityCommand("banner", "WAR_BANNER", owner.ActorId), now).Succeeded);
        Assert.True(session.Handle(ally.ActorId, new UseAbilityCommand("kill", "STRIKE", enemy.ActorId), now).Succeeded);
        Assert.Equal(72, owner.CurrentResource);
        Assert.Equal(62, ally.CurrentResource);
        Assert.Equal(now.AddSeconds(59), session.Snapshot(owner.ActorId).Player.Cooldowns["BATTLE_CRY"]);
    }

    [Fact]
    public async Task SunderBecomesShieldSlamModifierWhenBothTalentsAreSelected()
    {
        var content = await Content;
        var profile = Assert.Single(content.ClassProfiles!, c => c.Id == "WARRIOR");
        var unlocks = new HashSet<string> { "SUNDER_ARMOR", "SHIELD_SLAM" };
        var known = CharacterKnownAbilityResolver.Resolve(profile, 60, unlocks);
        Assert.DoesNotContain("SUNDER_ARMOR", known);
        Assert.Contains("SHIELD_SLAM", known);
        var abilities = content.Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var enemy = Actor(10000, 10000);
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, known),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []),
            ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = unlocks }, Random(), DateTimeOffset.UnixEpoch);
        var result = session.Handle(new UseAbilityCommand("slam", "SHIELD_SLAM", enemy.ActorId), DateTimeOffset.UnixEpoch);
        Assert.True(result.Succeeded);
        Assert.Contains(enemy.ActiveEffects, e => e.Definition.Id == "GUARDIAN_SUNDER_ARMOR");
        Assert.DoesNotContain(result.Snapshot.Player.Abilities, a => a.Id == "SUNDER_ARMOR");
    }

    [Fact]
    public async Task LastStandUsesSharedMaximumHealthEffectAndUpgradeWithoutSessionHook()
    {
        var content = await Content;
        var tree = Assert.Single(content.TalentTrees!, t => t.Id == "WARRIOR_TREE");
        var upgrade = Assert.Single(tree.Nodes, n => n.Id == "G-5-4");
        Assert.DoesNotContain(upgrade.Modifiers!, m => m.Type == TalentModifierType.EventTriggered);
        var ability = Assert.Single(content.Abilities!, a => a.Id == "LAST_STAND");
        var upgraded = TalentAbilityResolver.Apply(ability,
            TalentModifierResolver.Resolve(tree, new Dictionary<string, int> { ["G-5-4"] = 2 }));
        Assert.Equal(0.30m, Assert.Single(upgraded.Actions!).Effect!.Magnitude);
        var owner = Actor(1000, 500);
        var runtime = new CombatRuntimeState(owner);
        var now = DateTimeOffset.UnixEpoch;
        var execution = AbilityEngine.Execute(runtime, ability, new("stand", ability.Id, owner.ActorId), now, Random());
        Assert.True(execution.Succeeded);
        Assert.Equal(1200, owner.MaxHp);
        Assert.Equal(700, owner.CurrentHp);
        Assert.Contains(execution.Events, e => e.Type == CombatEventType.HealingApplied && e.Amount == 200);
        EffectEngine.Remove(owner, "GUARDIAN_LAST_STAND_ACTIVE", now.AddSeconds(1));
        Assert.Equal(1000, owner.MaxHp);
    }

    [Fact]
    public async Task AvatarKillDuringBerserkReducesItsRunningCooldown()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var enemy = Actor(50, 50);
        var now = DateTimeOffset.UnixEpoch;
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "BERSERK" },
            EventHooks = [new("B-9-1", TalentModifierKeys.OnCriticalHit, 1, 10, null, TimeSpan.Zero, false, SecondaryValue: 15, ChancePercent: 20)]
        };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["STRIKE", "BERSERK"]),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []), talents, Random(), now);
        Assert.True(session.Handle(new UseAbilityCommand("berserk", "BERSERK", owner.ActorId), now).Succeeded);
        var killed = session.Handle(new UseAbilityCommand("kill", "STRIKE", enemy.ActorId), now);
        Assert.True(killed.Succeeded);
        Assert.Equal(now.AddSeconds(105), killed.Snapshot.Player.Cooldowns["BERSERK"]);
    }

    [Theory]
    [InlineData("SHIELD_BASH")]
    [InlineData("CONCUSSION_BLOW")]
    public async Task MissedGuardianAttackDoesNotApplyControl(string id)
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100, CombatStats.Default);
        var enemy = Actor(10000, 10000);
        var talents = ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = new HashSet<string> { id } };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, [id]),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []), talents,
            new SequenceGameRandom(0), DateTimeOffset.UnixEpoch);
        Assert.True(session.Handle(new UseAbilityCommand("miss", id, enemy.ActorId), DateTimeOffset.UnixEpoch).Succeeded);
        Assert.False(EffectEngine.HasControl(enemy, EffectKind.Stun, DateTimeOffset.UnixEpoch));
    }

    [Theory]
    [InlineData("LAST_STAND")]
    [InlineData("SHIELD_BLOCK")]
    [InlineData("BASTION")]
    public async Task BerserkBlocksDefensiveAbilitiesWithoutSpendingRage(string id)
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var now = DateTimeOffset.UnixEpoch;
        var talents = ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = new HashSet<string> { id, "BERSERK" } };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, talents.UnlockedAbilityIds),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []), talents, Random(), now);
        Assert.True(session.Handle(new UseAbilityCommand("berserk", "BERSERK", owner.ActorId), now).Succeeded);
        var result = session.Handle(new UseAbilityCommand("defense", id, owner.ActorId), now);
        Assert.False(result.Succeeded);
        Assert.Equal(50, owner.CurrentResource);
        Assert.DoesNotContain(id, result.Snapshot.Player.Cooldowns.Keys);
    }

    [Fact]
    public async Task LowHealthPartyMitigationIsConditionalAndEndsAfterHealing()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 200);
        var talents = ResolvedTalentModifiers.Empty with
        { EventHooks = [new("W-7-4", TalentModifierKeys.OnPartyEvent, 3, 6, null, TimeSpan.Zero, false)] };
        var now = DateTimeOffset.UnixEpoch;
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, []),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []),
            talents, Random(), now);
        Assert.Equal(0.94m, EffectEngine.CalculateStat(owner, EffectStat.IncomingDamageMultiplier, 1, now));
        owner.ApplyHealing(500);
        session.AdvanceTo(now.AddSeconds(1));
        Assert.Equal(1, EffectEngine.CalculateStat(owner, EffectStat.IncomingDamageMultiplier, 1, now.AddSeconds(1)));
    }

    [Fact]
    public async Task FlagCooldownReductionRespectsItsTwentySecondInternalCooldown()
    {
        var content = await Content;
        var tree = Assert.Single(content.TalentTrees!, t => t.Id == "WARRIOR_TREE");
        var node = Assert.Single(tree.Nodes, n => n.Id == "W-8-4");
        Assert.Equal(20, Assert.Single(node.Modifiers!).InternalCooldownSeconds);
        var abilities = content.Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var now = DateTimeOffset.UnixEpoch;
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "BATTLE_CRY", "WAR_BANNER", "VICTORY_FLAG" },
            EventHooks = [new("W-8-4", TalentModifierKeys.OnPartyEvent, 2, 5, null, TimeSpan.FromSeconds(20), false)]
        };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, talents.UnlockedAbilityIds),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []), talents,
            Random(), now, initialPlayerCooldowns: new Dictionary<string, DateTimeOffset> { ["BATTLE_CRY"] = now.AddSeconds(60) });
        Assert.True(session.Handle(new UseAbilityCommand("banner", "WAR_BANNER", owner.ActorId), now).Succeeded);
        var second = session.Handle(new UseAbilityCommand("flag", "VICTORY_FLAG", owner.ActorId), now.AddSeconds(1));
        Assert.True(second.Succeeded);
        Assert.Equal(now.AddSeconds(55), second.Snapshot.Player.Cooldowns["BATTLE_CRY"]);
    }

    [Theory]
    [InlineData("BATTLE_CRY", 60)]
    [InlineData("ENDURANCE_CRY", 90)]
    [InlineData("RALLY_CRY", 120)]
    [InlineData("CRY_OF_VENGEANCE", 30)]
    public async Task BattleRhythmReducesBaseCryCooldownWithoutReducingOtherRunningCries(string id, int seconds)
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var now = DateTimeOffset.UnixEpoch;
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { id },
            EventHooks = [new("W-7-3", TalentModifierKeys.OnPartyEvent, 2, 6, null, TimeSpan.Zero, false)]
        };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, [id]),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities,
            new MonsterAiProfile("TEST", []), talents, Random(), now,
            initialPlayerCooldowns: new Dictionary<string, DateTimeOffset> { ["OTHER_CRY"] = now.AddSeconds(50) });
        var result = session.Handle(new UseAbilityCommand("cry", id, owner.ActorId), now);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(TimeSpan.FromSeconds(seconds - 6), Assert.Single(result.Snapshot.Player.Abilities, a => a.Id == id).Cooldown);
        Assert.Equal(now.AddSeconds(seconds - 6), result.Snapshot.Player.Cooldowns[id]);
    }

    [Fact]
    public async Task CapstoneRallyRestoresTwentyPercentRageAfterItsCost()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "RALLY_CRY" },
            EventHooks = [new("W-9-1", TalentModifierKeys.OnPartyEvent, 1, 4, null, TimeSpan.Zero, false, SecondaryValue: 6)]
        };
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["RALLY_CRY"]),
            Participant(Actor(10000, 10000), CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []),
            talents, Random(), DateTimeOffset.UnixEpoch);
        Assert.True(session.Handle(new UseAbilityCommand("rally", "RALLY_CRY", owner.ActorId), DateTimeOffset.UnixEpoch).Succeeded);
        Assert.Equal(90, owner.CurrentResource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VengeanceStacksFromPartyDamageAndConsumesOnlyInsideItsWindow(bool removeWindow)
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var ally = Actor(2000, 1800);
        var enemy = Actor(10000, 10000);
        var now = DateTimeOffset.UnixEpoch;
        var player = Participant(owner, CombatActorKind.Player, ["CRY_OF_VENGEANCE"]) with
        { CanAutoAttack = true, AutoAttack = new(TimeSpan.FromSeconds(2), 100, 0, 0) };
        var allyParticipant = Participant(ally, CombatActorKind.Player, ["PROVOKE"]);
        var opponent = Participant(enemy, CombatActorKind.Monster, []) with
        { CanAutoAttack = true, AutoAttack = new(TimeSpan.FromSeconds(0.25), 10, 0, 0) };
        var session = new CombatSession(Guid.NewGuid(), player, opponent, abilities, new MonsterAiProfile("TEST", []),
            ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = new HashSet<string> { "CRY_OF_VENGEANCE" } },
            Random(), now, additionalPlayers: [new(Guid.NewGuid(), allyParticipant,
                ResolvedTalentModifiers.Empty with { UnlockedAbilityIds = new HashSet<string> { "PROVOKE" } })]);
        Assert.True(session.Handle(owner.ActorId, new UseAbilityCommand("cry", "CRY_OF_VENGEANCE", owner.ActorId), now).Succeeded);
        Assert.DoesNotContain(owner.ActiveEffects, e => e.Definition.Id == "WARLORD_VENGEANCE_STACKS");
        Assert.True(session.Handle(ally.ActorId, new UseAbilityCommand("taunt", "PROVOKE", enemy.ActorId), now).Succeeded);
        session.AdvanceTo(now.AddSeconds(0.25));
        var stacks = Assert.Single(owner.ActiveEffects, e => e.Definition.Id == "WARLORD_VENGEANCE_STACKS");
        Assert.Equal(1, stacks.Stacks);
        session.AdvanceTo(now.AddSeconds(0.5));
        Assert.Equal(1, stacks.Stacks);
        session.AdvanceTo(now.AddSeconds(0.75));
        Assert.Equal(2, stacks.Stacks);
        Assert.Equal(now.AddSeconds(10), stacks.ExpiresAtUtc);
        decimal hpBefore = enemy.CurrentHp;
        if (removeWindow)
            EffectEngine.Remove(owner, "WARLORD_CRIT_OF_VENGEANCE", now.AddSeconds(0.75));
        session.AdvanceTo(now.AddSeconds(2));
        Assert.Equal(hpBefore - (removeWindow ? 100 : 240), enemy.CurrentHp);
        Assert.DoesNotContain(owner.ActiveEffects, e => e.Definition.Id == "WARLORD_VENGEANCE_STACKS");
    }

    [Fact]
    public async Task VictoryFlagPreventsNewControlForFourSecondsWithoutRemovingExistingControl()
    {
        var flag = Assert.Single((await Content).Abilities!, a => a.Id == "VICTORY_FLAG");
        var owner = Actor(1000, 500);
        var now = DateTimeOffset.UnixEpoch;
        var silence = new EffectDefinition("OLD", EffectKind.Root, TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Refresh, 1);
        EffectEngine.Apply(owner, Guid.NewGuid(), silence, now);
        Assert.True(AbilityEngine.Execute(new(owner), flag, new("flag", flag.Id, owner.ActorId, [owner.ActorId]), now, Random()).Succeeded);
        Assert.Contains(owner.ActiveEffects, e => e.Definition.Id == "OLD");
        var stun = silence with { Id = "NEW", Kind = EffectKind.Stun };
        Assert.Contains(EffectEngine.Apply(owner, Guid.NewGuid(), stun, now.AddSeconds(1)), e => e.Type == CombatEventType.EffectImmune);
        Assert.DoesNotContain(owner.ActiveEffects, e => e.Definition.Id == "NEW");
        Assert.Contains(EffectEngine.Apply(owner, Guid.NewGuid(), stun, now.AddSeconds(4)), e => e.Type == CombatEventType.EffectApplied);
    }

    [Fact]
    public async Task BattleStandardAddsConditionalSecondaryHitInsteadOfReducingAllPhysicalDamage()
    {
        var ability = Assert.Single((await Content).Abilities!, a => a.Id == "BATTLE_STANDARD");
        var owner = Actor(1000, 500);
        var enemy = Actor(2000, 2000);
        var runtime = new CombatRuntimeState(owner);
        runtime.AddActor(enemy);
        var now = DateTimeOffset.UnixEpoch;
        Assert.True(AbilityEngine.Execute(runtime, ability,
            new("banner", ability.Id, owner.ActorId, [owner.ActorId]), now, Random()).Succeeded);
        Assert.Equal(1, EffectEngine.CalculateStat(owner, EffectStat.OutgoingPhysicalDamageMultiplier, 1, now));
        var hit = new CombatEvent(CombatEventType.DamageDealt, now, owner.ActorId, "AUTO_ATTACK", 100,
            SourceActorId: owner.ActorId, TargetActorId: enemy.ActorId) { BaseDamage = 100 };
        var guard = new ProcGuard();
        var secondary = EffectEngine.ResolveEventActions(runtime, hit, new SequenceGameRandom(0.01m), guard);
        Assert.Equal(1975, enemy.CurrentHp);
        Assert.Contains(secondary, e => e.Type == CombatEventType.DamageDealt && e.Amount == 25 && e.IsProc && e.ProcDepth == 1);
        Assert.Empty(EffectEngine.ResolveEventActions(runtime, hit, Random(), guard));
        Assert.Empty(EffectEngine.ResolveEventActions(runtime, hit with { IsPeriodic = true }, Random(), guard));
        Assert.Empty(EffectEngine.ResolveEventActions(runtime, hit with { IsProc = true, ProcDepth = 1 }, Random(), guard));
        Assert.Empty(EffectEngine.ResolveEventActions(runtime, hit with { DefinitionId = "STRIKE" }, Random(), guard));
    }

    [Theory]
    [InlineData("STRIKE", 80, 10)]
    [InlineData("HEAVY_BLOW", 160, -30)]
    [InlineData("BATTLE_SHOUT", 0, 20)]
    public async Task BaselineKitExecutesThroughSessionAndExposesClientContract(string id, decimal damage, decimal rage)
    {
        var content = await Content;
        var abilities = content.Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        owner.TrySpendResource(50);
        var enemy = Actor(2000, 2000);
        var warrior = Assert.Single(content.ClassProfiles!, c => c.Id == "WARRIOR");
        var known = CharacterKnownAbilityResolver.Resolve(warrior, 6).ToHashSet();
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, known),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []),
            ResolvedTalentModifiers.Empty, new SequenceGameRandom(Enumerable.Repeat(0.99m, 100).ToArray()), DateTimeOffset.UnixEpoch);
        var result = session.Handle(new UseAbilityCommand("baseline", id, enemy.ActorId), DateTimeOffset.UnixEpoch);
        Assert.True(result.Succeeded);
        Assert.Equal(2000 - damage, enemy.CurrentHp);
        Assert.Equal(50 + rage, owner.CurrentResource);
        Assert.Contains(result.Snapshot.Player.Abilities, a => a.Id == id && a.ResourceCost == abilities[id].ResourceCost);
        Assert.Contains(result.Events, e => e.Type == CombatEventType.AbilityCompleted && e.DefinitionId == id);
        Assert.False(session.Handle(new UseAbilityCommand("baseline", id, enemy.ActorId), DateTimeOffset.UnixEpoch).Succeeded);
        Assert.Equal(50 + rage, owner.CurrentResource);
    }

    [Fact]
    public async Task WarlordCryUpgradeIncludesCapturedPartyPlayersAndExpiresWithCry()
    {
        var abilities = (await Content).Abilities!.ToDictionary(a => a.Id);
        var owner = Actor(1000, 500);
        var ally = Actor(2000, 800);
        var enemy = Actor(10000, 10000);
        var talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string> { "BATTLE_CRY" },
            EventHooks = [new("W-2-4", TalentModifierKeys.OnPartyEvent, 1, 3, null, TimeSpan.Zero, false)]
        };
        var now = DateTimeOffset.UnixEpoch;
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, CombatActorKind.Player, ["BATTLE_CRY"]),
            Participant(enemy, CombatActorKind.Monster, []), abilities, new MonsterAiProfile("TEST", []),
            talents, new SequenceGameRandom(0.99m), now,
            additionalPlayers: [new(Guid.NewGuid(), Participant(ally, CombatActorKind.Player, []), ResolvedTalentModifiers.Empty)]);
        var cast = session.Handle(owner.ActorId, new UseAbilityCommand("cry", "BATTLE_CRY", owner.ActorId), now);
        Assert.True(cast.Succeeded, cast.ErrorCode);
        var effect = Assert.Single(ally.ActiveEffects, e => e.Definition.Id == "WARLORD_UNIFIED_RHYTHM");
        Assert.Equal(now.AddSeconds(20), effect.ExpiresAtUtc);
        Assert.Equal(103, EffectEngine.CalculateStat(ally, EffectStat.Accuracy, 100, now));
        session.AdvanceTo(now.AddSeconds(20));
        Assert.DoesNotContain(ally.ActiveEffects, e => e.Definition.Id == "WARLORD_UNIFIED_RHYTHM");
    }

    private static CombatParticipantDefinition Participant(CombatActorState actor, CombatActorKind kind, IEnumerable<string> known) =>
        new(actor, kind, kind == CombatActorKind.Player ? "WARRIOR" : "TEST", "Test", "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0), known.ToHashSet(), CanAutoAttack: false);

    [Theory]
    [InlineData(1, "STRIKE", true)]
    [InlineData(1, "BATTLE_SHOUT", false)]
    [InlineData(3, "BATTLE_SHOUT", true)]
    [InlineData(5, "HEAVY_BLOW", false)]
    [InlineData(6, "HEAVY_BLOW", true)]
    public async Task BaselineUnlocksDoNotRequireTalents(int level, string id, bool known)
    {
        var content = await Content;
        var warrior = Assert.Single(content.ClassProfiles!, profile => profile.Id == "WARRIOR");
        Assert.Equal(known, CharacterKnownAbilityResolver.Resolve(warrior, level).Contains(id));
    }

    [Fact]
    public async Task EnduranceChangesMaximumHealthAndCleansUpWithoutHealing()
    {
        var content = await Content;
        AbilityDefinition ability = Assert.Single(content.Abilities!, a => a.Id == "ENDURANCE_CRY");
        var owner = Actor(1000, 500);
        var ally = Actor(2000, 800);
        var runtime = new CombatRuntimeState(owner);
        runtime.AddActor(ally);
        var now = DateTimeOffset.UnixEpoch;
        var result = AbilityEngine.Execute(runtime, ability,
            new("endurance", ability.Id, owner.ActorId, [owner.ActorId, ally.ActorId]), now, new SequenceGameRandom(0.99m));
        Assert.True(result.Succeeded);
        Assert.Equal(1060, owner.MaxHp);
        Assert.Equal(2120, ally.MaxHp);
        Assert.Equal(500, owner.CurrentHp);
        Assert.Equal(75, owner.CurrentResource);
        Assert.Equal(now.AddSeconds(90), runtime.Cooldowns[ability.Id]);
        Assert.Contains(result.Events, e => e.Type == CombatEventType.EffectApplied && e.TargetActorId == ally.ActorId);
        EffectEngine.Process(owner, now.AddSeconds(15));
        Assert.Equal(1000, owner.MaxHp);
        EffectEngine.Dispel(ally, "WARLORD", now.AddSeconds(1));
        Assert.Equal(2000, ally.MaxHp);
    }

    [Fact]
    public async Task RallyHealsEachTargetBySixPercentOfItsOwnMaximum()
    {
        var content = await Content;
        AbilityDefinition ability = Assert.Single(content.Abilities!, a => a.Id == "RALLY_CRY");
        var owner = Actor(1000, 500);
        var ally = Actor(2000, 800);
        var runtime = new CombatRuntimeState(owner);
        runtime.AddActor(ally);
        var now = DateTimeOffset.UnixEpoch;
        var result = AbilityEngine.Execute(runtime, ability,
            new("rally", ability.Id, owner.ActorId, [owner.ActorId, ally.ActorId]), now, new SequenceGameRandom(0.99m));
        Assert.True(result.Succeeded);
        Assert.Equal(560, owner.CurrentHp);
        Assert.Equal(920, ally.CurrentHp);
        Assert.Equal(70, owner.CurrentResource);
        Assert.Equal(now.AddSeconds(120), runtime.Cooldowns[ability.Id]);
        Assert.Contains(result.Events, e => e.Type == CombatEventType.HealingApplied && e.Amount == 120 && e.TargetActorId == ally.ActorId);
    }

    private static CombatActorState Actor(decimal maxHp, decimal hp) =>
        new(Guid.NewGuid(), maxHp, hp, 100, 100, CombatStats.Default with { Accuracy = 100, AttackPower = 100 });

    private static string ContentPath()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "content", "package.json");
            if (File.Exists(path)) return path;
        }
        throw new DirectoryNotFoundException("Repository content not found.");
    }
}
