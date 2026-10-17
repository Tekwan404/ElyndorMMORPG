using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class OpenWorldCombatSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly AbilityDefinition Strike = Ability("STRIKE", AbilityTargetType.SingleEnemy);
    private static readonly AbilityDefinition Cleave = Ability("CLEAVE", AbilityTargetType.AllEnemiesInCombat);
    private static readonly AbilityDefinition Heal = new("HEAL", AbilityType.Instant, AbilityTargetType.SingleAlly,
        0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "HOLY",
        Actions: [new(AbilityActionType.Healing, 10)]);
    private static readonly IReadOnlyDictionary<string, AbilityDefinition> Abilities =
        new[] { Strike, Cleave, Heal }.ToDictionary(ability => ability.Id);

    [Fact]
    public void EnemyJoinsExistingFightWithoutResettingMonsterOrSequence()
    {
        var human = Player("HUMAN");
        var monster = Monster();
        var session = Fight(human, monster);
        Assert.True(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("before-join", Strike.Id, monster.Actor.ActorId), Now).Succeeded);
        decimal health = monster.Actor.CurrentHp;
        long sequence = session.Sequence;
        var undead = Player("UNDEAD");
        Assert.True(session.TryJoinOpenWorldParticipant(undead, Now, out var error), error);
        Assert.Equal(health, session.Snapshot(undead.Participant.Actor.ActorId).Enemies!
            .Single(actor => actor.ActorId == monster.Actor.ActorId).Hp);
        Assert.True(session.Sequence > sequence);
        Assert.Equal(session.SessionId, session.Snapshot(undead.Participant.Actor.ActorId).SessionId);
        Assert.True(session.Handle(undead.Participant.Actor.ActorId,
            new UseAbilityCommand("attack-human", Strike.Id, human.Participant.Actor.ActorId), Now).Succeeded);
        Assert.True(human.Participant.Actor.CurrentHp < human.Participant.Actor.MaxHp);
    }

    [Fact]
    public void SameFactionCannotBeAttackedAndEnemyCannotBeHealed()
    {
        var human = Player("HUMAN");
        var ally = Player("HUMAN");
        var enemy = Player("UNDEAD");
        var session = Fight(human, Monster());
        Assert.True(session.TryJoinOpenWorldParticipant(ally, Now, out _));
        Assert.True(session.TryJoinOpenWorldParticipant(enemy, Now, out _));
        Assert.False(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("friendly-fire", Strike.Id, ally.Participant.Actor.ActorId), Now).Succeeded);
        Assert.False(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("heal-enemy", Heal.Id, enemy.Participant.Actor.ActorId), Now).Succeeded);
    }

    [Fact]
    public void AreaDamageHitsMonsterAndEnemyPlayerButNotAllies()
    {
        var human = Player("HUMAN");
        var ally = Player("HUMAN");
        var enemy = Player("UNDEAD");
        var monster = Monster();
        var session = Fight(human, monster);
        session.TryJoinOpenWorldParticipant(ally, Now, out _);
        session.TryJoinOpenWorldParticipant(enemy, Now, out _);
        Assert.True(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("cleave", Cleave.Id, Guid.Empty), Now).Succeeded);
        Assert.True(enemy.Participant.Actor.CurrentHp < 100);
        Assert.True(monster.Actor.CurrentHp < 100);
        Assert.Equal(100, ally.Participant.Actor.CurrentHp);
    }

    [Fact]
    public void PurePvpNeedsNoFakeMonsterAndDoesNotGrantPveParticipation()
    {
        var human = Player("HUMAN");
        var enemy = Player("UNDEAD");
        var session = new CombatSession(Guid.NewGuid(), human.Participant,
            Array.Empty<CombatParticipantDefinition>(), Abilities,
            new Dictionary<Guid, MonsterAiProfile>(), human.TalentModifiers, new FixedRandom(), Now,
            playerAccountId: human.AccountId, additionalPlayers: [enemy], openWorldPvp: true);
        Assert.True(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("pvp-only", Strike.Id, enemy.Participant.Actor.ActorId), Now).Succeeded);
        Assert.False(session.EvaluateParticipant(human.Participant.Actor.ActorId, Now.AddMinutes(1)).IsEligible);
        Assert.All(session.Snapshot().Enemies!, actor => Assert.Equal(CombatActorKind.Player, actor.Kind));
    }

    private static CombatSession Fight(CombatPlayerDefinition player, CombatParticipantDefinition monster) =>
        new(Guid.NewGuid(), player.Participant, [monster], Abilities,
            new Dictionary<Guid, MonsterAiProfile> { [monster.Actor.ActorId] = new("TEST_AI", []) },
            player.TalentModifiers, new FixedRandom(), Now,
            companion: player.Companion, playerAccountId: player.AccountId, openWorldPvp: true);

    [Fact]
    public void DeadPlayerCanLeaveWhileMonsterFightContinues()
    {
        var human = Player("HUMAN");
        var enemy = Player("UNDEAD");
        var session = Fight(human, Monster());
        session.TryJoinOpenWorldParticipant(enemy, Now, out _);
        enemy.Participant.Actor.SetCurrentHp(1);
        Assert.True(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("kill", Strike.Id, enemy.Participant.Actor.ActorId), Now).Succeeded);
        Assert.Equal(CombatSessionStatus.Active, session.Status);
        Assert.True(session.Handle(enemy.Participant.Actor.ActorId, new FleeCommand("respawn"), Now).Succeeded);
        Assert.Equal(CombatSessionStatus.Active, session.Status);
        Assert.False(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("corpse", Strike.Id, enemy.Participant.Actor.ActorId), Now).Succeeded);
    }

    [Fact]
    public void LateArcherKeepsOwnCompanionAndItsMonsterContribution()
    {
        var human = Player("HUMAN");
        var pet = new CombatParticipantDefinition(new(Guid.NewGuid(), 100, 100, 0, 0, CombatStats.Default),
            CombatActorKind.Companion, "ARCHER_STARTER_PREDATOR", "Pet", "NONE",
            new(TimeSpan.FromSeconds(1), 10, 0, 0), new HashSet<string>());
        var undead = Player("UNDEAD") with { Companion = pet };
        var session = Fight(human, Monster());
        Assert.True(session.TryJoinOpenWorldParticipant(undead, Now, out _));
        Assert.Equal(pet.Actor.ActorId, session.Snapshot(undead.Participant.Actor.ActorId).Companion!.ActorId);
        Assert.Null(session.Snapshot(human.Participant.Actor.ActorId).Companion);
        Assert.Contains(session.Snapshot(human.Participant.Actor.ActorId).Enemies!, actor => actor.ActorId == pet.Actor.ActorId);
        session.AdvanceTo(Now.AddSeconds(1));
        Assert.True(session.EvaluateParticipant(undead.Participant.Actor.ActorId, Now.AddSeconds(1)).IsEligible);
        pet.Actor.SetCurrentHp(1);
        Assert.True(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("kill-pet", Strike.Id, pet.Actor.ActorId), Now.AddSeconds(1)).Succeeded);
        Assert.Equal(CombatSessionStatus.Active, session.Status);
    }

    [Fact]
    public void FrostNovaOnlyControlsHostileActorsIncludingLateArrivals()
    {
        var nova = new AbilityDefinition("MAGE_FROST_NOVA", AbilityType.Instant, AbilityTargetType.AllEnemiesInCombat,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "FROST", Actions: []);
        var human = Player("HUMAN");
        human = human with { Participant = human.Participant with { DefinitionId = "MAGE", KnownAbilityIds = new HashSet<string> { nova.Id } } };
        var monster = Monster();
        var session = new CombatSession(Guid.NewGuid(), human.Participant, [monster],
            new Dictionary<string, AbilityDefinition> { [nova.Id] = nova },
            new Dictionary<Guid, MonsterAiProfile> { [monster.Actor.ActorId] = new("TEST_AI", []) },
            human.TalentModifiers, new FixedRandom(), Now, playerAccountId: human.AccountId, openWorldPvp: true);
        var ally = Player("HUMAN");
        var enemy = Player("UNDEAD");
        session.TryJoinOpenWorldParticipant(ally, Now, out _);
        session.TryJoinOpenWorldParticipant(enemy, Now, out _);
        Assert.True(session.Handle(human.Participant.Actor.ActorId,
            new UseAbilityCommand("nova", nova.Id, monster.Actor.ActorId), Now).Succeeded);
        Assert.Empty(human.Participant.Actor.ActiveEffects);
        Assert.Empty(ally.Participant.Actor.ActiveEffects);
        Assert.NotEmpty(enemy.Participant.Actor.ActiveEffects);
        Assert.NotEmpty(monster.Actor.ActiveEffects);
    }

    [Fact]
    public void CapturedPartyMemberOutsideLocationIsNotAnAttackableActor()
    {
        var human = Player("HUMAN");
        var distant = Player("HUMAN") with { InitiallyAttached = false };
        var monster = Monster();
        var session = new CombatSession(Guid.NewGuid(), human.Participant, [monster], Abilities,
            new Dictionary<Guid, MonsterAiProfile> { [monster.Actor.ActorId] = new("TEST_AI", []) },
            human.TalentModifiers, new FixedRandom(), Now, playerAccountId: human.AccountId,
            additionalPlayers: [distant], openWorldPvp: true);
        var undead = Player("UNDEAD");
        Assert.True(session.TryJoinOpenWorldParticipant(undead, Now, out _));
        Assert.DoesNotContain(session.Snapshot(undead.Participant.Actor.ActorId).Enemies!,
            actor => actor.ActorId == distant.Participant.Actor.ActorId);
        Assert.False(session.Handle(undead.Participant.Actor.ActorId,
            new UseAbilityCommand("forged-target", Strike.Id, distant.Participant.Actor.ActorId), Now).Succeeded);
    }

    [Fact]
    public void JoiningPrimaryPetOwnerPreservesItsRuntimeAndContributionOwner()
    {
        var pet = new CombatParticipantDefinition(new(Guid.NewGuid(), 100, 100, 0, 0, CombatStats.Default),
            CombatActorKind.Companion, "ARCHER_STARTER_PREDATOR", "Pet", "NONE",
            new(TimeSpan.FromSeconds(1), 10, 0, 0), new HashSet<string>());
        var human = Player("HUMAN") with { Companion = pet };
        var session = Fight(human, Monster());
        var enemy = Player("UNDEAD");
        Assert.True(session.TryJoinOpenWorldParticipant(enemy, Now, out _));
        session.AdvanceTo(Now.AddSeconds(1));
        Assert.True(session.EvaluateParticipant(human.Participant.Actor.ActorId, Now.AddSeconds(1)).IsEligible);
        Assert.False(session.EvaluateParticipant(enemy.Participant.Actor.ActorId, Now.AddSeconds(1)).IsEligible);
        Assert.Equal(pet.Actor.ActorId, session.Snapshot(human.Participant.Actor.ActorId).Companion!.ActorId);
    }

    [Fact]
    public void PetKillingAnOpposingPlayerKeepsAttackingOwnerAndAdvancesItsTimer()
    {
        var human = Player("HUMAN");
        var monster = Monster();
        var pet = new CombatParticipantDefinition(new(Guid.NewGuid(), 100, 100, 0, 0, CombatStats.Default),
            CombatActorKind.Companion, "ARCHER_STARTER_PREDATOR", "Pet", "NONE",
            new(TimeSpan.FromSeconds(1), 10, 0, 0), new HashSet<string>());
        var undead = Player("UNDEAD") with { Companion = pet };
        var session = Fight(human, monster);
        session.TryJoinOpenWorldParticipant(undead, Now, out _);
        session.Handle(undead.Participant.Actor.ActorId, new SelectTargetCommand("pet-target", human.Participant.Actor.ActorId), Now);
        human.Participant.Actor.SetCurrentHp(1);
        session.AdvanceTo(Now.AddSeconds(1));
        Assert.True(human.Participant.Actor.IsDead);
        Assert.Equal(CombatSessionStatus.Active, session.Status);
        session.AdvanceTo(Now.AddSeconds(2));
        Assert.True(monster.Actor.CurrentHp < monster.Actor.MaxHp);
    }

    [Fact]
    public void PlayerAutoAttackKillRetargetsWithoutReusingVictimsTimer()
    {
        var human = Player("HUMAN");
        human = human with { Participant = human.Participant with { CanAutoAttack = true, AutoAttack = new(TimeSpan.FromSeconds(1), 10, 0, 0) } };
        var monster = Monster();
        var undead = Player("UNDEAD");
        var session = Fight(human, monster);
        session.TryJoinOpenWorldParticipant(undead, Now, out _);
        session.Handle(human.Participant.Actor.ActorId, new SelectTargetCommand("enemy-player", undead.Participant.Actor.ActorId), Now);
        undead.Participant.Actor.SetCurrentHp(1);
        session.AdvanceTo(Now.AddSeconds(1));
        Assert.True(undead.Participant.Actor.IsDead);
        Assert.Equal(CombatSessionStatus.Active, session.Status);
        decimal hp = monster.Actor.CurrentHp;
        session.AdvanceTo(Now.AddSeconds(2));
        Assert.True(monster.Actor.CurrentHp < hp);
    }

    [Fact]
    public void PetPoisonKeepsItsSourceWhileTickingOnOpposingOwner()
    {
        var human = Player("HUMAN");
        var pet = new CombatParticipantDefinition(new(Guid.NewGuid(), 100, 100, 0, 0, CombatStats.Default),
            CombatActorKind.Companion, "ARCHER_STARTER_PREDATOR", "Pet", "NONE",
            new(TimeSpan.FromHours(1), 0, 0, 0), new HashSet<string>(), CanAutoAttack: false);
        var undead = Player("UNDEAD") with { Companion = pet };
        var session = Fight(human, Monster());
        session.TryJoinOpenWorldParticipant(undead, Now, out _);
        EffectEngine.Apply(human.Participant.Actor, pet.Actor.ActorId,
            new("PET_POISON", EffectKind.DamageOverTime, TimeSpan.FromSeconds(3), 1,
                EffectStackPolicy.Refresh, 10, TickInterval: TimeSpan.FromSeconds(1)), Now);
        session.AdvanceTo(Now.AddSeconds(1));
        Assert.Equal(90, human.Participant.Actor.CurrentHp);
        Assert.False(session.EvaluateParticipant(undead.Participant.Actor.ActorId, Now.AddSeconds(1)).IsEligible);
    }

    [Fact]
    public void OpposingOwnerDeathRetargetsPlayersAwayFromItsInactiveCompanion()
    {
        var human = Player("HUMAN");
        var ally = Player("HUMAN");
        var monster = Monster();
        var pet = new CombatParticipantDefinition(new(Guid.NewGuid(), 100, 100, 0, 0, CombatStats.Default),
            CombatActorKind.Companion, "ARCHER_STARTER_PREDATOR", "Pet", "NONE",
            new(TimeSpan.FromHours(1), 0, 0, 0), new HashSet<string>(), CanAutoAttack: false);
        var undead = Player("UNDEAD") with { Companion = pet };
        var session = Fight(human, monster);
        session.TryJoinOpenWorldParticipant(ally, Now, out _);
        session.TryJoinOpenWorldParticipant(undead, Now, out _);
        session.Handle(human.Participant.Actor.ActorId, new SelectTargetCommand("pet", pet.Actor.ActorId), Now);
        undead.Participant.Actor.SetCurrentHp(1);
        session.Handle(ally.Participant.Actor.ActorId,
            new UseAbilityCommand("owner-kill", Strike.Id, undead.Participant.Actor.ActorId), Now);
        Assert.Equal(monster.Actor.ActorId, session.Snapshot(human.Participant.Actor.ActorId).SelectedTargetActorId);
    }

    [Fact]
    public void ProgressionCapturesUsefulHealingAndDamageWithoutPveCredit()
    {
        var human = Player("HUMAN");
        var healer = Player("HUMAN");
        var enemy = Player("UNDEAD");
        var session = Fight(human, Monster());
        session.TryJoinOpenWorldParticipant(healer, Now, out _);
        session.TryJoinOpenWorldParticipant(enemy, Now, out _);
        Guid humanId = human.Participant.Actor.ActorId, enemyId = enemy.Participant.Actor.ActorId;
        session.Handle(humanId, new UseAbilityCommand("first", Strike.Id, enemyId), Now);
        session.Handle(enemyId, new UseAbilityCommand("wound", Strike.Id, humanId), Now);
        session.Handle(healer.Participant.Actor.ActorId, new UseAbilityCommand("support", Heal.Id, humanId), Now);
        for (int i = 0; i < 10 && !enemy.Participant.Actor.IsDead; i++)
            session.Handle(humanId, new UseAbilityCommand($"kill-{i}", Strike.Id, enemyId), Now.AddSeconds(12));
        var defeat = Assert.Single(session.Snapshot(humanId).WorldPvpDefeats!);
        Assert.Equal(enemyId, defeat.VictimId);
        Assert.Equal(humanId, defeat.KillerId);
        Assert.True(defeat.Contributions.Single(c => c.CharacterId == humanId).Damage > 0);
        Assert.Equal(10, defeat.Contributions.Single(c => c.CharacterId == healer.Participant.Actor.ActorId).Support);
        Assert.DoesNotContain(defeat.Contributions, c => c.CharacterId == enemyId);
    }

    [Fact]
    public void InstantKillsHaveDeathRecordWithoutProgression()
    {
        var human = Player("HUMAN");
        var enemy = Player("UNDEAD");
        var session = Fight(human, Monster());
        session.TryJoinOpenWorldParticipant(enemy, Now, out _);
        enemy.Participant.Actor.SetCurrentHp(1);
        session.Handle(human.Participant.Actor.ActorId, new UseAbilityCommand("fast", Strike.Id, enemy.Participant.Actor.ActorId), Now);
        var defeat = Assert.Single(session.Snapshot().WorldPvpDefeats!);
        Assert.Empty(defeat.Contributions);
        Assert.Equal(human.Participant.Actor.ActorId, defeat.KillerId);
    }

    [Fact]
    public void OldHealingCannotGrantAssistsForLaterOpponents()
    {
        var human = Player("HUMAN");
        var healer = Player("HUMAN");
        var enemy = Player("UNDEAD");
        var session = Fight(human, Monster());
        session.TryJoinOpenWorldParticipant(healer, Now, out _);
        session.TryJoinOpenWorldParticipant(enemy, Now, out _);
        Guid h = human.Participant.Actor.ActorId, e = enemy.Participant.Actor.ActorId;
        session.Handle(e, new UseAbilityCommand("wound", Strike.Id, h), Now);
        session.Handle(healer.Participant.Actor.ActorId, new UseAbilityCommand("heal", Heal.Id, h), Now);
        session.Handle(h, new UseAbilityCommand("engage", Strike.Id, e), Now);
        human.Participant.Actor.SetCurrentHp(80);
        session.Handle(healer.Participant.Actor.ActorId, new UseAbilityCommand("pve-heal", Heal.Id, h), Now.AddSeconds(29));
        for (int i = 0; i < 10 && !enemy.Participant.Actor.IsDead; i++)
            session.Handle(h, new UseAbilityCommand($"late-{i}", Strike.Id, e), Now.AddSeconds(45));
        var defeat = Assert.Single(session.Snapshot(h).WorldPvpDefeats!);
        Assert.DoesNotContain(defeat.Contributions, c => c.CharacterId == healer.Participant.Actor.ActorId);
    }

    private static CombatPlayerDefinition Player(string race) => new(Guid.NewGuid(),
        new(new CombatActorState(Guid.NewGuid(), 100, 100, 100, 100, CombatStats.Default),
            CombatActorKind.Player, "WARRIOR", race, "RAGE", new(TimeSpan.FromHours(1), 1, 0, 0),
            Abilities.Keys.ToHashSet(), CanAutoAttack: false, RaceId: race), ResolvedTalentModifiers.Empty);

    private static CombatParticipantDefinition Monster() => new(
        new CombatActorState(Guid.NewGuid(), 100, 100, 0, 0, CombatStats.Default),
        CombatActorKind.Monster, "TEST_MONSTER", "Monster", "NONE",
        new(TimeSpan.FromHours(1), 1, 0, 0), new HashSet<string>(), CanAutoAttack: false);

    private static AbilityDefinition Ability(string id, AbilityTargetType target) => new(id, AbilityType.Instant,
        target, 0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "PHYSICAL",
        Actions: [new(AbilityActionType.Damage, 10, CanMiss: false, CanCrit: false, CanDodge: false)]);

    private sealed class FixedRandom : IGameRandom { public decimal NextUnit() => 0.99m; }
}
