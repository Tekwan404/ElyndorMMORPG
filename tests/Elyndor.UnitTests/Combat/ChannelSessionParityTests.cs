using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class ChannelSessionParityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(false, EffectKind.Silence)]
    [InlineData(true, EffectKind.Silence)]
    [InlineData(false, EffectKind.Stun)]
    [InlineData(true, EffectKind.Stun)]
    public void PveShortControlBetweenTicksCancelsChannelWithCoarseOrFineAdvances(bool fine, EffectKind kind)
    {
        var (player, enemy, abilities) = Create(kind, .25);
        var session = new CombatSession(Guid.NewGuid(), player, enemy, abilities,
            new MonsterAiProfile("CONTROL_AI", ["CONTROL"]), ResolvedTalentModifiers.Empty, Random(), Now);
        Assert.True(session.Handle(new UseAbilityCommand("channel", "CHANNEL", enemy.Actor.ActorId), Now).Succeeded);
        if (fine)
            for (int step = 1; step < 40; step++) session.AdvanceTo(Now.AddMilliseconds(step * 100));
        session.AdvanceTo(Now.AddSeconds(4));
        Assert.Equal(9975, enemy.Actor.CurrentHp);
        Assert.Null(session.Snapshot().Player.ActiveCast);
        Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.AbilityInterrupted && e.DefinitionId == "CHANNEL");
    }

    [Theory]
    [InlineData(EffectKind.Silence)]
    [InlineData(EffectKind.Stun)]
    public void ArenaControlAtCommittedTickCancelsAllFutureTicks(EffectKind kind)
    {
        var (player, enemy, abilities) = Create(kind, 1);
        ArenaFighter Assemble(CombatParticipantDefinition participant) => ArenaFighterAssembler.Create(
            new CombatPlayerDefinition(Guid.NewGuid(), participant, ResolvedTalentModifiers.Empty),
            1, abilities, hasCompanion: false).Fighter;
        var mage = Assemble(player);
        var opponent = Assemble(enemy with { Kind = CombatActorKind.Player, DefinitionId = "MAGE" });
        var session = new ArenaCombatSession(Guid.NewGuid(), mage, opponent, Random(), Now);
        Assert.True(session.UseAbility(mage.AccountId, "channel", "CHANNEL", enemy.Actor.ActorId, Now).Succeeded);
        Assert.True(session.UseAbility(opponent.AccountId, "control", "CONTROL", player.Actor.ActorId, Now).Succeeded);
        session.AdvanceTo(Now.AddSeconds(4));
        Assert.Equal(9975, enemy.Actor.CurrentHp);
        Assert.Null(session.ActiveCastFor(mage.AccountId));
        Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.AbilityInterrupted && e.DefinitionId == "CHANNEL");
    }

    [Fact]
    public void PveChannelSnapshotAndRepeatedAdvancePreserveTickCursor()
    {
        var (player, enemy, abilities) = Create(EffectKind.Silence, .25);
        var session = new CombatSession(Guid.NewGuid(), player, enemy with { KnownAbilityIds = new HashSet<string>() },
            abilities, new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty, Random(), Now);
        Assert.True(session.Handle(new UseAbilityCommand("channel", "CHANNEL", enemy.Actor.ActorId), Now).Succeeded);
        session.AdvanceTo(Now.AddSeconds(2));
        var cast = session.Snapshot().Player.ActiveCast!;
        Assert.True(cast.IsChannelled);
        Assert.Equal(2, cast.CompletedTicks);
        Assert.Equal(4, cast.TotalTicks);
        session.AdvanceTo(Now.AddSeconds(2));
        Assert.Equal(9950, enemy.Actor.CurrentHp);
        session.AdvanceTo(Now.AddSeconds(4));
        Assert.Equal(9900, enemy.Actor.CurrentHp);
        Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.AbilityCompleted && e.DefinitionId == "CHANNEL");
    }

    private static (CombatParticipantDefinition Player, CombatParticipantDefinition Enemy,
        IReadOnlyDictionary<string, AbilityDefinition> Abilities) Create(EffectKind control, double controlCast)
    {
        CombatParticipantDefinition Participant(string id, string known, TimeSpan interval) => new(
            new CombatActorState(Guid.NewGuid(), 10000, 10000, 1000, 1000, CombatStats.Default),
            id == "MAGE" ? CombatActorKind.Player : CombatActorKind.Monster, id, id, "MANA",
            new(interval, 0, 0, 0), new HashSet<string>([known]), CanAutoAttack: false);
        var channel = new AbilityDefinition("CHANNEL", AbilityType.Channelled, AbilityTargetType.SingleEnemy,
            20, TimeSpan.Zero, TimeSpan.FromSeconds(4), false, GlobalCooldownCategory.None, true, "ARCANE",
            Actions: [new(AbilityActionType.Damage, 25, DamageType.True, CanMiss: false, CanCrit: false, CanDodge: false)],
            ChannelTickInterval: TimeSpan.FromSeconds(1));
        var stop = new AbilityDefinition("CONTROL", AbilityType.Casted, AbilityTargetType.SingleEnemy,
            0, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(controlCast), false, GlobalCooldownCategory.None, true, "FROST",
            Actions: [new(AbilityActionType.ApplyEffect, Effect: new("SHORT_CONTROL", control, TimeSpan.FromSeconds(.25),
                1, EffectStackPolicy.Replace, 0))]);
        return (Participant("MAGE", "CHANNEL", TimeSpan.FromHours(1)), Participant("ENEMY", "CONTROL", TimeSpan.FromSeconds(1)),
            new Dictionary<string, AbilityDefinition> { [channel.Id] = channel, [stop.Id] = stop });
    }

    private static SequenceGameRandom Random() => new(Enumerable.Repeat(.99m, 100).ToArray());
}
