using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class MorEtPhaseGuardTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid BossId =
        Guid.Parse("82000000-0000-0000-0000-000000000001");

    [Fact]
    public void LethalOpeningHitCannotSkipRequiredSecondSoulWave()
    {
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["NUKE"] = DamageAbility("NUKE", 5_000),
            ["PING"] = DamageAbility("PING", 100)
        };
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 2_000, 2_000, 100, 100, Stats()),
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(abilities.Keys, StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition boss = new(
            new CombatActorState(BossId, 1_000, 1_000, 0, 0, Stats()),
            CombatActorKind.Monster,
            "MOR_ET_BOSS",
            "Mor-Et",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);
        CombatSession session = new(
            Guid.Parse("80000000-0000-0000-0000-000000000001"),
            player,
            boss,
            abilities,
            new MonsterAiProfile("MOR_ET_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 128).ToArray()),
            Now);

        EncounterEnemyProfile soulProfile = SoulProfile();
        session.ConfigureMorEtEncounter(new MorEtCombatEncounterProfile(
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                ["WARRIOR"] = soulProfile,
                ["MAGE"] = soulProfile,
                ["ARCHER"] = soulProfile,
                ["PALADIN"] = soulProfile
            }));

        CombatCommandResult firstHit = session.Handle(
            new UseAbilityCommand("opening-overkill", "NUKE", BossId),
            Now);
        Assert.Equal(1, Boss(firstHit.Snapshot).Hp);
        CombatActorSnapshot firstSoul = AliveSoul(firstHit.Snapshot);

        CombatCommandResult firstSoulKilled = session.Handle(
            new UseAbilityCommand("first-soul", "NUKE", firstSoul.ActorId),
            Now.AddMilliseconds(1));
        Assert.Contains(Boss(firstSoulKilled.Snapshot).Effects, effect =>
            effect.Id == "MOR_ET_PHASE_GUARD");

        CombatCommandResult secondThreshold = session.Handle(
            new UseAbilityCommand("second-wave-trigger", "PING", BossId),
            Now.AddMilliseconds(2));
        Assert.Equal(1, Boss(secondThreshold.Snapshot).Hp);
        Assert.NotNull(AliveSoul(secondThreshold.Snapshot));
        Assert.Equal(CombatSessionStatus.Active, secondThreshold.Snapshot.Status);
    }

    private static EncounterEnemyProfile SoulProfile()
    {
        MonsterDefinition monster = new(
            "MOR_ET_SOUL_WARRIOR",
            "Soul",
            MonsterRank.Elite,
            25,
            100,
            Stats(),
            TimeSpan.FromHours(1),
            0,
            [],
            "SOUL_AI");
        return new EncounterEnemyProfile(monster, new MonsterAiProfile("SOUL_AI", []));
    }

    private static AbilityDefinition DamageAbility(string id, decimal amount) =>
        new(
            id,
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: amount,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);

    private static CombatStats Stats() =>
        new(25, 100, 0, 0, 1, 0, 0, 0, 0);

    private static CombatActorSnapshot Boss(CombatSessionSnapshot snapshot) =>
        snapshot.Enemies!.Single(enemy => enemy.DefinitionId == "MOR_ET_BOSS");

    private static CombatActorSnapshot AliveSoul(CombatSessionSnapshot snapshot) =>
        snapshot.Enemies!.Single(enemy =>
            enemy.DefinitionId == "MOR_ET_SOUL_WARRIOR" && enemy.Hp > 0);
}
