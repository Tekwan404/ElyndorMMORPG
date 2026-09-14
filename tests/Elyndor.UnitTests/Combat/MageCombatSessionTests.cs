using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class MageCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid Enemy2Id =
        Guid.Parse("72000000-0000-0000-0000-000000000002");

    [Fact]
    public void ClearcastingMakesNextManaSpellFreeAndConsumesItsCharge()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook("A-1-2", TalentModifierKeys.OnAbilityUsed, 1, 100));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_ARCANE_SPARK", "MAGE_ICE_SHARD"], StringComparer.Ordinal));

        DateTimeOffset firstCastAt = Now.AddMilliseconds(1);
        CombatCommandResult spark = session.Handle(
            new UseAbilityCommand("spark", "MAGE_ARCANE_SPARK", EnemyId),
            firstCastAt);

        Assert.True(spark.Succeeded);
        Assert.Equal(85, spark.Snapshot.Player.Resource);
        Assert.Contains(
            spark.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_CLEARCASTING");

        DateTimeOffset shardAt = firstCastAt.AddSeconds(1.501);
        CombatCommandResult shard = session.Handle(
            new UseAbilityCommand("free-shard", "MAGE_ICE_SHARD", EnemyId),
            shardAt);

        Assert.True(shard.Succeeded);
        Assert.Equal(85, shard.Snapshot.Player.Resource);
        Assert.DoesNotContain(
            shard.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_CLEARCASTING");
    }

    [Fact]
    public void AbsolutePresenceMakesNextShortCastInstantAndHalfCost()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook("A-8-2", TalentModifierKeys.OnAbilityUsed, 1, 50));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_PRESENCE_OF_MIND", "MAGE_ICE_SHARD"], StringComparer.Ordinal));

        DateTimeOffset presenceAt = Now.AddMilliseconds(1);
        CombatCommandResult presence = session.Handle(
            new UseAbilityCommand("presence", "MAGE_PRESENCE_OF_MIND", PlayerId),
            presenceAt);
        Assert.True(presence.Succeeded);
        Assert.Contains(
            presence.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_PRESENCE_OF_MIND_ACTIVE");

        DateTimeOffset shardAt = presenceAt.AddMilliseconds(1);
        CombatCommandResult shard = session.Handle(
            new UseAbilityCommand("instant-shard", "MAGE_ICE_SHARD", EnemyId),
            shardAt);

        Assert.True(shard.Succeeded);
        Assert.Equal(91, shard.Snapshot.Player.Resource);
        CombatCastSnapshot cast = Assert.IsType<CombatCastSnapshot>(shard.Snapshot.Player.ActiveCast);
        Assert.Equal(shardAt, cast.ResolvesAtUtc);
        Assert.DoesNotContain(
            shard.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_PRESENCE_OF_MIND_ACTIVE");

        CombatCommandResult completed = session.AdvanceTo(shardAt);
        Assert.Null(completed.Snapshot.Player.ActiveCast);
        Assert.True(completed.Snapshot.Enemy.Hp < 100_000);
    }

    [Fact]
    public void CounterspellInterruptsActiveEnemyCastAndImprovedCounterspellSilences()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook("A-4-1", TalentModifierKeys.OnAbilityUsed, 2, 2));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_COUNTERSPELL"], StringComparer.Ordinal),
            enemyAbilityIds: new HashSet<string>(["TEST_ENEMY_CAST"], StringComparer.Ordinal),
            enemyPriorityAbilityIds: ["TEST_ENEMY_CAST"],
            enemyActionInterval: TimeSpan.FromSeconds(1));

        CombatCommandResult enemyStarted = session.AdvanceTo(Now.AddSeconds(1));
        CombatCastSnapshot activeCast = Assert.IsType<CombatCastSnapshot>(enemyStarted.Snapshot.Enemy.ActiveCast);
        Assert.Equal("TEST_ENEMY_CAST", activeCast.AbilityId);

        DateTimeOffset interruptAt = Now.AddSeconds(1).AddMilliseconds(1);
        CombatCommandResult interrupted = session.Handle(
            new UseAbilityCommand("counterspell", "MAGE_COUNTERSPELL", EnemyId),
            interruptAt);

        Assert.True(interrupted.Succeeded);
        Assert.Null(interrupted.Snapshot.Enemy.ActiveCast);
        Assert.Contains(interrupted.Events, combatEvent =>
            combatEvent.Type == CombatEventType.AbilityInterrupted
            && combatEvent.SourceActorId == EnemyId);
        Assert.Contains(
            interrupted.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_COUNTERSPELL_SILENCE");
    }

    [Fact]
    public void ImprovedCounterspellDoesNotSilenceWhenNoCastWasInterrupted()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook("A-4-1", TalentModifierKeys.OnAbilityUsed, 2, 2));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_COUNTERSPELL"], StringComparer.Ordinal));

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("empty-counterspell", "MAGE_COUNTERSPELL", EnemyId),
            Now.AddMilliseconds(1));

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(
            result.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_COUNTERSPELL_SILENCE");
    }

    [Fact]
    public void FrostNovaFreezesNormalEnemyAndIceLanceDealsTripleDamage()
    {
        CombatSession session = CreateSession(
            ResolvedTalentModifiers.Empty,
            new HashSet<string>(["MAGE_FROST_NOVA", "MAGE_ICE_LANCE"], StringComparer.Ordinal));

        DateTimeOffset novaAt = Now.AddMilliseconds(1);
        CombatCommandResult nova = session.Handle(
            new UseAbilityCommand("nova", "MAGE_FROST_NOVA", EnemyId),
            novaAt);

        Assert.True(nova.Succeeded);
        Assert.Contains(
            nova.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_FREEZE");
        Assert.DoesNotContain(
            nova.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_DEEP_CHILL");

        DateTimeOffset lanceAt = novaAt.AddSeconds(1.501);
        CombatCommandResult lance = session.Handle(
            new UseAbilityCommand("lance", "MAGE_ICE_LANCE", EnemyId),
            lanceAt);

        Assert.True(lance.Succeeded);
        CombatEvent damage = Assert.Single(
            lance.Events,
            combatEvent => combatEvent.Type == CombatEventType.DamageDealt
                && combatEvent.DefinitionId == "MAGE_ICE_LANCE");
        Assert.Equal(285m, damage.Amount);
    }

    [Fact]
    public void FrostNovaUsesDeepChillOnBossWithoutFreezeStun()
    {
        CombatSession session = CreateSession(
            ResolvedTalentModifiers.Empty,
            new HashSet<string>(["MAGE_FROST_NOVA"], StringComparer.Ordinal),
            enemyRank: MonsterRank.Boss);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("boss-nova", "MAGE_FROST_NOVA", EnemyId),
            Now.AddMilliseconds(1));

        Assert.True(result.Succeeded);
        Assert.Contains(
            result.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_DEEP_CHILL");
        Assert.DoesNotContain(
            result.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_FREEZE");
    }

    [Fact]
    public void IceBlockPreventsCastingUntilItsThreeSecondLockoutExpires()
    {
        CombatSession session = CreateSession(
            ResolvedTalentModifiers.Empty,
            new HashSet<string>(["MAGE_ICE_BLOCK", "MAGE_ICE_SHARD"], StringComparer.Ordinal));

        CombatCommandResult blocked = session.Handle(
            new UseAbilityCommand("ice-block-lock", "MAGE_ICE_BLOCK", PlayerId),
            Now);
        Assert.True(blocked.Succeeded);

        CombatCommandResult duringBlock = session.Handle(
            new UseAbilityCommand("blocked-shard", "MAGE_ICE_SHARD", EnemyId),
            Now.AddSeconds(1));
        Assert.False(duringBlock.Succeeded);
        Assert.Equal(100, duringBlock.Snapshot.Player.Resource);

        DateTimeOffset afterBlockAt = Now.AddSeconds(3).AddMilliseconds(1);
        CombatCommandResult afterBlock = session.Handle(
            new UseAbilityCommand("allowed-shard", "MAGE_ICE_SHARD", EnemyId),
            afterBlockAt);
        Assert.True(afterBlock.Succeeded);
        Assert.Equal(82, afterBlock.Snapshot.Player.Resource);
    }

    [Fact]
    public void ColdBloodAppearsAfterIceBlockAndDiscountsNextFrostSpell()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook(
                "I-7-3",
                TalentModifierKeys.OnAbilityUsed,
                2,
                20,
                secondaryValue: 50,
                duration: TimeSpan.FromSeconds(6)));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_ICE_BLOCK", "MAGE_ICE_SHARD"], StringComparer.Ordinal),
            enemyActionInterval: TimeSpan.FromSeconds(1));

        CombatCommandResult blocked = session.Handle(
            new UseAbilityCommand("ice-block", "MAGE_ICE_BLOCK", PlayerId),
            Now);
        Assert.True(blocked.Succeeded);
        Assert.Contains(blocked.Snapshot.Player.Effects, effect => effect.Id == "MAGE_ICE_BLOCK_ACTIVE");

        CombatCommandResult ready = session.AdvanceTo(Now.AddSeconds(3));
        Assert.Contains(ready.Snapshot.Player.Effects, effect => effect.Id == "MAGE_COLD_BLOOD");

        DateTimeOffset shardAt = Now.AddSeconds(3).AddMilliseconds(1);
        CombatCommandResult shard = session.Handle(
            new UseAbilityCommand("cold-blood-shard", "MAGE_ICE_SHARD", EnemyId),
            shardAt);

        Assert.True(shard.Succeeded);
        Assert.Equal(91, shard.Snapshot.Player.Resource);
        Assert.DoesNotContain(shard.Snapshot.Player.Effects, effect => effect.Id == "MAGE_COLD_BLOOD");
    }

    [Fact]
    public void BlizzardAppliesWinterChillOnlyToTargetsThatActuallyCrit()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook(
                "I-5-1",
                TalentModifierKeys.OnAbilityUsed,
                5,
                5,
                duration: TimeSpan.FromSeconds(12)));
        MultiEnemyFight fight = CreateMultiEnemyFight(
            talents,
            new HashSet<string>(["MAGE_BLIZZARD"], StringComparer.Ordinal),
            [0.5m, 0m, 0.5m, 0.9m]);

        DateTimeOffset startedAt = Now.AddMilliseconds(1);
        CombatCommandResult started = fight.Session.Handle(
            new UseAbilityCommand("blizzard-crit-targets", "MAGE_BLIZZARD", EnemyId),
            startedAt);
        Assert.True(started.Succeeded);

        CombatCommandResult completed = fight.Session.AdvanceTo(startedAt.AddSeconds(4));
        Guid[] winterTargets = completed.Events
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.EffectApplied
                && combatEvent.DefinitionId == "MAGE_WINTERS_CHILL"
                && combatEvent.TargetActorId.HasValue)
            .Select(combatEvent => combatEvent.TargetActorId!.Value)
            .Distinct()
            .ToArray();

        Assert.Equal(EnemyId, Assert.Single(winterTargets));
        Assert.Contains(
            fight.Enemy1.ActiveEffects,
            effect => effect.Definition.Id == "MAGE_WINTERS_CHILL");
        Assert.DoesNotContain(
            fight.Enemy2.ActiveEffects,
            effect => effect.Definition.Id == "MAGE_WINTERS_CHILL");
    }

    [Fact]
    public void ImprovedBlizzardStrengthensItsAppliedChill()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook("I-3-3", TalentModifierKeys.OnAbilityUsed, 3, 30));
        MultiEnemyFight fight = CreateMultiEnemyFight(
            talents,
            new HashSet<string>(["MAGE_BLIZZARD"], StringComparer.Ordinal),
            [0.5m, 0.5m, 0.5m, 0.5m]);

        DateTimeOffset startedAt = Now.AddMilliseconds(1);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand("improved-blizzard", "MAGE_BLIZZARD", EnemyId),
            startedAt).Succeeded);
        fight.Session.AdvanceTo(startedAt.AddSeconds(4));

        ActiveEffect chill = Assert.Single(
            fight.Enemy1.ActiveEffects,
            effect => effect.Definition.Id == "MAGE_CHILL");
        Assert.Equal(0.90m, chill.Definition.Magnitude);
    }

    private static ResolvedTalentEventHook Hook(
        string talentId,
        string key,
        int rank,
        decimal value,
        string? targetId = null,
        TimeSpan? internalCooldown = null,
        decimal secondaryValue = 0,
        decimal threshold = 0,
        decimal chancePercent = 100,
        TimeSpan? duration = null,
        TimeSpan? tickInterval = null,
        int triggerCount = 0,
        decimal castTimeSeconds = 0,
        decimal resourceCostReductionPercent = 0) =>
        new(
            talentId,
            key,
            rank,
            value,
            targetId,
            internalCooldown ?? TimeSpan.Zero,
            false,
            secondaryValue,
            threshold,
            chancePercent,
            duration ?? TimeSpan.Zero,
            tickInterval ?? TimeSpan.Zero,
            triggerCount,
            castTimeSeconds,
            resourceCostReductionPercent);

    private static ResolvedTalentModifiers Talents(
        IReadOnlySet<string> unlocked,
        params ResolvedTalentEventHook[] hooks) =>
        ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = unlocked,
            EventHooks = hooks
        };

    private static CombatSession CreateSession(
        ResolvedTalentModifiers talents,
        IReadOnlySet<string> knownAbilityIds,
        decimal playerResource = 100,
        decimal maxResource = 100,
        MonsterRank? enemyRank = null,
        IReadOnlySet<string>? enemyAbilityIds = null,
        IReadOnlyList<string>? enemyPriorityAbilityIds = null,
        TimeSpan? enemyActionInterval = null)
    {
        CombatActorState playerActor = Actor(
            PlayerId,
            hp: 500,
            maxResource,
            playerResource,
            spellPower: 100,
            criticalChance: 0);
        CombatActorState enemyActor = Actor(
            EnemyId,
            hp: 100_000,
            maxResource: 0,
            resource: 0,
            spellPower: 0,
            criticalChance: 0);

        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "MAGE",
            "Mage",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            knownAbilityIds,
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            "TEST_ENEMY",
            "Enemy",
            "NONE",
            new AutoAttackProfile(enemyActionInterval ?? TimeSpan.FromHours(1), 0, 0, 0),
            enemyAbilityIds ?? new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: enemyRank);

        return new CombatSession(
            Guid.NewGuid(),
            player,
            enemy,
            Abilities(),
            new MonsterAiProfile("TEST_AI", enemyPriorityAbilityIds ?? []),
            talents,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 200).ToArray()),
            Now);
    }

    private static MultiEnemyFight CreateMultiEnemyFight(
        ResolvedTalentModifiers talents,
        IReadOnlySet<string> knownAbilityIds,
        IReadOnlyList<decimal> randomValues)
    {
        CombatActorState playerActor = Actor(
            PlayerId,
            hp: 500,
            maxResource: 100,
            resource: 100,
            spellPower: 100,
            criticalChance: 0);
        CombatActorState enemy1Actor = Actor(
            EnemyId,
            hp: 100_000,
            maxResource: 0,
            resource: 0,
            spellPower: 0,
            criticalChance: 0);
        CombatActorState enemy2Actor = Actor(
            Enemy2Id,
            hp: 100_000,
            maxResource: 0,
            resource: 0,
            spellPower: 0,
            criticalChance: 0);

        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "MAGE",
            "Mage",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            knownAbilityIds,
            CanAutoAttack: false);
        CombatParticipantDefinition enemy1 = EnemyParticipant(enemy1Actor, "TEST_ENEMY_1");
        CombatParticipantDefinition enemy2 = EnemyParticipant(enemy2Actor, "TEST_ENEMY_2");
        CombatSession session = new(
            Guid.NewGuid(),
            player,
            [enemy1, enemy2],
            Abilities(),
            new MonsterAiProfile("TEST_AI", []),
            talents,
            new SequenceGameRandom(randomValues.ToArray()),
            Now);

        return new MultiEnemyFight(session, enemy1Actor, enemy2Actor);
    }

    private static CombatParticipantDefinition EnemyParticipant(
        CombatActorState actor,
        string definitionId) =>
        new(
            actor,
            CombatActorKind.Monster,
            definitionId,
            definitionId,
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));

    private static CombatActorState Actor(
        Guid id,
        decimal hp,
        decimal maxResource,
        decimal resource,
        decimal spellPower,
        decimal criticalChance) =>
        new(
            id,
            hp,
            hp,
            maxResource,
            resource,
            new CombatStats(
                Level: 20,
                Accuracy: 100,
                Dodge: 0,
                CriticalChance: criticalChance,
                CriticalDamage: 0,
                Armor: 0,
                MagicResistance: 0,
                ArmorPenetration: 0,
                MagicPenetration: 0,
                AttackPower: 0,
                SpellPower: spellPower));

    private static Dictionary<string, AbilityDefinition> Abilities() =>
        new(StringComparer.Ordinal)
        {
            ["MAGE_ARCANE_SPARK"] = Damage(
                "MAGE_ARCANE_SPARK", AbilityType.Instant, "ARCANE", 15, 3, 0, 0.75m),
            ["MAGE_PRESENCE_OF_MIND"] = Utility(
                "MAGE_PRESENCE_OF_MIND", AbilityTargetType.Self, "ARCANE", 0, 45, usesGlobalCooldown: false),
            ["MAGE_COUNTERSPELL"] = Utility(
                "MAGE_COUNTERSPELL", AbilityTargetType.SingleEnemy, "ARCANE", 0, 18, usesGlobalCooldown: false),
            ["MAGE_ICE_SHARD"] = Damage(
                "MAGE_ICE_SHARD", AbilityType.Casted, "FROST", 18, 0, 1.5, 1.05m),
            ["MAGE_FROST_NOVA"] = Utility(
                "MAGE_FROST_NOVA", AbilityTargetType.AllEnemiesInCombat, "FROST", 20, 25),
            ["MAGE_BLIZZARD"] = new(
                "MAGE_BLIZZARD",
                AbilityType.Casted,
                AbilityTargetType.AllEnemiesInCombat,
                30,
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(4),
                true,
                GlobalCooldownCategory.Standard,
                true,
                "FROST",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.Damage,
                        DamageType: DamageType.Magical,
                        SpellPowerCoefficient: 2.20m)
                ]),
            ["MAGE_ICE_BLOCK"] = Utility(
                "MAGE_ICE_BLOCK", AbilityTargetType.Self, "FROST", 0, 90, usesGlobalCooldown: false),
            ["MAGE_ICE_LANCE"] = Damage(
                "MAGE_ICE_LANCE", AbilityType.Instant, "FROST", 18, 6, 0, 0.95m),
            ["TEST_ENEMY_CAST"] = Damage(
                "TEST_ENEMY_CAST", AbilityType.Casted, "SHADOW", 0, 0, 5, 0.50m)
        };

    private static AbilityDefinition Damage(
        string id,
        AbilityType type,
        string school,
        decimal mana,
        double cooldownSeconds,
        double castSeconds,
        decimal coefficient) =>
        new(
            id,
            type,
            AbilityTargetType.SingleEnemy,
            mana,
            TimeSpan.FromSeconds(cooldownSeconds),
            TimeSpan.FromSeconds(castSeconds),
            true,
            GlobalCooldownCategory.Standard,
            true,
            school,
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    DamageType: DamageType.Magical,
                    SpellPowerCoefficient: coefficient)
            ]);

    private static AbilityDefinition Utility(
        string id,
        AbilityTargetType targetType,
        string school,
        decimal mana,
        double cooldownSeconds,
        bool usesGlobalCooldown = true) =>
        new(
            id,
            AbilityType.Instant,
            targetType,
            mana,
            TimeSpan.FromSeconds(cooldownSeconds),
            TimeSpan.Zero,
            usesGlobalCooldown,
            usesGlobalCooldown ? GlobalCooldownCategory.Standard : GlobalCooldownCategory.None,
            true,
            school,
            Actions: []);

    private sealed record MultiEnemyFight(
        CombatSession Session,
        CombatActorState Enemy1,
        CombatActorState Enemy2);
}
