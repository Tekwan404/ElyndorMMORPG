using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class MorEtRetargetTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid WarriorId =
        Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid MageId =
        Guid.Parse("b1000000-0000-0000-0000-000000000002");
    private static readonly Guid BossId =
        Guid.Parse("b2000000-0000-0000-0000-000000000001");

    [Fact]
    public void KillingOneSoulKeepsPlayersTargetingTheRemainingSoul()
    {
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["HIT_350"] = DamageAbility("HIT_350", 350),
            ["NUKE"] = DamageAbility("NUKE", 5_000)
        };
        CombatParticipantDefinition warrior = Player(WarriorId, "WARRIOR", abilities.Keys);
        CombatParticipantDefinition mage = Player(MageId, "MAGE", abilities.Keys);
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
            Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            warrior,
            boss,
            abilities,
            new MonsterAiProfile("MOR_ET_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 256).ToArray()),
            Now,
            additionalPlayers:
            [
                new CombatPlayerDefinition(
                    Guid.Parse("b3000000-0000-0000-0000-000000000002"),
                    mage,
                    ResolvedTalentModifiers.Empty)
            ]);

        session.ConfigureMorEtEncounter(new MorEtCombatEncounterProfile(
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                ["WARRIOR"] = SoulProfile("MOR_ET_SOUL_WARRIOR"),
                ["MAGE"] = SoulProfile("MOR_ET_SOUL_MAGE"),
                ["ARCHER"] = SoulProfile("MOR_ET_SOUL_ARCHER"),
                ["PALADIN"] = SoulProfile("MOR_ET_SOUL_PALADIN")
            }));

        CombatCommandResult wave = session.Handle(
            WarriorId,
            new UseAbilityCommand("wave", "HIT_350", BossId),
            Now);
        CombatActorSnapshot[] souls = wave.Snapshot.Enemies!
            .Where(enemy => enemy.DefinitionId.StartsWith("MOR_ET_SOUL_", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, souls.Length);

        Guid killedSoulId = souls[0].ActorId;
        Guid remainingSoulId = souls[1].ActorId;
        Assert.True(session.Handle(
            WarriorId,
            new SelectTargetCommand("warrior-target", killedSoulId),
            Now.AddMilliseconds(1)).Succeeded);
        Assert.True(session.Handle(
            MageId,
            new SelectTargetCommand("mage-target", killedSoulId),
            Now.AddMilliseconds(2)).Succeeded);

        CombatCommandResult killed = session.Handle(
            WarriorId,
            new UseAbilityCommand("kill-soul", "NUKE", killedSoulId),
            Now.AddMilliseconds(3));
        Assert.True(killed.Succeeded, killed.ErrorCode);
        Assert.Equal(remainingSoulId, session.Snapshot(WarriorId).SelectedTargetActorId);
        Assert.Equal(remainingSoulId, session.Snapshot(MageId).SelectedTargetActorId);
    }

    private static CombatParticipantDefinition Player(
        Guid actorId,
        string classId,
        IEnumerable<string> abilityIds) =>
        new(
            new CombatActorState(actorId, 2_000, 2_000, 100, 100, Stats()),
            CombatActorKind.Player,
            classId,
            classId,
            classId == "MAGE" ? "MANA" : "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            abilityIds.ToHashSet(StringComparer.Ordinal),
            CanAutoAttack: false);

    private static EncounterEnemyProfile SoulProfile(string id)
    {
        MonsterDefinition monster = new(
            id,
            id,
            MonsterRank.Elite,
            25,
            100,
            Stats(),
            TimeSpan.FromHours(1),
            0,
            [],
            $"{id}_AI");
        return new EncounterEnemyProfile(monster, new MonsterAiProfile($"{id}_AI", []));
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
}
