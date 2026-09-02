using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
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
    public void EnemyDeathAndCombatEndAreEmittedOnlyOnce()
    {
        CombatSession session = CreateSession(enemyHp: 1);

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
                (active, now) => active.Handle(new StartAutoAttackCommand("same-command"), now),
                CancellationToken.None))
            .ToArray();
        CombatOperationResult[] results = await Task.WhenAll(commands);

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == CombatErrorCodes.DuplicateCommand);
        Assert.Single(session.GetEventsAfter(0), item => item.Type == CombatEventType.AutoAttackStarted);
    }

    private static CombatSession CreateSession(
        decimal enemyHp,
        ResolvedTalentModifiers? talents = null,
        decimal playerCriticalChance = 5,
        TimeSpan? playerAutoAttackInterval = null)
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
            new AutoAttackProfile(playerAutoAttackInterval ?? TimeSpan.FromSeconds(2), 0, 0.65m, 10),
            new HashSet<string>(["STRIKE"], StringComparer.Ordinal));
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
            talents ?? ResolvedTalentModifiers.Empty, random, Now);
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
