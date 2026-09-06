using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PlayerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyTwoId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyThreeId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    [Fact]
    public void SnapshotKeepsPinnedContentIdentity()
    {
        CombatSession session = CreateSession(
            enemyHp: 80,
            contentVersion: "content-old",
            balanceVersion: "balance-old");

        CombatSessionSnapshot snapshot = session.Snapshot();

        Assert.Equal("content-old", snapshot.ContentVersion);
        Assert.Equal("balance-old", snapshot.BalanceVersion);
    }

    [Fact]
    public async Task RegistryKeepsPinnedSnapshotForWholeSession()
    {
        CombatSession session = CreateSession(
            enemyHp: 10_000,
            contentVersion: "content-old",
            balanceVersion: "balance-old");
        GameContentSnapshot pinned = GameContentSnapshot.Create(
            new GameContentPackage(
                "content-old",
                "balance-old",
                Now,
                [],
                []));

        using CombatSessionRegistry registry = new(
            new FrozenTimeProvider(Now),
            new NullPublisher(),
            new NullFinalizer());
        Guid accountId = Guid.NewGuid();

        Assert.True(registry.TryAdd(accountId, PlayerId, session, pinned));

        GameContentSnapshot? observed = null;
        CombatOperationResult result = await registry.ExecuteAsync(
            accountId,
            (active, contentSnapshot, now) =>
            {
                observed = contentSnapshot;
                return active.Handle(
                    new StopAutoAttackCommand("pin-check"),
                    now);
            },
            CancellationToken.None);

        Assert.Same(pinned, observed);
        Assert.Same(pinned, result.ContentSnapshot);
        Assert.Equal("content-old", result.Snapshot!.ContentVersion);
        Assert.Equal("balance-old", result.Snapshot.BalanceVersion);
    }

    [Fact]
    public void RegistryRejectsMismatchedPinnedSnapshot()
    {
        CombatSession session = CreateSession(
            enemyHp: 80,
            contentVersion: "content-old",
            balanceVersion: "balance-old");
        GameContentSnapshot wrong = GameContentSnapshot.Create(
            new GameContentPackage(
                "content-new",
                "balance-new",
                Now,
                [],
                []));
        using CombatSessionRegistry registry = new(
            new FrozenTimeProvider(Now),
            new NullPublisher(),
            new NullFinalizer());

        Assert.Throws<InvalidOperationException>(() =>
            registry.TryAdd(Guid.NewGuid(), PlayerId, session, wrong));
    }

    [Fact]
    public void SameCommandsTimeAndRandomSequenceProduceSameFight()
    {
        CombatSession first = CreateSession(enemyHp: 80);
        CombatSession second = CreateSession(enemyHp: 80);

        CombatCommandResult firstStart = first.Handle(new StartAutoAttackCommand("auto-on"), Now);
        CombatCommandResult secondStart = second.Handle(new StartAutoAttackCommand("auto-on"), Now);
        CombatCommandResult firstResult = first.AdvanceTo(Now.AddSeconds(10));
        CombatCommandResult secondResult = second.AdvanceTo(Now.AddSeconds(10));

        Assert.True(firstStart.Succeeded);
        Assert.True(secondStart.Succeeded);
        Assert.Equal(firstResult.Snapshot.Status, secondResult.Snapshot.Status);
        Assert.Equal(
            first.GetEventsAfter(0).Select(EventSignature),
            second.GetEventsAfter(0).Select(EventSignature));
    }

    [Fact]
    public void DualWieldEqualSpeedAlternatesHandsAndPreservesWeaponSource()
    {
        AutoAttackProfile mainHand = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 20,
            AttackPowerCoefficient: 0,
            ResourceOnHit: 10,
            WeaponDefinitionId: "MAIN_TEST_SWORD",
            WeaponHand: CombatWeaponHand.MainHand);
        AutoAttackProfile offHand = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 8,
            AttackPowerCoefficient: 0,
            ResourceOnHit: 10,
            WeaponDefinitionId: "OFF_TEST_SWORD",
            WeaponHand: CombatWeaponHand.OffHand);
        CombatSession session = CreateSession(
            enemyHp: 10_000,
            playerCriticalChance: 0,
            mainHandAutoAttack: mainHand,
            offHandAutoAttack: offHand);

        CombatCommandResult result = session.AdvanceTo(Now.AddSeconds(3));

        CombatEvent[] swings = session.GetEventsAfter(0)
            .Where(item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK")
            .ToArray();
        Assert.Equal(4, swings.Length);
        Assert.Equal(
            [
                CombatWeaponHand.MainHand,
                CombatWeaponHand.OffHand,
                CombatWeaponHand.MainHand,
                CombatWeaponHand.OffHand
            ],
            swings.Select(item => item.WeaponHand));
        Assert.Equal(
            ["MAIN_TEST_SWORD", "OFF_TEST_SWORD", "MAIN_TEST_SWORD", "OFF_TEST_SWORD"],
            swings.Select(item => item.WeaponDefinitionId));
        Assert.Equal(
            [Now, Now.AddSeconds(1), Now.AddSeconds(2), Now.AddSeconds(3)],
            swings.Select(item => item.OccurredAtUtc));
        Assert.True(swings[0].Amount > swings[1].Amount);
        CombatEvent[] rageFromWeapons = session.GetEventsAfter(0)
            .Where(item => item.Type == CombatEventType.ResourceChanged
                && item.DefinitionId == "AUTO_ATTACK")
            .ToArray();
        Assert.Equal(4, rageFromWeapons.Length);
        Assert.Equal(40, rageFromWeapons.Sum(item => item.Amount));
    }

    [Fact]
    public void DualWieldUsesIndependentWeaponIntervals()
    {
        AutoAttackProfile mainHand = new(
            TimeSpan.FromSeconds(2.4),
            BaseDamage: 10,
            AttackPowerCoefficient: 0,
            ResourceOnHit: 0,
            WeaponDefinitionId: "SLOW_MAIN",
            WeaponHand: CombatWeaponHand.MainHand);
        AutoAttackProfile offHand = new(
            TimeSpan.FromSeconds(1.6),
            BaseDamage: 6,
            AttackPowerCoefficient: 0,
            ResourceOnHit: 0,
            WeaponDefinitionId: "FAST_OFF",
            WeaponHand: CombatWeaponHand.OffHand);
        CombatSession session = CreateSession(
            enemyHp: 10_000,
            playerCriticalChance: 0,
            mainHandAutoAttack: mainHand,
            offHandAutoAttack: offHand);

        session.AdvanceTo(Now.AddSeconds(4));

        CombatEvent[] mainSwings = session.GetEventsAfter(0)
            .Where(item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK"
                && item.WeaponHand == CombatWeaponHand.MainHand)
            .ToArray();
        CombatEvent[] offSwings = session.GetEventsAfter(0)
            .Where(item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK"
                && item.WeaponHand == CombatWeaponHand.OffHand)
            .ToArray();

        Assert.Equal(
            [Now, Now + mainHand.Interval],
            mainSwings.Select(item => item.OccurredAtUtc));
        TimeSpan offInitialDelay = TimeSpan.FromTicks(offHand.Interval.Ticks / 2);
        Assert.Equal(
            [
                Now + offInitialDelay,
                Now + offInitialDelay + offHand.Interval,
                Now + offInitialDelay + offHand.Interval + offHand.Interval
            ],
            offSwings.Select(item => item.OccurredAtUtc));
    }

    [Fact]
    public void MultiEnemySnapshotExposesCollectionAndSelectedTarget()
    {
        CombatSession session = CreateMultiEnemySession(
            firstEnemyHp: 100,
            secondEnemyHp: 100,
            canAutoAttack: false);

        CombatSessionSnapshot snapshot = session.Snapshot();

        Assert.NotNull(snapshot.Enemies);
        Assert.Equal(2, snapshot.Enemies!.Count);
        Assert.Equal(EnemyId, snapshot.SelectedTargetActorId);
        Assert.Equal(EnemyId, snapshot.Enemy.ActorId);
        Assert.Equal([EnemyId, EnemyTwoId], snapshot.Enemies.Select(enemy => enemy.ActorId));
    }

    [Fact]
    public void SelectingTargetChangesCompatibilityTargetAndNextAutoAttackTarget()
    {
        CombatSession session = CreateMultiEnemySession(
            firstEnemyHp: 10_000,
            secondEnemyHp: 10_000,
            canAutoAttack: true);

        // Opening swing owns the initial target.
        session.AdvanceTo(Now);
        CombatCommandResult selected = session.Handle(
            new SelectTargetCommand("select-second", EnemyTwoId),
            Now.AddSeconds(1));
        session.AdvanceTo(Now.AddSeconds(2));

        Assert.True(selected.Succeeded);
        Assert.Equal(EnemyTwoId, selected.Snapshot.SelectedTargetActorId);
        Assert.Equal(EnemyTwoId, selected.Snapshot.Enemy.ActorId);
        Assert.Contains(selected.Events, item =>
            item.Type == CombatEventType.TargetChanged
            && item.TargetActorId == EnemyTwoId);

        CombatEvent secondSwing = Assert.Single(
            session.GetEventsAfter(selected.Snapshot.Sequence),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK");
        Assert.Equal(EnemyTwoId, secondSwing.TargetActorId);
    }

    [Fact]
    public void KillingOneEnemyKeepsCombatActiveAndRetargetsNextAliveEnemy()
    {
        CombatSession session = CreateMultiEnemySession(
            firstEnemyHp: 1,
            secondEnemyHp: 100,
            canAutoAttack: false);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("kill-first", "STRIKE", EnemyId),
            Now);

        Assert.True(result.Succeeded);
        Assert.Equal(CombatSessionStatus.Active, result.Snapshot.Status);
        Assert.Equal(EnemyTwoId, result.Snapshot.SelectedTargetActorId);
        Assert.Equal(EnemyTwoId, result.Snapshot.Enemy.ActorId);
        Assert.Equal(0, result.Snapshot.Enemies!.Single(enemy => enemy.ActorId == EnemyId).Hp);
        Assert.Equal(100, result.Snapshot.Enemies!.Single(enemy => enemy.ActorId == EnemyTwoId).Hp);
        Assert.Single(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.EnemyKilled
            && item.TargetActorId == EnemyId);
        Assert.DoesNotContain(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.CombatEnded);
        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.TargetChanged
            && item.TargetActorId == EnemyTwoId);
    }

    [Fact]
    public void CombatEndsOnlyAfterEveryEnemyIsDead()
    {
        CombatSession session = CreateMultiEnemySession(
            firstEnemyHp: 1,
            secondEnemyHp: 1,
            canAutoAttack: false);

        CombatCommandResult first = session.Handle(
            new UseAbilityCommand("kill-first", "STRIKE", EnemyId),
            Now);
        CombatCommandResult second = session.Handle(
            new UseAbilityCommand("kill-second", "STRIKE", EnemyTwoId),
            Now.AddSeconds(2));

        Assert.Equal(CombatSessionStatus.Active, first.Snapshot.Status);
        Assert.Equal(CombatSessionStatus.Victory, second.Snapshot.Status);
        Assert.Equal(2, session.GetEventsAfter(0).Count(item =>
            item.Type == CombatEventType.EnemyKilled));
        Assert.Single(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.CombatEnded);
        Assert.All(second.Snapshot.Enemies!, enemy => Assert.Equal(0, enemy.Hp));
    }

    [Fact]
    public void CannotSelectDeadOrUnknownEnemy()
    {
        CombatSession session = CreateMultiEnemySession(
            firstEnemyHp: 1,
            secondEnemyHp: 100,
            canAutoAttack: false);
        session.Handle(
            new UseAbilityCommand("kill-first", "STRIKE", EnemyId),
            Now);

        CombatCommandResult dead = session.Handle(
            new SelectTargetCommand("select-dead", EnemyId),
            Now.AddSeconds(1));
        CombatCommandResult unknown = session.Handle(
            new SelectTargetCommand("select-unknown", Guid.CreateVersion7()),
            Now.AddSeconds(1));

        Assert.False(dead.Succeeded);
        Assert.Equal(CombatErrorCodes.InvalidTarget, dead.ErrorCode);
        Assert.False(unknown.Succeeded);
        Assert.Equal(CombatErrorCodes.InvalidTarget, unknown.ErrorCode);
        Assert.Equal(EnemyTwoId, unknown.Snapshot.SelectedTargetActorId);
    }

    [Fact]
    public void SingleEnemyAbilityUsesSelectedTargetInsteadOfCallerTarget()
    {
        CombatSession session = CreateTargetingSession(100, 100, 100);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("single-authoritative", "STRIKE", EnemyTwoId),
            Now);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Snapshot.Enemies!.Single(enemy =>
            enemy.ActorId == EnemyId).Hp);
        Assert.Equal(100, result.Snapshot.Enemies!.Single(enemy =>
            enemy.ActorId == EnemyTwoId).Hp);
    }

    [Fact]
    public void AllEnemiesAbilityHitsEveryAliveEnemyInEncounterOrderOnce()
    {
        CombatSession session = CreateTargetingSession(100, 100, 100);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("all-targets", "WHIRLWIND", Guid.Empty),
            Now);

        Assert.True(result.Succeeded);
        Assert.Equal(65, result.Snapshot.Player.Resource);
        Assert.Equal(
            Now.AddSeconds(10),
            result.Snapshot.Player.Cooldowns["WHIRLWIND"]);
        Assert.All(result.Snapshot.Enemies!, enemy => Assert.Equal(80, enemy.Hp));
        Assert.Equal(
            [EnemyId, EnemyTwoId, EnemyThreeId],
            result.Events
                .Where(item => item.Type == CombatEventType.DamageDealt
                    && item.DefinitionId == "WHIRLWIND")
                .Select(item => item.TargetActorId));
    }

    [Fact]
    public void NEnemiesAbilitySelectsNextAliveEnemiesInEncounterOrder()
    {
        CombatSession session = CreateTargetingSession(1, 100, 100);
        CombatCommandResult killed = session.Handle(
            new UseAbilityCommand("kill-first-for-n", "STRIKE", EnemyThreeId),
            Now);
        Assert.True(killed.Succeeded);
        Assert.Equal(EnemyTwoId, killed.Snapshot.SelectedTargetActorId);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("cleave-two", "CLEAVE_TWO", EnemyId),
            Now.AddMilliseconds(1));

        Assert.True(result.Succeeded);
        Assert.Equal(
            [EnemyTwoId, EnemyThreeId],
            result.Events
                .Where(item => item.Type == CombatEventType.DamageDealt
                    && item.DefinitionId == "CLEAVE_TWO")
                .Select(item => item.TargetActorId));
        Assert.Equal(90, result.Snapshot.Enemies!.Single(enemy =>
            enemy.ActorId == EnemyTwoId).Hp);
        Assert.Equal(90, result.Snapshot.Enemies!.Single(enemy =>
            enemy.ActorId == EnemyThreeId).Hp);
    }

    [Fact]
    public void LethalAoeFinalizesEveryEnemyDeathBeforeSingleCombatEnd()
    {
        CombatSession session = CreateTargetingSession(10, 10, 10);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("lethal-aoe", "WHIRLWIND", Guid.Empty),
            Now);

        Assert.True(result.Succeeded);
        Assert.Equal(CombatSessionStatus.Victory, result.Snapshot.Status);
        Assert.Equal(3, result.Events.Count(item =>
            item.Type == CombatEventType.ActorDied));
        Assert.Equal(3, result.Events.Count(item =>
            item.Type == CombatEventType.EnemyKilled));
        Assert.Single(result.Events, item =>
            item.Type == CombatEventType.CombatEnded);
        Assert.All(result.Snapshot.Enemies!, enemy => Assert.Equal(0, enemy.Hp));
    }

    [Fact]
    public void EnemyDeathAndCombatEndAreEmittedOnlyOnce()
    {
        // The session starts with auto attack enabled and resolves its first swing at Now.
        // Leave 1 HP after that opening swing so STRIKE owns the terminal kill.
        CombatSession session = CreateSession(enemyHp: 20);

        CombatCommandResult kill = session.Handle(
            new UseAbilityCommand("kill", "STRIKE", EnemyId), Now);
        CombatCommandResult later = session.AdvanceTo(Now.AddMinutes(1));

        Assert.True(kill.Succeeded);
        Assert.Equal(CombatSessionStatus.Victory, kill.Snapshot.Status);
        Assert.Equal(CombatSessionStatus.Victory, later.Snapshot.Status);
        Assert.Single(session.GetEventsAfter(0), item => item.Type == CombatEventType.ActorDied);
        Assert.Single(session.GetEventsAfter(0), item => item.Type == CombatEventType.EnemyKilled);
        Assert.Single(session.GetEventsAfter(0), item => item.Type == CombatEventType.CombatEnded);
    }

    [Fact]
    public void CommandAfterCombatEndIsRejected()
    {
        CombatSession session = CreateSession(enemyHp: 1);
        session.Handle(new UseAbilityCommand("kill", "STRIKE", EnemyId), Now);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("after-end", "STRIKE", EnemyId), Now.AddSeconds(2));

        Assert.False(result.Succeeded);
        Assert.Equal(CombatErrorCodes.Ended, result.ErrorCode);
    }

    [Fact]
    public void CriticalInstinctUsesOneSecondInternalCooldown()
    {
        ResolvedTalentModifiers talents = ResolvedTalentModifiers.Empty with
        {
            EventHooks =
            [
                new ResolvedTalentEventHook(
                    "B-3-1", TalentModifierKeys.OnCriticalHit, 1, 4, null,
                    TimeSpan.FromSeconds(1), false)
            ]
        };
        CombatSession session = CreateSession(
            enemyHp: 10_000,
            talents,
            playerCriticalChance: 100,
            playerAutoAttackInterval: TimeSpan.FromMilliseconds(200));

        session.Handle(new StartAutoAttackCommand("auto-on"), Now);
        session.AdvanceTo(Now.AddMilliseconds(900));
        Assert.Single(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.ResourceChanged && item.DefinitionId == "B-3-1");

        session.AdvanceTo(Now.AddMilliseconds(1_100));

        Assert.Equal(2, session.GetEventsAfter(0).Count(item =>
            item.Type == CombatEventType.ResourceChanged && item.DefinitionId == "B-3-1"));
    }

    [Fact]
    public async Task ConcurrentDuplicateCommandsMutateSessionOnlyOnce()
    {
        CombatSession session = CreateSession(enemyHp: 10_000);
        using CombatSessionRegistry registry = new(
            new FrozenTimeProvider(Now),
            new NullPublisher(),
            new NullFinalizer());
        Guid accountId = Guid.NewGuid();
        Assert.True(registry.TryAdd(accountId, PlayerId, session));

        Task<CombatOperationResult>[] commands = Enumerable.Range(0, 2)
            .Select(_ => registry.ExecuteAsync(
                accountId,
                (active, now) => active.Handle(new StopAutoAttackCommand("same-command"), now),
                CancellationToken.None))
            .ToArray();
        CombatOperationResult[] results = await Task.WhenAll(commands);

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == CombatErrorCodes.DuplicateCommand);
        Assert.Single(session.GetEventsAfter(0), item => item.Type == CombatEventType.AutoAttackStopped);
    }

    private static CombatSession CreateTargetingSession(
        decimal firstEnemyHp,
        decimal secondEnemyHp,
        decimal thirdEnemyHp)
    {
        CombatStats playerStats = new(
            Level: 3, Accuracy: 100, Dodge: 0, CriticalChance: 0,
            CriticalDamage: 1, Armor: 10, MagicResistance: 5,
            ArmorPenetration: 0, MagicPenetration: 0, AttackPower: 30, SpellPower: 0);
        CombatStats enemyStats = new(
            Level: 3, Accuracy: 100, Dodge: 0, CriticalChance: 0,
            CriticalDamage: 1, Armor: 0, MagicResistance: 0,
            ArmorPenetration: 0, MagicPenetration: 0, AttackPower: 0, SpellPower: 0);
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 200, 200, 100, 100, playerStats),
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(
                ["STRIKE", "WHIRLWIND", "CLEAVE_TWO"],
                StringComparer.Ordinal),
            CanAutoAttack: false);

        CombatParticipantDefinition Enemy(
            Guid id,
            decimal hp,
            string definitionId,
            string name) =>
            new(
                new CombatActorState(id, hp, hp, 0, 0, enemyStats),
                CombatActorKind.Monster,
                definitionId,
                name,
                "NONE",
                new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
                new HashSet<string>(StringComparer.Ordinal));

        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["STRIKE"] = new(
                "STRIKE",
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
                        Amount: 100,
                        DamageType: DamageType.True,
                        CanMiss: false,
                        CanCrit: false,
                        CanDodge: false)
                ]),
            ["WHIRLWIND"] = new(
                "WHIRLWIND",
                AbilityType.Instant,
                AbilityTargetType.AllEnemiesInCombat,
                35,
                TimeSpan.FromSeconds(10),
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                false,
                "PHYSICAL",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.Damage,
                        Amount: 20,
                        DamageType: DamageType.True,
                        CanMiss: false,
                        CanCrit: false,
                        CanDodge: false)
                ]),
            ["CLEAVE_TWO"] = new(
                "CLEAVE_TWO",
                AbilityType.Instant,
                AbilityTargetType.NEnemiesInCombat,
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
                        Amount: 10,
                        DamageType: DamageType.True,
                        CanMiss: false,
                        CanCrit: false,
                        CanDodge: false)
                ],
                TargetCount: 2)
        };

        return new CombatSession(
            SessionId,
            player,
            [
                Enemy(EnemyId, firstEnemyHp, "WOLF", "Forest Wolf"),
                Enemy(EnemyTwoId, secondEnemyHp, "WOLF_ALPHA", "Alpha Wolf"),
                Enemy(EnemyThreeId, thirdEnemyHp, "BOAR", "Forest Boar")
            ],
            abilities,
            new MonsterAiProfile("PASSIVE_TARGETING_TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 200).ToArray()),
            Now);
    }

    private static CombatSession CreateMultiEnemySession(
        decimal firstEnemyHp,
        decimal secondEnemyHp,
        bool canAutoAttack)
    {
        CombatStats playerStats = new(
            Level: 3, Accuracy: 100, Dodge: 0, CriticalChance: 0,
            CriticalDamage: 1, Armor: 10, MagicResistance: 5,
            ArmorPenetration: 0, MagicPenetration: 0, AttackPower: 30, SpellPower: 0);
        CombatStats enemyStats = new(
            Level: 3, Accuracy: 100, Dodge: 0, CriticalChance: 0,
            CriticalDamage: 1, Armor: 5, MagicResistance: 5,
            ArmorPenetration: 0, MagicPenetration: 0, AttackPower: 8, SpellPower: 0);
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 200, 200, 100, 0, playerStats),
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromSeconds(2), 10, 0, 0),
            new HashSet<string>(["STRIKE"], StringComparer.Ordinal),
            CanAutoAttack: canAutoAttack);
        CombatParticipantDefinition firstEnemy = new(
            new CombatActorState(EnemyId, firstEnemyHp, firstEnemyHp, 0, 0, enemyStats),
            CombatActorKind.Monster,
            "WOLF",
            "Forest Wolf",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(10), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        CombatParticipantDefinition secondEnemy = new(
            new CombatActorState(EnemyTwoId, secondEnemyHp, secondEnemyHp, 0, 0, enemyStats),
            CombatActorKind.Monster,
            "WOLF_ALPHA",
            "Alpha Wolf",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(10), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["STRIKE"] = new(
                "STRIKE",
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
                        Amount: 100,
                        DamageType: DamageType.Physical,
                        CanMiss: false,
                        CanCrit: false,
                        CanDodge: false)
                ])
        };

        return new CombatSession(
            SessionId,
            player,
            [firstEnemy, secondEnemy],
            abilities,
            new MonsterAiProfile("PASSIVE_MULTI_TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 100).ToArray()),
            Now);
    }

    private static CombatSession CreateSession(
        decimal enemyHp,
        ResolvedTalentModifiers? talents = null,
        decimal playerCriticalChance = 5,
        TimeSpan? playerAutoAttackInterval = null,
        string contentVersion = "UNVERSIONED",
        string balanceVersion = "UNVERSIONED",
        AutoAttackProfile? mainHandAutoAttack = null,
        AutoAttackProfile? offHandAutoAttack = null)
    {
        CombatStats playerStats = new(
            Level: 3, Accuracy: 100, Dodge: 0, CriticalChance: playerCriticalChance,
            CriticalDamage: 1, Armor: 10, MagicResistance: 5,
            ArmorPenetration: 0, MagicPenetration: 0, AttackPower: 30, SpellPower: 0);
        CombatStats enemyStats = new(
            Level: 3, Accuracy: 100, Dodge: 0, CriticalChance: 0,
            CriticalDamage: 1, Armor: 5, MagicResistance: 5,
            ArmorPenetration: 0, MagicPenetration: 0, AttackPower: 8, SpellPower: 0);
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 200, 200, 100, 0, playerStats),
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            mainHandAutoAttack
                ?? new AutoAttackProfile(
                    playerAutoAttackInterval ?? TimeSpan.FromSeconds(2),
                    0,
                    0.65m,
                    10),
            new HashSet<string>(["STRIKE"], StringComparer.Ordinal),
            OffHandAutoAttack: offHandAutoAttack);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, enemyHp, enemyHp, 0, 0, enemyStats),
            CombatActorKind.Monster,
            "WOLF",
            "Forest Wolf",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(2.5), 6, 0.5m, 0),
            new HashSet<string>(["BITE"], StringComparer.Ordinal));
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["STRIKE"] = new(
                "STRIKE", AbilityType.Instant, AbilityTargetType.SingleEnemy, 0,
                TimeSpan.Zero, TimeSpan.Zero, true, GlobalCooldownCategory.Standard,
                false, "PHYSICAL", Actions:
                [new AbilityActionDefinition(AbilityActionType.Damage,
                    DamageType: DamageType.Physical, AttackPowerCoefficient: 1)]),
            ["BITE"] = new(
                "BITE", AbilityType.Instant, AbilityTargetType.SingleEnemy, 0,
                TimeSpan.FromSeconds(4), TimeSpan.Zero, true, GlobalCooldownCategory.Standard,
                false, "PHYSICAL", Actions:
                [new AbilityActionDefinition(AbilityActionType.Damage, 4,
                    DamageType.Physical, AttackPowerCoefficient: 0.9m)])
        };
        MonsterAiProfile ai = new("WOLF_BASIC_AI", ["BITE"]);
        decimal[] randomValues = Enumerable.Repeat(0.99m, 100).ToArray();
        IGameRandom random = new SequenceGameRandom(randomValues);

        return new CombatSession(
            SessionId, player, enemy, abilities, ai,
            talents ?? ResolvedTalentModifiers.Empty, random, Now,
            contentVersion, balanceVersion);
    }

    private static object EventSignature(CombatEvent item) => new
    {
        item.Sequence,
        item.Type,
        item.SourceActorId,
        item.TargetActorId,
        item.DefinitionId,
        item.Amount
    };

    private sealed class NullPublisher : ICombatUpdatePublisher
    {
        public Task PublishAsync(
            Guid accountId, CombatOperationResult update, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NullFinalizer : ICombatSessionFinalizer
    {
        public Task<Elyndor.Infrastructure.Progression.CombatRewardApplicationResult?> FinalizeAsync(
            Guid characterId,
            CombatSessionSnapshot snapshot,
            CancellationToken cancellationToken) =>
            Task.FromResult<Elyndor.Infrastructure.Progression.CombatRewardApplicationResult?>(null);
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override ITimer CreateTimer(
            TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new NoOpTimer();
    }

    private sealed class NoOpTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
