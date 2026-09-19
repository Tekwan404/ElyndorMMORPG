using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Combat.SetPassives;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class SetPassiveCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("82000000-0000-0000-0000-000000000001");

    [Fact]
    public void EquippedSetPiecesDriveTheBlockPassiveThroughTheSession()
    {
        CombatSession session = CreateSession(equippedGuardianPieces: 2);

        CombatCommandResult result = session.AdvanceTo(Now.AddSeconds(1));

        Assert.Contains(
            result.Events,
            item => item.Type == CombatEventType.DamageBlocked);
        Assert.Contains(
            result.Events,
            item => item.Type is CombatEventType.EffectApplied or CombatEventType.EffectRefreshed
                && item.DefinitionId == "EFFECT_GUARDIAN_BLOCK_ARMOR");
    }

    [Fact]
    public void PassiveStaysSilentBelowTheRequiredPieceCount()
    {
        CombatSession session = CreateSession(equippedGuardianPieces: 1);

        CombatCommandResult result = session.AdvanceTo(Now.AddSeconds(1));

        Assert.Contains(
            result.Events,
            item => item.Type == CombatEventType.DamageBlocked);
        Assert.DoesNotContain(
            result.Events,
            item => item.DefinitionId == "EFFECT_GUARDIAN_BLOCK_ARMOR");
    }

    private static CombatSession CreateSession(int equippedGuardianPieces)
    {
        CombatStats playerStats = new(
            Level: 20,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 100,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 0,
            SpellPower: 0,
            BlockChance: 60,
            BlockValueMin: 100,
            BlockValueMax: 100);
        CombatStats enemyStats = new(20, 100, 0, 0, 1, 0, 0, 0, 0, 0);

        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 0, playerStats),
            CombatActorKind.Player,
            "WARRIOR",
            "Guardian",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false,
            EquippedSetPieces: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [SetPassiveCatalog.AncientMineGuardianSetId] = equippedGuardianPieces
            });
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 10_000, 10_000, 0, 0, enemyStats),
            CombatActorKind.Monster,
            "SET_PASSIVE_TEST",
            "Set Passive Test",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(1), 50, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));

        return new CombatSession(
            Guid.CreateVersion7(),
            player,
            enemy,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            new MonsterAiProfile("SET_PASSIVE_TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(0.99m, 0.99m, 0m),
            Now);
    }
}
