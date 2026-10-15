using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatParticipantOwnershipTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void ConsumableValidationUsesRequestingMembersHealthAndCooldowns()
    {
        Fight fight = Create([], []);
        fight.Member!.ApplyDamage(500);
        ResolvedConsumableAction[] actions = [new(ConsumableActionType.RestoreHp, 100)];
        Assert.Null(fight.Session.ValidateConsumableUse(fight.Member.ActorId, Now, actions, "HEAL", TimeSpan.FromSeconds(30)));
        Assert.Equal(CombatErrorCodes.ConsumableNotNeeded,
            fight.Session.ValidateConsumableUse(fight.Owner.ActorId, Now, actions, "HEAL", TimeSpan.FromSeconds(30)));
        Assert.True(fight.Session.Handle(fight.Member.ActorId,
            new UseConsumableCommand("member-heal", "TEST_POTION", actions, "HEAL", TimeSpan.FromSeconds(30)), Now).Succeeded);
        fight.Owner.ApplyDamage(100);
        Assert.Null(fight.Session.ValidateConsumableUse(fight.Owner.ActorId, Now, actions, "HEAL", TimeSpan.FromSeconds(30)));
        Assert.Equal(CombatErrorCodes.ConsumableOnCooldown,
            fight.Session.ValidateConsumableUse(fight.Member.ActorId, Now, actions, "HEAL", TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void ReplayedCommandReturnsOriginalOutcomeWithoutApplyingDamageAgain()
    {
        Fight fight = Create([], []);
        UseAbilityCommand command = new("replay", "SHOT", fight.Enemies[0].ActorId);
        CombatCommandResult first = fight.Session.Handle(fight.Owner.ActorId, command, Now);
        decimal hp = fight.Enemies[0].CurrentHp;
        CombatCommandResult replay = fight.Session.Handle(fight.Owner.ActorId, command, Now);
        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(hp, fight.Enemies[0].CurrentHp);
        Assert.Empty(replay.Events);
        Assert.Equal(fight.Owner.ActorId, replay.Snapshot.Player.ActorId);
    }

    [Fact]
    public void RejectedCommandReplaysItsOriginalReason()
    {
        Fight fight = Create([], []);
        UseAbilityCommand command = new("rejected", "UNKNOWN", fight.Enemies[0].ActorId);
        CombatCommandResult first = fight.Session.Handle(fight.Owner.ActorId, command, Now);
        CombatCommandResult replay = fight.Session.Handle(fight.Owner.ActorId, command, Now);
        Assert.Equal(CombatErrorCodes.AbilityNotKnown, first.ErrorCode);
        Assert.Equal(first.ErrorCode, replay.ErrorCode);
    }

    [Fact]
    public void CommandHistoryIsBoundedPerParticipantAndKeepsOldReplaySafe()
    {
        Fight fight = Create([], []);
        for (int index = 0; index < 16384; index++)
            Assert.True(fight.Session.Handle(fight.Owner.ActorId, new StopAutoAttackCommand($"stop-{index}"), Now).Succeeded);
        CombatCommandResult full = fight.Session.Handle(fight.Owner.ActorId, new StopAutoAttackCommand("over-limit"), Now);
        Assert.Equal("combat_command_limit", full.ErrorCode);
        Assert.True(fight.Session.Handle(fight.Owner.ActorId, new StopAutoAttackCommand("stop-0"), Now).Succeeded);
        Assert.True(fight.Session.Handle(fight.Member!.ActorId, new StopAutoAttackCommand("member-stop"), Now).Succeeded);
        Assert.Equal(CombatErrorCodes.DuplicateCommand,
            fight.Session.Handle(fight.Member.ActorId, new StopAutoAttackCommand("stop-0"), Now).ErrorCode);
        Assert.True(fight.Session.Handle(fight.Owner.ActorId, new FleeCommand("escape"), Now).Succeeded);
        Assert.True(fight.Session.Handle(fight.Owner.ActorId, new FleeCommand("escape"), Now).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")]
    public void InvalidCommandIdsAreRejectedBeforeMutation(string commandId)
    {
        Fight fight = Create([], []);
        decimal hp = fight.Enemies[0].CurrentHp;
        CombatCommandResult result = fight.Session.Handle(fight.Owner.ActorId,
            new UseAbilityCommand(commandId, "SHOT", fight.Enemies[0].ActorId), Now);
        Assert.Equal("combat_command_id_invalid", result.ErrorCode);
        Assert.Equal(hp, fight.Enemies[0].CurrentHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachArchersMarkedShotsCountOnlyTheirOwnHits(bool reverse)
    {
        ResolvedTalentEventHook hook = new("M-9-1", TalentModifierKeys.OnAbilityUsed, 1, 60,
            "MASTER_ARROW_EXTRA", TimeSpan.Zero, false, TriggerCount: 5);
        Fight fight = Create([hook], [hook], reverse: reverse);
        foreach (CombatActorState actor in new[] { fight.Owner, fight.Member! })
            EffectEngine.Apply(fight.Enemies[0], actor.ActorId,
                new EffectDefinition("ARCHER_HUNTER_MARK", EffectKind.Debuff,
                    TimeSpan.FromHours(1), 1, EffectStackPolicy.Replace, 0, SourceSpecific: true), Now);
        for (int index = 0; index < 4; index++) Shot(fight, fight.Owner, $"owner-{index}");
        Shot(fight, fight.Member!, "member-1");
        Assert.Empty(ExtraArrows(fight));
        Shot(fight, fight.Owner, "owner-5");
        Assert.Equal(fight.Owner.ActorId, Assert.Single(ExtraArrows(fight)).SourceActorId);
        for (int index = 2; index <= 5; index++) Shot(fight, fight.Member!, $"member-{index}");
        Assert.Equal(new[] { fight.Owner.ActorId, fight.Member!.ActorId },
            ExtraArrows(fight).Select(item => item.SourceActorId!.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachLowHpBerserkerReceivesOneDeathsEmbrace(bool reverse)
    {
        ResolvedTalentEventHook hook = new("B-8-3", TalentModifierKeys.OnHpThreshold, 1, 200,
            null, TimeSpan.Zero, false, Threshold: 10);
        Fight fight = Create([hook], [hook], reverse: reverse, classId: "WARRIOR", autoAttack: true);
        fight.Owner.ApplyDamage(950);
        fight.Member!.ApplyDamage(950);
        fight.Session.AdvanceTo(Now);
        CombatEvent[] criticals = fight.Session.GetEventsAfter(0).Where(item =>
            item.Type == CombatEventType.DamageDealt && item.IsCritical).ToArray();
        Assert.Equal(2, criticals.Length);
        Assert.Contains(criticals, item => item.SourceActorId == fight.Owner.ActorId);
        Assert.Contains(criticals, item => item.SourceActorId == fight.Member.ActorId);
        fight.Session.AdvanceTo(Now.AddHours(1));
        Assert.Equal(2, fight.Session.GetEventsAfter(0).Count(item =>
            item.Type == CombatEventType.DamageDealt && item.IsCritical));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnotherArcherCannotResetOwnersSpiritBond(bool reverse)
    {
        ResolvedTalentEventHook hook = new("B-4-2", TalentModifierKeys.OnPartyEvent, 2, 2,
            "SPIRIT_BOND", TimeSpan.FromSeconds(10), false);
        Fight fight = Create([hook], [], reverse: reverse, companion: true);
        fight.Owner.ApplyDamage(500);
        fight.Companion!.ApplyDamage(500);
        for (int second = 0; second <= 11; second++) fight.Session.AdvanceTo(Now.AddSeconds(second));
        Assert.Equal(520, fight.Owner.CurrentHp);
        Assert.Equal(520, fight.Companion.CurrentHp);
        Assert.Equal(1000, fight.Member!.CurrentHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionFollowsOwnersTargetRegardlessOfRosterOrder(bool reverse)
    {
        Fight fight = Create([], [], reverse: reverse, companion: true, twoEnemies: true);
        Assert.True(fight.Session.Handle(fight.Owner.ActorId,
            new SelectTargetCommand("owner-target", fight.Enemies[0].ActorId), Now).Succeeded);
        Assert.True(fight.Session.Handle(fight.Member!.ActorId,
            new SelectTargetCommand("member-target", fight.Enemies[1].ActorId), Now).Succeeded);
        fight.Session.AdvanceTo(Now.AddSeconds(1));
        CombatEvent hit = Assert.Single(fight.Session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.DamageDealt && item.SourceActorId == fight.Companion!.ActorId);
        Assert.Equal(fight.Enemies[0].ActorId, hit.TargetActorId);
    }

    private static IEnumerable<CombatEvent> ExtraArrows(Fight fight) =>
        fight.Session.GetEventsAfter(0).Where(item => item.Type == CombatEventType.DamageDealt
            && item.ProcOriginId == "M-9-1");

    private static void Shot(Fight fight, CombatActorState actor, string id) =>
        Assert.True(fight.Session.Handle(actor.ActorId,
            new UseAbilityCommand(id, "SHOT", fight.Enemies[0].ActorId), Now).Succeeded);

    private static Fight Create(ResolvedTalentEventHook[] ownerHooks, ResolvedTalentEventHook[] memberHooks,
        bool reverse = false, string classId = "ARCHER", bool autoAttack = false,
        bool companion = false, bool twoEnemies = false)
    {
        int actorIndex = 2;
        static Guid ActorId(int index) => Guid.Parse($"00000000-0000-0000-0000-{index:D12}");
        CombatStats stats = CombatStats.Default with { Accuracy = 100, AttackPower = 20 };
        CombatParticipantDefinition Player(int index) => new(
            new CombatActorState(ActorId(index), 1000, 1000, 100, 100, stats),
            CombatActorKind.Player, classId, "Player", "FOCUS", new(TimeSpan.FromHours(1), 10, 0, 0),
            new HashSet<string>(StringComparer.Ordinal) { "SHOT" }, CanAutoAttack: autoAttack);
        CombatParticipantDefinition owner = Player(reverse ? 2 : 1);
        CombatParticipantDefinition member = Player(reverse ? 1 : 2);
        CombatParticipantDefinition Enemy() => new(
            new CombatActorState(ActorId(++actorIndex), 10000, 10000, 0, 0, stats),
            CombatActorKind.Monster, "TEST_ENEMY", "Enemy", "NONE", new(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal), CanAutoAttack: false);
        CombatParticipantDefinition[] enemies = twoEnemies ? [Enemy(), Enemy()] : [Enemy()];
        CombatParticipantDefinition? pet = companion ? new(
            new CombatActorState(ActorId(++actorIndex), 1000, 1000, 0, 0, stats),
            CombatActorKind.Companion, "ARCHER_STARTER_PREDATOR", "Pet", "NONE", new(TimeSpan.FromSeconds(1), 10, 0, 0),
            new HashSet<string>(StringComparer.Ordinal)) : null;
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["SHOT"] = new("SHOT", AbilityType.Instant, AbilityTargetType.SingleEnemy, 0,
                TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "PHYSICAL",
                Actions: [new(AbilityActionType.Damage, 10, CanMiss: false, CanDodge: false, CanCrit: false)])
        };
        CombatSession session = new(Guid.NewGuid(), owner, enemies, abilities, new MonsterAiProfile("PASSIVE", []),
            ResolvedTalentModifiers.Empty with { EventHooks = ownerHooks }, new SeededGameRandom(1), Now,
            companion: pet, additionalPlayers: [new(Guid.NewGuid(), member,
                ResolvedTalentModifiers.Empty with { EventHooks = memberHooks })]);
        return new(session, owner.Actor, member.Actor, enemies.Select(enemy => enemy.Actor).ToArray(), pet?.Actor);
    }

    private sealed record Fight(CombatSession Session, CombatActorState Owner, CombatActorState? Member,
        CombatActorState[] Enemies, CombatActorState? Companion);
}
