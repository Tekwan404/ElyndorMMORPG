using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class AzraelPhaseGuardTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid BossId =
        Guid.Parse("92000000-0000-0000-0000-000000000001");

    [Fact]
    public void LethalPeriodicTickCannotConsumeGuardAndSkipSplit()
    {
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["LETHAL_DOT"] = new AbilityDefinition(
                "LETHAL_DOT",
                AbilityType.Instant,
                AbilityTargetType.SingleEnemy,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                false,
                "SHADOW",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.ApplyEffect,
                        Effect: new EffectDefinition(
                            "LETHAL_DOT_EFFECT",
                            EffectKind.DamageOverTime,
                            TimeSpan.FromSeconds(1),
                            1,
                            EffectStackPolicy.Replace,
                            5_000,
                            TimeSpan.FromMilliseconds(100),
                            PeriodicDamageType: DamageType.True))
                ]),
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
            "AZRAEL_BOSS",
            "Azrael",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);
        CombatSession session = new(
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            player,
            boss,
            abilities,
            new MonsterAiProfile("AZRAEL_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 256).ToArray()),
            Now);

        EncounterEnemyProfile clone = CloneProfile();
        session.ConfigureAzraelEncounter(new AzraelCombatEncounterProfile(
            new Dictionary<AzraelCloneRole, EncounterEnemyProfile>
            {
                [AzraelCloneRole.Fire] = clone,
                [AzraelCloneRole.Frost] = clone,
                [AzraelCloneRole.Void] = clone
            }));

        CombatCommandResult dotApplied = session.Handle(
            new UseAbilityCommand("dot", "LETHAL_DOT", BossId),
            Now);
        Assert.True(dotApplied.Succeeded, dotApplied.ErrorCode);

        CombatCommandResult periodicTick = session.AdvanceTo(Now.AddMilliseconds(100));
        CombatActorSnapshot afterDot = Boss(periodicTick.Snapshot);
        Assert.Equal(1, afterDot.Hp);
        Assert.Contains(afterDot.Effects, effect => effect.Id == "AZRAEL_PHASE_GUARD");
        Assert.DoesNotContain(afterDot.Effects, effect => effect.Id == "AZRAEL_SPLIT_IMMUNITY");

        CombatCommandResult directHit = session.Handle(
            new UseAbilityCommand("split-trigger", "PING", BossId),
            Now.AddMilliseconds(101));
        Assert.True(directHit.Succeeded, directHit.ErrorCode);
        Assert.Equal(CombatSessionStatus.Active, directHit.Snapshot.Status);
        Assert.Equal(4, directHit.Snapshot.Enemies!.Count);
        Assert.Contains(Boss(directHit.Snapshot).Effects, effect => effect.Id == "AZRAEL_SPLIT_IMMUNITY");
    }

    private static EncounterEnemyProfile CloneProfile()
    {
        MonsterDefinition monster = new(
            "AZRAEL_TEST_CLONE",
            "Aspect",
            MonsterRank.Elite,
            25,
            100,
            Stats(),
            TimeSpan.FromHours(1),
            0,
            [],
            "AZRAEL_TEST_CLONE_AI");
        return new EncounterEnemyProfile(
            monster,
            new MonsterAiProfile("AZRAEL_TEST_CLONE_AI", []));
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
        snapshot.Enemies!.Single(enemy => enemy.DefinitionId == "AZRAEL_BOSS");
}
