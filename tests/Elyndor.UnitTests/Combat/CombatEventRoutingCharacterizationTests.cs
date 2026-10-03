using System.Reflection;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Combat.SetPassives;
using Elyndor.Core.Monsters;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatEventRoutingCharacterizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void CriticalHitPublishesGenericThenClassFollowUpsBeforeDamageWithoutReapplyingHp()
    {
        var (session, player, enemy) = Create(Hook("TEST_CRIT", TalentModifierKeys.OnCriticalHit, 3),
            Hook("B-3-1", TalentModifierKeys.OnCriticalHit, 7));
        DamageResult damage = DamagePipeline.Resolve(new DamageRequest(player, enemy, 10,
            DamageType.Physical, CanMiss: false, CanDodge: false), new SequenceGameRandom(0), Now);
        decimal hp = enemy.CurrentHp;
        Publish(session, damage.Events, player, enemy);
        Assert.Equal(hp, enemy.CurrentHp);
        Assert.Equal(["CriticalHit:TEST", "ResourceChanged:TEST_CRIT",
            "ResourceChanged:B-3-1", "DamageDealt:TEST"], Trace(session));
        var procs = session.GetEventsAfter(0).Where(e => e.Type == CombatEventType.ResourceChanged).ToArray();
        Assert.All(procs, e => { Assert.True(e.IsProc); Assert.Equal(1, e.ProcDepth);
            Assert.Equal(e.DefinitionId, e.ProcOriginId); });
        Assert.Equal(10, player.CurrentResource);
    }

    [Fact]
    public void BlockPublishesSetFollowUpBeforeGuardianResourceReaction()
    {
        var (session, player, enemy) = Create(Hook("G-2-5", TalentModifierKeys.OnDamageTaken, 7));
        Publish(session, [new CombatEvent(CombatEventType.DamageBlocked, Now, player.ActorId,
            Amount: 10, SourceActorId: enemy.ActorId, TargetActorId: player.ActorId)], enemy, player);
        string[] trace = Trace(session);
        Assert.Contains("EffectApplied:EFFECT_GUARDIAN_BLOCK_ARMOR", trace);
        Assert.Contains("ResourceChanged:G-2-5", trace);
        Assert.True(Array.IndexOf(trace, "EffectApplied:EFFECT_GUARDIAN_BLOCK_ARMOR")
            < Array.IndexOf(trace, "ResourceChanged:G-2-5"));
        Assert.Contains("ResourceChanged:FULL_BLOCK", trace);
        Assert.Equal(12, player.CurrentResource);
    }

    [Theory]
    [InlineData(true, false, false, 0)]
    [InlineData(false, true, false, 1)]
    [InlineData(false, false, true, 0)]
    [InlineData(false, false, false, 2)]
    public void SecondaryPeriodicAndReflectedEventsDoNotStartClassOrGenericProcChains(
        bool periodic, bool proc, bool reflected, int depth)
    {
        var (session, player, enemy) = Create(Hook("TEST_CRIT", TalentModifierKeys.OnCriticalHit, 3),
            Hook("B-3-1", TalentModifierKeys.OnCriticalHit, 7));
        var input = new CombatEvent(CombatEventType.CriticalHit, Now, player.ActorId,
            SourceActorId: player.ActorId, TargetActorId: enemy.ActorId,
            IsPeriodic: periodic, IsProc: proc, ProcDepth: depth, IsReflected: reflected,
            ProcOriginId: "original", Sequence: 45);
        Publish(session, [input], player, enemy);
        Assert.Equal(0, player.CurrentResource);
        CombatEvent published = Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.CriticalHit);
        Assert.Equal(periodic, published.IsPeriodic);
        Assert.Equal(proc, published.IsProc);
        Assert.Equal(depth, published.ProcDepth);
        Assert.Equal("original", published.ProcOriginId);
    }

    [Fact]
    public void RecordCopiesDoNotDoubleDispatchGenericOrClassProcs()
    {
        var (session, player, enemy) = Create(Hook("TEST_CRIT", TalentModifierKeys.OnCriticalHit, 3),
            Hook("B-3-1", TalentModifierKeys.OnCriticalHit, 7));
        var input = new CombatEvent(CombatEventType.CriticalHit, Now, player.ActorId,
            SourceActorId: player.ActorId, TargetActorId: enemy.ActorId);
        Publish(session, [input, input with { }], player, enemy);
        Assert.Equal(10, player.CurrentResource);
        Assert.Equal(2, session.GetEventsAfter(0).Count(e => e.Type == CombatEventType.ResourceChanged));
    }

    [Fact]
    public void NormalizationFillsMissingIdentityWithoutReplacingExplicitMetadata()
    {
        var (session, player, enemy) = Create();
        var input = new CombatEvent(CombatEventType.DamageDealt, Now, enemy.ActorId,
            DefinitionId: "EXPLICIT", Amount: 1, WeaponHand: CombatWeaponHand.OffHand,
            WeaponDefinitionId: "OFF_HAND", IsProc: true, ProcDepth: 1, ProcOriginId: "ROOT",
            IsCritical: true, IsReflected: true, DamageType: DamageType.Magical);
        Publish(session, [input], player, enemy);
        CombatEvent published = Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.DamageDealt);
        Assert.Equal(input with { SourceActorId = player.ActorId, TargetActorId = enemy.ActorId,
            Sequence = published.Sequence }, published);
        Assert.Equal(2, published.Sequence);
        Assert.Equal(1000, enemy.CurrentHp);
    }

    [Fact]
    public void GenericAndClassInternalCooldownsStillAdmitAtTheirOriginalBoundary()
    {
        var (session, player, enemy) = Create(
            Hook("TEST_CRIT", TalentModifierKeys.OnCriticalHit, 3) with { InternalCooldown = TimeSpan.FromSeconds(2) },
            Hook("B-3-1", TalentModifierKeys.OnCriticalHit, 7) with { InternalCooldown = TimeSpan.FromSeconds(2) });
        foreach (int seconds in new[] { 0, 1, 2 })
            Publish(session, [new CombatEvent(CombatEventType.CriticalHit, Now.AddSeconds(seconds),
                player.ActorId, SourceActorId: player.ActorId, TargetActorId: enemy.ActorId)], player, enemy);
        Assert.Equal(20, player.CurrentResource);
        Assert.Equal(4, session.GetEventsAfter(0).Count(e => e.Type == CombatEventType.ResourceChanged));
    }

    [Fact]
    public void AbilityCompletedDispatchesGenericOnAbilityUsedWithoutReplayingAbility()
    {
        var (session, player, enemy) = Create(Hook("TEST_CAST", TalentModifierKeys.OnAbilityUsed, 4));
        Publish(session, [new CombatEvent(CombatEventType.AbilityCompleted, Now, player.ActorId)], player, enemy);
        Assert.Equal(["AbilityCompleted:TEST", "ResourceChanged:TEST_CAST"], Trace(session));
        Assert.Equal(1000, enemy.CurrentHp);
    }

    [Fact]
    public void ShieldAndVampirismResultsAreNotAppliedToHpTwice()
    {
        var (session, player, enemy) = CreateForClass("WARRIOR", [], vampirism: 50);
        player.ApplyDamage(100);
        EffectEngine.Apply(enemy, enemy.ActorId, new EffectDefinition("SHIELD", EffectKind.Shield,
            TimeSpan.FromSeconds(5), 1, EffectStackPolicy.Replace, 4), Now);
        Publish(session, DamagePipeline.Resolve(new DamageRequest(player, enemy, 10,
            DamageType.Physical, CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(0), Now).Events, player, enemy);
        Assert.Equal(994, enemy.CurrentHp);
        Assert.Equal(["ShieldAbsorbed:SHIELD", "DamageDealt:TEST", "HealingApplied:TEST"], Trace(session));
        Assert.Equal(903, player.CurrentHp);
        // HealingApplied is an authoritative result; the healing engine has already mutated HP.
        player.ApplyHealing(5);
        Publish(session, [new CombatEvent(CombatEventType.HealingApplied, Now, player.ActorId,
            Amount: 5)], player, player);
        Assert.Equal(908, player.CurrentHp);
    }

    [Fact]
    public void PvePaladinSealPublishesSecondaryDamageBeforePrimaryDamage()
    {
        var (session, player, enemy) = CreateForClass("PALADIN", []);
        Publish(session, [new CombatEvent(CombatEventType.AbilityCompleted, Now, player.ActorId,
            DefinitionId: "SEAL_OF_RIGHTEOUSNESS", SourceActorId: player.ActorId)], player, enemy);
        long before = session.Sequence;
        var damage = DamagePipeline.Resolve(new DamageRequest(player, enemy, 10,
            DamageType.Physical, CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(0), Now);
        Publish(session, damage.Events.Select(e => e with { DefinitionId = "AUTO_ATTACK" }), player, enemy);
        CombatEvent[] hits = session.GetEventsAfter(before).Where(e => e.Type == CombatEventType.DamageDealt).ToArray();
        Assert.Equal(["PALADIN_SEAL_RIGHTEOUSNESS_PROC", "AUTO_ATTACK"], hits.Select(e => e.DefinitionId));
        Assert.True(hits[0].IsProc);
        Assert.Equal(1, hits[0].ProcDepth);
        Assert.Equal("paladin", hits[0].ProcOriginId);
        Assert.Equal(988, enemy.CurrentHp);
    }

    [Fact]
    public void HostedArenaPaladinSealPublishesPrimaryThenSecondaryThroughSameMechanicsHost()
    {
        AbilityDefinition seal = new("SEAL_OF_RIGHTEOUSNESS", AbilityType.Instant,
            AbilityTargetType.Self, 0, TimeSpan.Zero, TimeSpan.Zero, false,
            GlobalCooldownCategory.None, true, "HOLY",
            Actions: [new AbilityActionDefinition(AbilityActionType.ResourceChange, 0)]);
        var abilities = new Dictionary<string, AbilityDefinition> { [seal.Id] = seal };
        ArenaFighter Fighter(bool attacks)
        {
            var actor = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100,
                CombatStats.Default with { Accuracy = 100 });
            var auto = new AutoAttackProfile(TimeSpan.FromSeconds(1), 10, 0, 0);
            var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
                "PALADIN", "Test", "MANA", auto, abilities.Keys.ToHashSet(), CanAutoAttack: attacks);
            var player = new CombatPlayerDefinition(Guid.NewGuid(), participant, ResolvedTalentModifiers.Empty);
            return new ArenaFighter(player.AccountId, actor.ActorId, actor, abilities, auto,
                CanAutoAttack: attacks, PlayerDefinition: player, BaseAbilities: abilities);
        }
        ArenaFighter owner = Fighter(true), opponent = Fighter(false);
        var session = new ArenaCombatSession(Guid.NewGuid(), owner, opponent, new SeededGameRandom(1), Now);
        Assert.True(session.UseAbility(owner.AccountId, "seal", seal.Id, owner.Actor.ActorId, Now).Succeeded);
        session.AdvanceTo(Now.AddSeconds(1));
        CombatEvent[] hits = session.GetEventsAfter(0).Where(e => e.Type == CombatEventType.DamageDealt).ToArray();
        Assert.Equal(["AUTO_ATTACK", "PALADIN_SEAL_RIGHTEOUSNESS_PROC"], hits.Select(e => e.DefinitionId));
        Assert.Equal(988, opponent.Actor.CurrentHp);
        Assert.True(hits[1].IsProc);
    }

    [Fact]
    public void LethalPeriodicHitKeepsOnKillAfterDeathExactlyOnce()
    {
        var (session, player, enemy) = Create(Hook("TEST_KILL", TalentModifierKeys.OnEnemyKilled, 9));
        var damage = DamagePipeline.Resolve(new DamageRequest(player, enemy, 1000,
            DamageType.True, CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(0), Now);
        CombatEvent[] periodic = damage.Events.Select(e => e with { IsPeriodic = true }).ToArray();
        Publish(session, periodic, player, enemy);
        Publish(session, [periodic[^1] with { }], player, enemy);
        var events = session.GetEventsAfter(0);
        Assert.Single(events, e => e.Type == CombatEventType.ActorDied);
        Assert.Single(events, e => e.Type == CombatEventType.EnemyKilled);
        Assert.Single(events, e => e.DefinitionId == "TEST_KILL");
        Assert.Equal(9, player.CurrentResource);
        Assert.Equal(CombatSessionStatus.Victory, session.Status);
    }

    [Theory]
    [InlineData(EffectKind.DamageOverTime, 990)]
    [InlineData(EffectKind.HealingOverTime, 1000)]
    public void PeriodicEffectsPublishTickThenResultAndNeverReplayMutation(EffectKind kind, int hp)
    {
        var (session, player, enemy) = Create();
        if (kind == EffectKind.HealingOverTime) enemy.ApplyDamage(10);
        EffectEngine.Apply(enemy, player.ActorId, new EffectDefinition("PERIODIC", kind,
            TimeSpan.FromSeconds(2), 1, EffectStackPolicy.Replace, 10,
            TickInterval: TimeSpan.FromSeconds(1)), Now);
        session.AdvanceTo(Now.AddSeconds(1));
        Assert.Equal(hp, enemy.CurrentHp);
        Assert.Equal(new[] { "EffectTicked:PERIODIC",
            kind == EffectKind.DamageOverTime ? "DamageDealt:PERIODIC" : "HealingApplied:PERIODIC" }, Trace(session));
    }

    private static string[] Trace(CombatSession session) => session.GetEventsAfter(0)
        .Where(e => e.Type != CombatEventType.CombatStarted)
        .Select(e => $"{e.Type}:{e.DefinitionId}").ToArray();

    // Inject engine results at the existing private boundary, not a new production test hook.
    private static void Publish(CombatSession session, IEnumerable<CombatEvent> events,
        CombatActorState source, CombatActorState target) => typeof(CombatSession)
        .GetMethod("ApplyKernelEvents", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(session, [events, source.ActorId, target.ActorId, "TEST", null, null]);

    private static ResolvedTalentEventHook Hook(string id, string trigger, decimal amount) =>
        new(id, trigger, 1, amount, null, TimeSpan.Zero, false);

    private static (CombatSession, CombatActorState, CombatActorState) Create(params ResolvedTalentEventHook[] hooks)
        => CreateForClass("WARRIOR", hooks);

    private static (CombatSession, CombatActorState, CombatActorState) CreateForClass(
        string classId, ResolvedTalentEventHook[] hooks, decimal vampirism = 0)
    {
        var player = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 0,
            CombatStats.Default with { Accuracy = 100, CriticalChance = 100 },
            new TalentCombatModifiers(VampirismPercent: vampirism));
        var enemy = new CombatActorState(Guid.NewGuid(), 1000, 1000, 0, 0, CombatStats.Default);
        CombatParticipantDefinition Participant(CombatActorState actor, CombatActorKind kind) =>
            new(actor, kind, kind == CombatActorKind.Player ? classId : "TARGET", "Test",
                kind == CombatActorKind.Player ? "RAGE" : "NONE",
                new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0), new HashSet<string>(),
                CanAutoAttack: false, EquippedSetPieces: new Dictionary<string, int>
                { [SetPassiveCatalog.AncientMineGuardianSetId] = 2 });
        var session = new CombatSession(Guid.NewGuid(), Participant(player, CombatActorKind.Player),
            Participant(enemy, CombatActorKind.Monster), new Dictionary<string, AbilityDefinition>(),
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty with { EventHooks = hooks },
            new SequenceGameRandom(0), Now);
        return (session, player, enemy);
    }
}
