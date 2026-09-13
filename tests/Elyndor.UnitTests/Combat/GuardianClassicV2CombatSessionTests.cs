using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class GuardianClassicV2CombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 20, 20, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(MonsterRank.Normal, true)]
    [InlineData(MonsterRank.Elite, true)]
    [InlineData(MonsterRank.Boss, false)]
    public void ConcussionBlowRespectsBossStunImmunity(
        MonsterRank rank,
        bool shouldStun)
    {
        Guid playerId = Guid.Parse("91000000-0000-0000-0000-000000000001");
        Guid enemyId = Guid.Parse("92000000-0000-0000-0000-000000000001");
        CombatStats stats = new(
            Level: 20,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 100);
        CombatParticipantDefinition player = new(
            new CombatActorState(playerId, 1_000, 1_000, 100, 100, stats),
            CombatActorKind.Player,
            "WARRIOR",
            "Guardian",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(["CONCUSSION_BLOW"], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(enemyId, 10_000, 10_000, 0, 0, stats),
            CombatActorKind.Monster,
            $"TEST_{rank.ToString().ToUpperInvariant()}",
            rank.ToString(),
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false,
            MonsterRank: rank);
        AbilityDefinition concussionBlow = new(
            "CONCUSSION_BLOW",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            15,
            TimeSpan.FromSeconds(30),
            TimeSpan.Zero,
            true,
            GlobalCooldownCategory.Standard,
            false,
            "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    DamageType: Elyndor.Core.Combat.Damage.DamageType.Physical,
                    AttackPowerCoefficient: 0.45m)
            ]);
        ResolvedTalentModifiers talents = ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = new HashSet<string>(["CONCUSSION_BLOW"], StringComparer.Ordinal)
        };
        CombatSession session = new(
            Guid.CreateVersion7(),
            player,
            enemy,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [concussionBlow.Id] = concussionBlow
            },
            new MonsterAiProfile("TEST_AI", []),
            talents,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 20).ToArray()),
            Now);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("concussion", concussionBlow.Id, enemyId),
            Now.AddMilliseconds(100));

        Assert.True(result.Succeeded);
        Assert.Equal(
            shouldStun,
            result.Snapshot.Enemy.Effects.Any(effect =>
                effect.Id == "GUARDIAN_CONCUSSION_BLOW_STUN"));
    }
}
