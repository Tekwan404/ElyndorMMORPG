using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class PveTemporaryUntargetableTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 10, 45, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("94000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("95000000-0000-0000-0000-000000000001");

    [Fact]
    public void TemporaryUntargetableExpiresAtConfiguredTime()
    {
        CombatActorState actor = CombatActorState.CreateDummy(100);

        actor.SetTemporaryUntargetable(Now, TimeSpan.FromMilliseconds(800));

        Assert.False(actor.IsTargetable(Now));
        Assert.False(actor.IsTargetable(Now.AddMilliseconds(799)));
        Assert.True(actor.IsTargetable(Now.AddMilliseconds(800)));
    }

    [Fact]
    public void DirectAbilityRejectsUntargetableTarget()
    {
        CombatActorState caster = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(100);
        target.SetTemporaryUntargetable(Now, TimeSpan.FromMilliseconds(800));
        CombatRuntimeState runtime = new(caster);
        runtime.AddActor(target);
        AbilityDefinition hit = DamageAbility("DIRECT_HIT", 10);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            hit,
            new AbilityIntent("hit", hit.Id, target.ActorId),
            Now.AddMilliseconds(100),
            new SequenceGameRandom(0.5m));

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityErrorCode.InvalidTarget, result.ErrorCode);
        Assert.Equal(100, target.CurrentHp);
    }

    [Fact]
    public void ExistingDotContinuesWhileTargetIsUntargetable()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        Guid sourceId = Guid.Parse("96000000-0000-0000-0000-000000000001");
        EffectDefinition dot = new(
            "TEST_DOT",
            EffectKind.DamageOverTime,
            TimeSpan.FromSeconds(2),
            1,
            EffectStackPolicy.Replace,
            10,
            TickInterval: TimeSpan.FromMilliseconds(500));
        EffectEngine.Apply(target, sourceId, dot, Now);
        target.SetTemporaryUntargetable(Now.AddMilliseconds(100), TimeSpan.FromMilliseconds(800));

        IReadOnlyList<CombatEvent> events = EffectEngine.Process(
            target,
            Now.AddMilliseconds(500));

        Assert.Equal(90, target.CurrentHp);
        Assert.Contains(events, combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.DefinitionId == dot.Id
            && combatEvent.IsPeriodic);
        Assert.False(target.IsTargetable(Now.AddMilliseconds(500)));
    }

    [Fact]
    public void VoidTeleportBlocksNewTargetingAndDelaysAutoAttackUntilReturn()
    {
        AbilityDefinition teleport = new(
            "VOID_TELEPORT_TEST",
            AbilityType.Instant,
            AbilityTargetType.Self,
            0,
            TimeSpan.FromSeconds(10),
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "SHADOW",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.DropThreatPercent,
                    Amount: 50),
                new AbilityActionDefinition(
                    AbilityActionType.TemporaryUntargetable,
                    Duration: TimeSpan.FromMilliseconds(800))
            ]);
        AbilityDefinition directHit = DamageAbility("PLAYER_DIRECT_HIT", 100);
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            [teleport.Id] = teleport,
            [directHit.Id] = directHit
        };
        CombatSession session = CreateSession(abilities, teleport.Id, directHit.Id);

        session.AdvanceTo(Now.AddMilliseconds(100));

        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.TargetabilityChanged
            && combatEvent.SourceActorId == EnemyId
            && combatEvent.TargetActorId == EnemyId
            && combatEvent.Amount == 0.8m);
        Assert.Single(PlayerAutoAttackDamage(session));

        CombatCommandResult rejectedAbility = session.Handle(
            new UseAbilityCommand("blocked-hit", directHit.Id, EnemyId),
            Now.AddMilliseconds(200));
        CombatCommandResult rejectedTarget = session.Handle(
            new SelectTargetCommand("blocked-target", EnemyId),
            Now.AddMilliseconds(300));

        Assert.False(rejectedAbility.Succeeded);
        Assert.False(rejectedTarget.Succeeded);

        session.AdvanceTo(Now.AddMilliseconds(800));
        Assert.Single(PlayerAutoAttackDamage(session));

        session.AdvanceTo(Now.AddMilliseconds(900));
        Assert.Equal(2, PlayerAutoAttackDamage(session).Length);

        CombatCommandResult resumedAbility = session.Handle(
            new UseAbilityCommand("resumed-hit", directHit.Id, EnemyId),
            Now.AddMilliseconds(901));
        Assert.True(resumedAbility.Succeeded, resumedAbility.ErrorCode);
    }

    [Fact]
    public void TemporaryUntargetableActionRequiresPositiveDuration()
    {
        CombatActorState actor = CombatActorState.CreateDummy(100);
        CombatRuntimeState runtime = new(actor);
        AbilityDefinition invalid = new(
            "INVALID_UNTARGETABLE",
            AbilityType.Instant,
            AbilityTargetType.Self,
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
                    AbilityActionType.TemporaryUntargetable,
                    Duration: TimeSpan.Zero)
            ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AbilityEngine.Execute(
                runtime,
                invalid,
                new AbilityIntent("invalid", invalid.Id, actor.ActorId),
                Now));

        Assert.Contains("untargetable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CombatEvent[] PlayerAutoAttackDamage(CombatSession session) =>
        session.GetEventsAfter(0)
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.DamageDealt
                && combatEvent.SourceActorId == PlayerId
                && combatEvent.TargetActorId == EnemyId
                && combatEvent.DefinitionId == "AUTO_ATTACK")
            .ToArray();

    private static AbilityDefinition DamageAbility(string id, decimal damage) =>
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
                    Amount: damage,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);

    private static CombatSession CreateSession(
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        string teleportAbilityId,
        string playerAbilityId)
    {
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
            AttackPower: 0,
            SpellPower: 0);
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 2_000, 2_000, 100, 100, stats),
            CombatActorKind.Player,
            "TEST_PLAYER",
            "Test Player",
            "MANA",
            new AutoAttackProfile(
                TimeSpan.FromMilliseconds(500),
                BaseDamage: 25,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0,
                DamageType: DamageType.True),
            new HashSet<string>([playerAbilityId], StringComparer.Ordinal),
            CanAutoAttack: true);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 5_000, 5_000, 0, 0, stats),
            CombatActorKind.Monster,
            "VOID_TELEPORT_MONSTER",
            "Void Teleport Monster",
            "NONE",
            new AutoAttackProfile(
                TimeSpan.FromMilliseconds(100),
                BaseDamage: 1,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0,
                DamageType: DamageType.True),
            new HashSet<string>([teleportAbilityId], StringComparer.Ordinal));

        return new CombatSession(
            Guid.Parse("97000000-0000-0000-0000-000000000001"),
            player,
            enemy,
            abilities,
            new MonsterAiProfile("VOID_TELEPORT_AI", [teleportAbilityId]),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 200).ToArray()),
            Now);
    }
}
