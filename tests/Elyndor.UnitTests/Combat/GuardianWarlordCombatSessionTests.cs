using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class GuardianWarlordCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WarlordCryTargetsPlayerAndCompanionAndCreatesCapstoneShield()
    {
        CombatActorState playerActor = Actor(Guid.Parse("10000000-0000-0000-0000-000000000001"), 100, 100);
        CombatActorState companionActor = Actor(Guid.Parse("20000000-0000-0000-0000-000000000001"), 80, 80);
        AbilityDefinition cry = new(
            "BATTLE_CRY",
            AbilityType.Instant,
            AbilityTargetType.SelfAndPartyMembersInCombat,
            20,
            TimeSpan.FromSeconds(60),
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions:
            [
                new(
                    AbilityActionType.ApplyEffect,
                    Effect: new EffectDefinition(
                        "WARLORD_BATTLE_CRY",
                        EffectKind.StatModifier,
                        TimeSpan.FromSeconds(20),
                        1,
                        EffectStackPolicy.Refresh,
                        0.08m,
                        ModifiedStat: EffectStat.AttackPower,
                        ModifierMode: EffectModifierMode.Percent))
            ]);

        CombatSession session = CreateSession(
            playerActor,
            companionActor,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [cry.Id] = cry
            },
            new ResolvedTalentModifiers(
                new TalentStatModifiers(),
                new TalentCombatModifiers(),
                new HashSet<string>([cry.Id], StringComparer.Ordinal),
                new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
                [Hook("W-9-1", 1, 4, secondaryValue: 6)],
                []));

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("warlord-cry", cry.Id, playerActor.ActorId),
            Now);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Snapshot.Player.Effects, effect => effect.Id == cry.Id || effect.Id == "WARLORD_BATTLE_CRY");
        Assert.Contains(result.Snapshot.Companion!.Effects, effect => effect.Id == "WARLORD_BATTLE_CRY");
        Assert.Contains(result.Snapshot.Player.Effects, effect => effect.Id == "WARLORD_PARTY_SHIELD");
        Assert.Contains(result.Snapshot.Companion.Effects, effect => effect.Id == "WARLORD_PARTY_SHIELD");
    }

    [Fact]
    public void GuardianBastionTalentAddsDodgeDuringItsSixSecondWindow()
    {
        CombatActorState playerActor = Actor(Guid.Parse("30000000-0000-0000-0000-000000000001"), 100, 100);
        AbilityDefinition bastion = new(
            "BASTION",
            AbilityType.Instant,
            AbilityTargetType.Self,
            40,
            TimeSpan.FromSeconds(90),
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions:
            [
                new(
                    AbilityActionType.ApplyEffect,
                    Effect: new EffectDefinition(
                        "BASTION_DAMAGE_REDUCTION",
                        EffectKind.StatModifier,
                        TimeSpan.FromSeconds(6),
                        1,
                        EffectStackPolicy.Refresh,
                        0.7m,
                        ModifiedStat: EffectStat.IncomingDamageMultiplier,
                        ModifierMode: EffectModifierMode.Multiplicative))
            ]);

        CombatSession session = CreateSession(
            playerActor,
            null,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [bastion.Id] = bastion
            },
            new ResolvedTalentModifiers(
                new TalentStatModifiers(),
                new TalentCombatModifiers(),
                new HashSet<string>([bastion.Id], StringComparer.Ordinal),
                new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
                [Hook("G-8-2", 1, 8)],
                []));

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("guardian-bastion", bastion.Id, playerActor.ActorId),
            Now);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Snapshot.Player.Effects, effect => effect.Id == "GUARDIAN_UNBREAKABLE_BASTION");
    }

    private static CombatSession CreateSession(
        CombatActorState playerActor,
        CombatActorState? companionActor,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        ResolvedTalentModifiers talents)
    {
        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 10, 0, 0),
            new HashSet<string>(abilities.Keys, StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition? companion = companionActor is null
            ? null
            : new(
                companionActor,
                CombatActorKind.Companion,
                "TEST_COMPANION",
                "Companion",
                "ENERGY",
                new AutoAttackProfile(TimeSpan.FromHours(1), 10, 0, 0),
                new HashSet<string>(StringComparer.Ordinal),
                CanAutoAttack: false);
        CombatActorState enemyActor = Actor(
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            1000,
            1000);
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            "TEST_ENEMY",
            "Enemy",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        return new CombatSession(
            Guid.Parse("50000000-0000-0000-0000-000000000001"),
            player,
            enemy,
            abilities,
            new MonsterAiProfile("TEST_AI", []),
            talents,
            new SequenceGameRandom(0.99m, 0.99m, 0.99m),
            Now,
            companion: companion);
    }

    private static CombatActorState Actor(Guid id, decimal maxHp, decimal hp) =>
        new(id, maxHp, hp, 100, 100, new CombatStats(1, 100, 0, 0, 1, 0, 0, 0, 0, 10));

    private static ResolvedTalentEventHook Hook(
        string talentId,
        int rank,
        decimal value,
        decimal secondaryValue = 0) =>
        new(
            talentId,
            TalentModifierKeys.OnPartyEvent,
            rank,
            value,
            null,
            TimeSpan.Zero,
            false,
            secondaryValue);
}
