using Elyndor.Core.Combat;
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
    public void DefaultRoster_RejectsSixParticipants() =>
        Assert.Throws<ArgumentException>(() =>
            new CombatParticipantRoster(Participants(6), UtcNow));

    [Fact]
    public void RaidRoster_AllowsTwentyParticipants()
    {
        CombatParticipantRoster roster = new(
            Participants(20),
            UtcNow,
            CombatParticipantLimit.MaximumRaid);

        Assert.Equal(20, roster.Participants.Count);
        Assert.Equal(CombatParticipantLimit.MaximumRaid, roster.MaximumParticipants);
    }

    [Fact]
    public void RaidRoster_RejectsTwentyFirstParticipant() =>
        Assert.Throws<ArgumentException>(() =>
            new CombatParticipantRoster(
                Participants(21),
                UtcNow,
                CombatParticipantLimit.MaximumRaid));

    [Fact]
    public void MaximumAboveRaidLimit_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CombatParticipantRoster(
                Participants(1),
                UtcNow,
                CombatParticipantLimit.MaximumRaid + 1));

    [Fact]
    public void FledRaidParticipant_CannotReattach()
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
    public void RaidCombatSession_CapturesTenPlayersInOneRoster()
    {
        CombatParticipantDefinition leader = CreatePlayer(Guid.NewGuid());
        Guid leaderAccountId = Guid.NewGuid();
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 9)
            .Select(_ => new CombatPlayerDefinition(
                Guid.NewGuid(),
                CreatePlayer(Guid.NewGuid()),
                ResolvedTalentModifiers.Empty))
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
    public void RaidCombatSession_AllowsFullTwentyPlayerRoster()
    {
        CombatParticipantDefinition leader = CreatePlayer(Guid.NewGuid());
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 19)
            .Select(_ => new CombatPlayerDefinition(
                Guid.NewGuid(),
                CreatePlayer(Guid.NewGuid()),
                ResolvedTalentModifiers.Empty))
            .ToArray();

        CombatSession session = CreateRaidSession(
            leader,
            Guid.NewGuid(),
            additional);

        Assert.Equal(20, session.ParticipantRoster.Participants.Count);
        Assert.Equal(20, session.PlayerActorIds.Count);
    }

    [Fact]
    public void RaidCombatSession_RejectsTwentyFirstPlayer()
    {
        CombatParticipantDefinition leader = CreatePlayer(Guid.NewGuid());
        CombatPlayerDefinition[] additional = Enumerable.Range(0, 20)
            .Select(_ => new CombatPlayerDefinition(
                Guid.NewGuid(),
                CreatePlayer(Guid.NewGuid()),
                ResolvedTalentModifiers.Empty))
            .ToArray();

        Assert.Throws<ArgumentException>(() => CreateRaidSession(
            leader,
            Guid.NewGuid(),
            additional));
    }

    [Fact]
    public void OrdinaryCombatSession_StillRejectsSixPlayers()
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
            new Dictionary<string, Elyndor.Core.Combat.Abilities.AbilityDefinition>(StringComparer.Ordinal),
            new MonsterAiProfile("TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            Random(),
            UtcNow,
            playerAccountId: Guid.NewGuid(),
            additionalPlayers: additional));
    }

    private static CombatSession CreateRaidSession(
        CombatParticipantDefinition leader,
        Guid leaderAccountId,
        IReadOnlyList<CombatPlayerDefinition> additionalPlayers) =>
        new(
            Guid.NewGuid(),
            leader,
            CreateEnemy(),
            new Dictionary<string, Elyndor.Core.Combat.Abilities.AbilityDefinition>(StringComparer.Ordinal),
            new MonsterAiProfile("TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            Random(),
            UtcNow,
            "TEST_CONTENT",
            "TEST_BALANCE",
            null,
            null,
            null,
            leaderAccountId,
            additionalPlayers,
            CombatGroupContext.Raid,
            CombatParticipantLimit.MaximumRaid);

    private static CombatParticipantDefinition CreatePlayer(Guid actorId)
    {
        CombatStats stats = Stats();
        return new CombatParticipantDefinition(
            new CombatActorState(actorId, 500, 500, 100, 100, stats),
            CombatActorKind.Player,
            "WARRIOR",
            $"Player-{actorId:N}",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
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
