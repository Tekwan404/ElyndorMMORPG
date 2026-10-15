using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class RaidCombatRosterTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 9, 19, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultRosterRejectsSixParticipants() =>
        Assert.Throws<ArgumentException>(() =>
            new CombatParticipantRoster(Participants(6), UtcNow));

    [Fact]
    public void RaidRosterAllowsTwentyParticipants()
    {
        CombatParticipantRoster roster = new(
            Participants(20),
            UtcNow,
            CombatParticipantLimit.MaximumRaid);

        Assert.Equal(20, roster.Participants.Count);
        Assert.Equal(CombatParticipantLimit.MaximumRaid, roster.MaximumParticipants);
    }

    [Fact]
    public void RaidRosterRejectsTwentyFirstParticipant() =>
        Assert.Throws<ArgumentException>(() =>
            new CombatParticipantRoster(
                Participants(21),
                UtcNow,
                CombatParticipantLimit.MaximumRaid));

    [Fact]
    public void MaximumAboveRaidLimitIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CombatParticipantRoster(
                Participants(1),
                UtcNow,
                CombatParticipantLimit.MaximumRaid + 1));

    [Fact]
    public void FledRaidParticipantCannotReattach()
    {
        Guid characterId = Guid.NewGuid();
        CombatParticipantRoster roster = new(
            [new CombatParticipantIdentity(Guid.NewGuid(), characterId, characterId)],
            UtcNow,
            CombatParticipantLimit.MaximumRaid);
        Assert.True(roster.TryAttach(characterId, UtcNow, out _));
        Assert.True(roster.TryFlee(characterId, UtcNow.AddSeconds(1), out _));

        Assert.False(roster.TryAttach(characterId, UtcNow.AddSeconds(2), out string? errorCode));
        Assert.Equal(CombatParticipantErrorCodes.AlreadyFled, errorCode);
    }

    [Fact]
    public void RaidCombatSessionCapturesTenPlayersInOneRoster()
    {
        CombatParticipantDefinition leader = CreatePlayer(Guid.NewGuid());
        Guid leaderAccountId = Guid.NewGuid();
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 9)
            .Select(_ => CreateRaidPlayerDefinition())
            .ToArray();

        CombatSession session = CreateRaidSession(
            leader,
            leaderAccountId,
            additional);

        Assert.Equal(CombatGroupContext.Raid, session.GroupContext);
        Assert.Equal(10, session.ParticipantRoster.Participants.Count);
        Assert.Equal(10, session.ParticipantRoster.ActiveCount);
        Assert.Equal(CombatParticipantLimit.MaximumRaid, session.ParticipantRoster.MaximumParticipants);
    }

    [Fact]
    public void RaidCombatSessionAllowsFullTwentyPlayerRoster()
    {
        CombatParticipantDefinition leader = CreatePlayer(Guid.NewGuid());
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 19)
            .Select(_ => CreateRaidPlayerDefinition())
            .ToArray();

        CombatSession session = CreateRaidSession(
            leader,
            Guid.NewGuid(),
            additional);

        Assert.Equal(20, session.ParticipantRoster.Participants.Count);
        Assert.Equal(20, session.PlayerActorIds.Count);
    }

    [Fact]
    public void RaidCombatSessionRejectsTwentyFirstPlayer()
    {
        CombatParticipantDefinition leader = CreatePlayer(Guid.NewGuid());
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 20)
            .Select(_ => CreateRaidPlayerDefinition())
            .ToArray();

        Assert.Throws<ArgumentException>(() => CreateRaidSession(
            leader,
            Guid.NewGuid(),
            additional));
    }

    [Fact]
    public void RaidCombatSessionPreservesCharacterIdsWhenActorIdsDiffer()
    {
        Guid leaderActorId = Guid.NewGuid();
        Guid leaderCharacterId = Guid.NewGuid();
        Guid memberActorId = Guid.NewGuid();
        Guid memberCharacterId = Guid.NewGuid();
        CombatParticipantDefinition leader = CreatePlayer(leaderActorId);
        CombatPlayerDefinition member = new(
            Guid.NewGuid(),
            CreatePlayer(memberActorId),
            ResolvedTalentModifiers.Empty,
            CharacterId: memberCharacterId);

        CombatSession session = CreateRaidSession(
            leader,
            Guid.NewGuid(),
            [member],
            leaderCharacterId: leaderCharacterId);

        CombatParticipantSnapshot capturedLeader = Assert.Single(
            session.ParticipantRoster.Participants,
            participant => participant.ActorId == leaderActorId);
        CombatParticipantSnapshot capturedMember = Assert.Single(
            session.ParticipantRoster.Participants,
            participant => participant.ActorId == memberActorId);
        Assert.Equal(leaderCharacterId, capturedLeader.CharacterId);
        Assert.Equal(memberCharacterId, capturedMember.CharacterId);
    }

    [Fact]
    public void OrdinaryCombatSessionStillRejectsSixPlayers()
    {
        CombatParticipantDefinition leader = CreatePlayer(Guid.NewGuid());
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 5)
            .Select(_ => new CombatPlayerDefinition(
                Guid.NewGuid(),
                CreatePlayer(Guid.NewGuid()),
                ResolvedTalentModifiers.Empty))
            .ToArray();

        Assert.Throws<ArgumentException>(() => new CombatSession(
            Guid.NewGuid(),
            leader,
            CreateEnemy(),
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            new MonsterAiProfile("TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            Random(),
            UtcNow,
            playerAccountId: Guid.NewGuid(),
            additionalPlayers: additional));
    }

    [Fact]
    public void RaidGroupEffectAppliesToAllTenActivePlayers()
    {
        const string abilityId = "TEST_RAID_BUFF";
        const string effectId = "TEST_RAID_BUFF_EFFECT";
        EffectDefinition effect = new(
            effectId,
            EffectKind.Buff,
            TimeSpan.FromMinutes(1),
            1,
            EffectStackPolicy.Refresh,
            1);
        AbilityDefinition ability = CreateGroupBuffAbility(abilityId, effect);
        Guid leaderId = Guid.NewGuid();
        CombatParticipantDefinition leader = CreatePlayer(leaderId, abilityId);
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 9)
            .Select(_ => CreateRaidPlayerDefinition())
            .ToArray();
        CombatSession session = CreateRaidSession(
            leader,
            Guid.NewGuid(),
            additional,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [abilityId] = ability
            });

        CombatCommandResult result = session.Handle(
            leaderId,
            new UseAbilityCommand("raid-buff-1", abilityId, Guid.Empty),
            UtcNow);

        Assert.True(result.Succeeded, result.ErrorCode);
        Guid[] affectedPlayerIds = result.Events
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.EffectApplied
                && combatEvent.DefinitionId == effectId
                && combatEvent.TargetActorId.HasValue)
            .Select(combatEvent => combatEvent.TargetActorId!.Value)
            .Distinct()
            .ToArray();
        Assert.Equal(10, affectedPlayerIds.Length);
        Assert.All(session.PlayerActorIds, actorId => Assert.Contains(actorId, affectedPlayerIds));
    }

    private static AbilityDefinition CreateGroupBuffAbility(
        string abilityId,
        EffectDefinition effect) =>
        new(
            abilityId,
            AbilityType.Instant,
            AbilityTargetType.SelfAndPartyMembersInCombat,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "HOLY",
            true,
            true,
            false,
            false,
            [
                new AbilityActionDefinition(
                    AbilityActionType.ApplyEffect,
                    Effect: effect)
            ]);

    private static CombatSession CreateRaidSession(
        CombatParticipantDefinition leader,
        Guid leaderAccountId,
        IReadOnlyList<CombatPlayerDefinition> additionalPlayers,
        IReadOnlyDictionary<string, AbilityDefinition>? abilities = null,
        CombatParticipantDefinition? companion = null,
        Guid? leaderCharacterId = null) =>
        new(
            Guid.NewGuid(),
            leader,
            CreateEnemy(),
            abilities ?? new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            new MonsterAiProfile("TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            Random(),
            UtcNow,
            "TEST_CONTENT",
            "TEST_BALANCE",
            null,
            null,
            companion,
            leaderAccountId,
            leaderCharacterId ?? leader.Actor.ActorId,
            additionalPlayers,
            CombatGroupContext.Raid,
            CombatParticipantLimit.MaximumRaid);

    private static CombatPlayerDefinition CreateRaidPlayerDefinition()
    {
        Guid characterId = Guid.NewGuid();
        return new CombatPlayerDefinition(
            Guid.NewGuid(),
            CreatePlayer(characterId),
            ResolvedTalentModifiers.Empty,
            CharacterId: characterId);
    }

    private static CombatParticipantDefinition CreatePlayer(
        Guid actorId,
        string? knownAbilityId = null)
    {
        CombatStats stats = Stats();
        HashSet<string> knownAbilities = knownAbilityId is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>([knownAbilityId], StringComparer.Ordinal);
        return new CombatParticipantDefinition(
            new CombatActorState(actorId, 500, 500, 100, 100, stats),
            CombatActorKind.Player,
            "WARRIOR",
            $"Player-{actorId:N}",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            knownAbilities,
            CanAutoAttack: false);
    }

    private static CombatParticipantDefinition CreateEnemy()
    {
        Guid actorId = Guid.NewGuid();
        return new CombatParticipantDefinition(
            new CombatActorState(actorId, 1_000, 1_000, 0, 0, Stats()),
            CombatActorKind.Monster,
            "TEST_BOSS",
            "Test Boss",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);
    }

    private static CombatStats Stats() => new(
        Level: 10,
        Accuracy: 100,
        Dodge: 0,
        CriticalChance: 0,
        CriticalDamage: 1,
        Armor: 0,
        MagicResistance: 0,
        ArmorPenetration: 0,
        MagicPenetration: 0);

    private static SequenceGameRandom Random() =>
        new(Enumerable.Repeat(0.5m, 5_000).ToArray());

    private static CombatParticipantIdentity[] Participants(int count) =>
        Enumerable.Range(0, count)
            .Select(_ =>
            {
                Guid characterId = Guid.NewGuid();
                return new CombatParticipantIdentity(
                    Guid.NewGuid(),
                    characterId,
                    characterId);
            })
            .ToArray();
}
