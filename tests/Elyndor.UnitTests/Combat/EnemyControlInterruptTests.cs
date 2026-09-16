using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class EnemyControlInterruptTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid BossId =
        Guid.Parse("72000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("CONCUSSION_BLOW")]
    [InlineData("HAMMER_OF_JUSTICE")]
    public void ControlStrikeInterruptsActiveBossCastWithoutRuntimeParameter(string interruptAbilityId)
    {
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["BOSS_CAST"] = new AbilityDefinition(
                "BOSS_CAST",
                AbilityType.Casted,
                AbilityTargetType.SingleEnemy,
                0,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(4),
                false,
                GlobalCooldownCategory.None,
                true,
                "ARCANE",
                Interruptible: true,
                Actions: []),
            [interruptAbilityId] = new AbilityDefinition(
                interruptAbilityId,
                AbilityType.Instant,
                AbilityTargetType.SingleEnemy,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                false,
                "PHYSICAL",
                Actions: [])
        };

        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 100, Stats()),
            CombatActorKind.Player,
            interruptAbilityId == "HAMMER_OF_JUSTICE" ? "PALADIN" : "WARRIOR",
            "Tester",
            interruptAbilityId == "HAMMER_OF_JUSTICE" ? "MANA" : "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>([interruptAbilityId], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition boss = new(
            new CombatActorState(BossId, 2_000, 2_000, 0, 0, Stats()),
            CombatActorKind.Monster,
            "INTERRUPT_TEST_BOSS",
            "Interrupt Test Boss",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromMilliseconds(100), 0, 0, 0),
            new HashSet<string>(["BOSS_CAST"], StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);
        CombatSession session = new(
            Guid.Parse("70000000-0000-0000-0000-000000000001"),
            player,
            boss,
            abilities,
            new MonsterAiProfile("INTERRUPT_TEST_AI", ["BOSS_CAST"]),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 64).ToArray()),
            Now);

        CombatCommandResult casting = session.AdvanceTo(Now.AddMilliseconds(100));
        Assert.NotNull(casting.Snapshot.Enemy.ActiveCast);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("interrupt", interruptAbilityId, BossId),
            Now.AddMilliseconds(200));

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Null(result.Snapshot.Enemy.ActiveCast);
        Assert.Contains(result.Events, combatEvent =>
            combatEvent.Type == CombatEventType.AbilityInterrupted
            && combatEvent.TargetActorId == BossId);
    }

    private static CombatStats Stats() =>
        new(
            Level: 25,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 0,
            SpellPower: 0);
}
