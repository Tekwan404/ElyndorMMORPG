using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class ResurrectionSessionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private readonly Guid _first = Guid.NewGuid();
    private readonly Guid _second = Guid.NewGuid();
    private readonly Guid _ally = Guid.NewGuid();
    private readonly Guid _enemy = Guid.NewGuid();

    [Fact]
    public void DifferentPaladinsCanReviveSuccessiveDeathsWithoutResettingCooldowns()
    {
        var session = Create();
        Assert.True(session.Handle(_ally, new UseConsumableCommand("potion", "POTION",
            [new ResolvedConsumableAction(ConsumableActionType.RestoreResource, 1, ResourceType: "MANA")],
            "MANA_POTION", TimeSpan.FromMinutes(1)), Now).Succeeded);
        Assert.True(session.Handle(_ally, new UseAbilityCommand("die", "FALL", _ally), Now).Succeeded);
        Assert.Equal(CombatParticipantStatus.Dead,
            session.Snapshot(_first).ParticipantRoster!.Single(p => p.ActorId == _ally).Status);
        Assert.True(session.Handle(_first, new UseAbilityCommand("res", "RESURRECTION", _ally), Now).Succeeded);
        session.AdvanceTo(Now.AddSeconds(6));
        var restored = session.Snapshot(_ally);
        Assert.Equal(30, restored.Player.Hp);
        Assert.Equal(20, restored.Player.Resource);
        Assert.Equal(Now.AddMinutes(2), restored.Player.Cooldowns["FALL"]);
        Assert.Equal(Now.AddMinutes(1), restored.Player.ConsumableCooldowns!["MANA_POTION"]);
        Assert.Equal(CombatParticipantStatus.Active,
            restored.ParticipantRoster!.Single(p => p.ActorId == _ally).Status);
        Assert.Null(restored.ParticipantRoster!.Single(p => p.ActorId == _ally).DiedAtUtc);
        Assert.True(session.Handle(_ally, new UseAbilityCommand("die-again", "FALL_AGAIN", _ally), Now.AddSeconds(7)).Succeeded);
        Assert.False(session.Handle(_first, new UseAbilityCommand("personal-cd", "RESURRECTION", _ally), Now.AddSeconds(7)).Succeeded);
        Assert.True(session.Handle(_second, new UseAbilityCommand("second-res", "RESURRECTION", _ally), Now.AddSeconds(7)).Succeeded);
        session.AdvanceTo(Now.AddSeconds(13));
        Assert.Equal(30, session.Snapshot(_ally).Player.Hp);
        Assert.Equal(2, session.GetEventsAfter(0).Count(e => e.Type == CombatEventType.ActorResurrected));
        Assert.Equal(2, session.GetEventsAfter(0).Count(e => e.Type == CombatEventType.ActorDied && e.ActorId == _ally));
    }

    [Fact]
    public void DuplicateResurrectionCommandDoesNotChargeTwiceAndCannotReopenWipe()
    {
        var session = Create();
        session.Handle(_ally, new UseAbilityCommand("die", "FALL", _ally), Now);
        Assert.True(session.Handle(_first, new UseAbilityCommand("res", "RESURRECTION", _ally), Now).Succeeded);
        session.Handle(_first, new UseAbilityCommand("res", "RESURRECTION", _ally), Now);
        Assert.Equal(920, session.Snapshot(_first).Player.Resource);
        session.Handle(_second, new UseAbilityCommand("die-second", "FALL", _second), Now.AddSeconds(1));
        session.Handle(_first, new UseAbilityCommand("die-first", "FALL", _first), Now.AddSeconds(1));
        session.AdvanceTo(Now.AddSeconds(7));
        Assert.Equal(CombatSessionStatus.Defeat, session.Snapshot(_ally).Status);
        Assert.DoesNotContain(session.GetEventsAfter(0), e => e.Type == CombatEventType.ActorResurrected);
    }

    [Fact]
    public void EnemyOutsiderSelfAndLivingAllyAreRejectedBeforeSpending()
    {
        var session = Create();
        foreach (Guid target in new[] { _enemy, Guid.NewGuid(), _first, _ally })
        {
            var result = session.Handle(_first,
                new UseAbilityCommand(Guid.NewGuid().ToString(), "RESURRECTION", target), Now);
            Assert.False(result.Succeeded);
            Assert.Equal(1000, result.Snapshot.Player.Resource);
            Assert.Empty(result.Snapshot.Player.Cooldowns);
        }
    }

    [Fact]
    public void RevivalSkipsResourceRegenerationDuringDeadTime()
    {
        var session = Create(allyRegen: 10);
        session.Handle(_ally, new UseAbilityCommand("die", "FALL", _ally), Now);
        session.Handle(_first, new UseAbilityCommand("res", "RESURRECTION", _ally), Now);
        session.AdvanceTo(Now.AddSeconds(6));
        Assert.Equal(20, session.Snapshot(_ally).Player.Resource);
        session.AdvanceTo(Now.AddSeconds(7));
        Assert.Equal(30, session.Snapshot(_ally).Player.Resource);
    }

    [Fact]
    public void DeadCasterRevivalDoesNotResumeItsOldResurrectionCast()
    {
        var session = Create();
        session.Handle(_ally, new UseAbilityCommand("die-ally", "FALL", _ally), Now);
        session.Handle(_first, new UseAbilityCommand("old-res", "RESURRECTION", _ally), Now);
        Assert.True(session.Handle(_first, new UseAbilityCommand("die-caster", "FALL", _first), Now.AddSeconds(1)).Succeeded);
        Assert.True(session.Handle(_second, new UseAbilityCommand("revive-caster", "RESURRECTION", _first), Now.AddSeconds(2)).Succeeded);
        session.AdvanceTo(Now.AddSeconds(10));
        Assert.True(session.Snapshot(_first).Player.Hp > 0);
        Assert.Equal(0, session.Snapshot(_ally).Player.Hp);
        Assert.Null(session.Snapshot(_first).Player.ActiveCast);
        Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.ActorResurrected);
    }

    [Fact]
    public void InterruptPreventsResurrection()
    {
        var runtime = new CombatRuntimeState(CombatActorState.CreateDummy(100, maxResource: 1000));
        var ally = CombatActorState.CreateDummy(100);
        ally.SetCurrentHp(0);
        runtime.AddActor(ally);
        Assert.True(AbilityEngine.Execute(runtime, ResurrectionAbilityTests.Resurrection,
            new("cast", "RESURRECTION", ally.ActorId), Now).Succeeded);
        Assert.True(AbilityEngine.Interrupt(runtime, Now.AddSeconds(3), TimeSpan.FromSeconds(2)).Succeeded);
        Assert.Equal(AbilityErrorCode.NoActiveCast, AbilityEngine.CompleteCast(runtime, Now.AddSeconds(6)).ErrorCode);
        Assert.True(ally.IsDead);
        Assert.Equal(920, runtime.Actor.CurrentResource);
    }

    [Fact]
    public void SoloArenaAcceptsAbilityButHasNoEligibleDeadAlly()
    {
        var ability = ResurrectionAbilityTests.Resurrection;
        ArenaFighter Fighter(Guid id) => ArenaFighterAssembler.Create(
            new CombatPlayerDefinition(Guid.NewGuid(), Player(id) with
                { KnownAbilityIds = new HashSet<string> { ability.Id } }, ResolvedTalentModifiers.Empty),
            1, new Dictionary<string, AbilityDefinition> { [ability.Id] = ability }, false).Fighter;
        var first = Fighter(_first);
        var second = Fighter(_second);
        var arena = new ArenaCombatSession(Guid.NewGuid(), first, second, new SeededGameRandom(1), Now);
        var result = arena.UseAbility(first.AccountId, "res", ability.Id, second.Actor.ActorId, Now);
        Assert.False(result.Succeeded);
        Assert.Equal(1000, first.Actor.CurrentResource);
    }

    private static CombatParticipantDefinition Player(Guid id) => new(
        new CombatActorState(id, 100, 100, 1000, 1000, CombatStats.Default),
        CombatActorKind.Player, "PALADIN", "Paladin", "MANA",
        new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
        new HashSet<string> { "RESURRECTION", "FALL", "FALL_AGAIN" }, CanAutoAttack: false);

    private CombatSession Create(decimal allyRegen = 0)
    {
        var fall = new AbilityDefinition("FALL", AbilityType.Instant, AbilityTargetType.Self,
            0, TimeSpan.FromMinutes(2), TimeSpan.Zero, false, GlobalCooldownCategory.None,
            false, "PHYSICAL", CanUseWhileCasting: true, Actions: [new(AbilityActionType.Damage, 1000,
                DamageType.Physical, CanMiss: false, CanCrit: false, CanDodge: false)]);
        var abilities = new Dictionary<string, AbilityDefinition>
        {
            [fall.Id] = fall,
            ["FALL_AGAIN"] = fall with { Id = "FALL_AGAIN" },
            ["RESURRECTION"] = ResurrectionAbilityTests.Resurrection
        };
        var enemy = new CombatParticipantDefinition(
            new CombatActorState(_enemy, 10000, 10000, 0, 0, CombatStats.Default),
            CombatActorKind.Monster, "TEST_ENEMY", "Enemy", "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0), new HashSet<string>());
        return new CombatSession(Guid.NewGuid(), Player(_first), enemy, abilities,
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty,
            new SeededGameRandom(1), Now, additionalPlayers:
            [new(Guid.NewGuid(), Player(_second), ResolvedTalentModifiers.Empty),
             new(Guid.NewGuid(), Player(_ally) with
             { Actor = new CombatActorState(_ally, 100, 100, 100, 90, CombatStats.Default),
               ResourceRegenPerSecond = allyRegen }, ResolvedTalentModifiers.Empty)]);
    }
}
