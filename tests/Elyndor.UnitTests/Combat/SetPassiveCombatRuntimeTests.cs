using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.SetPassives;

namespace Elyndor.UnitTests.Combat;

public sealed class SetPassiveCombatRuntimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private const string SetId = "SET_TEST";
    private static readonly string[] ShotIds = ["SHOT"];
    private static readonly string[] BurnIds = ["BURN"];

    [Fact]
    public void EveryNthShotCountsAnAoeActionOnceAndIsolatesPlayers()
    {
        var owner = CombatActorState.CreateDummy(100);
        var other = CombatActorState.CreateDummy(100);
        var target = CombatActorState.CreateDummy(1000);
        var passive = Passive(new(EveryNth: 3, OncePerAction: true), Charge(.2m));
        var runtime = Runtime([passive], [owner, other, target]);
        runtime.Process(Hit(owner, target));
        runtime.Process(Hit(owner, other));
        runtime.Process(Hit(other, target));
        runtime.Process(Hit(owner, target) with { OccurredAtUtc = Now.AddSeconds(1) });
        Assert.Equal(1, Multiplier(owner, target));
        runtime.Process(Hit(owner, target) with { OccurredAtUtc = Now.AddSeconds(2) });
        Assert.Equal(1.2m, Multiplier(owner, target, Now.AddSeconds(2)));
        Assert.Equal(1, Multiplier(other, target));
    }

    [Fact]
    public void PetCriticalEmpowersItsOwnerAndNeverAnotherPlayer()
    {
        var owner = CombatActorState.CreateDummy(100);
        var pet = CombatActorState.CreateDummy(100);
        var target = CombatActorState.CreateDummy(1000);
        var passive = Passive(new(CriticalOnly: true), Charge(.1m)) with
            { Trigger = new(CombatEventType.DamageDealt, SetPassiveActorRole.CompanionOwner) };
        var runtime = Runtime([passive], [owner, pet, target], new Dictionary<Guid, Guid> { [pet.ActorId] = owner.ActorId });
        runtime.Process(Hit(pet, target) with { IsCritical = true });
        Assert.Equal(1.1m, Multiplier(owner, target));
        Assert.Equal(1, Multiplier(pet, target));
    }

    [Fact]
    public void StaticDirectBonusRequiresTheOwnersEffectAndCorrectAbility()
    {
        var owner = CombatActorState.CreateDummy(100);
        var target = CombatActorState.CreateDummy(1000);
        var passive = Passive(new(), new(SetPassiveActionKind.ModifyDirectDamage,
            Magnitude: .2m, AbilityIds: ShotIds, TargetEffectIds: BurnIds));
        _ = Runtime([passive], [owner, target]);
        var burn = new EffectDefinition("BURN", EffectKind.DamageOverTime, TimeSpan.FromSeconds(5), 1,
            EffectStackPolicy.Refresh, 1, TickInterval: TimeSpan.FromSeconds(1));
        EffectEngine.Apply(target, Guid.NewGuid(), burn, Now);
        Assert.Equal(1, Multiplier(owner, target));
        EffectEngine.Apply(target, owner.ActorId, burn with { StackPolicy = EffectStackPolicy.Independent }, Now);
        Assert.Equal(1.2m, Multiplier(owner, target));
        Assert.Equal(1, owner.SetPassiveMultiplier!("OTHER", true, target, Now, false));
        Assert.Equal(1, Multiplier(owner, target, Now.AddSeconds(5)));
    }

    [Fact]
    public void CooldownReductionClampsAndDoesNotCreateMissingCooldown()
    {
        var owner = CombatActorState.CreateDummy(100);
        var target = CombatActorState.CreateDummy(1000);
        Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal) { ["SHOT"] = Now.AddSeconds(1) };
        var passive = Passive(new(), new(SetPassiveActionKind.ReduceCooldown, "SHOT", Magnitude: 2));
        var runtime = Runtime([passive], [owner, target], cooldowns: cooldowns);
        runtime.Process(Hit(owner, target));
        Assert.Equal(Now, cooldowns["SHOT"]);
        cooldowns.Clear();
        runtime.Process(Hit(owner, target));
        Assert.Empty(cooldowns);
    }

    [Fact]
    public void ResourceRefundClampsAndCannotTriggerItself()
    {
        var owner = CombatActorState.CreateDummy(100, resource: 98);
        var passive = Passive(new(), new(SetPassiveActionKind.RestoreResource, Magnitude: 8));
        var runtime = Runtime([passive], [owner]);
        CombatEvent result = Assert.Single(runtime.Process(Hit(owner, owner)));
        Assert.Equal(100, owner.CurrentResource);
        Assert.Equal(2, result.Amount);
        Assert.Empty(runtime.Process(result));
    }

    [Fact]
    public void PeriodicProcReflectedAndOverhealDoNotAdvanceEffectiveHealing()
    {
        var owner = CombatActorState.CreateDummy(100);
        var passive = Passive(new(EveryNth: 2, PositiveAmountOnly: true), Charge(.2m)) with
            { Trigger = new(CombatEventType.HealingApplied, SetPassiveActorRole.Source) };
        var runtime = Runtime([passive], [owner]);
        var heal = new CombatEvent(CombatEventType.HealingApplied, Now, owner.ActorId, "HEAL", 10,
            owner.ActorId, owner.ActorId, HealingOrigin: HealingOrigin.Direct);
        runtime.Process(heal with { IsPeriodic = true });
        runtime.Process(heal with { IsProc = true });
        runtime.Process(heal with { IsReflected = true });
        runtime.Process(heal with { Amount = 0 });
        runtime.Process(heal with { HealingOrigin = HealingOrigin.Copied });
        runtime.Process(heal);
        Assert.Equal(1, Multiplier(owner, owner));
        runtime.Process(new CombatEvent(CombatEventType.HealingApplied, Now.AddSeconds(1), owner.ActorId,
            "HEAL", 10, owner.ActorId, owner.ActorId, HealingOrigin: HealingOrigin.Direct));
        Assert.Equal(1.2m, Multiplier(owner, owner, Now.AddSeconds(1)));
    }

    [Fact]
    public void ASharedHolyShockChargeCannotBeUsedForBothHealingAndDamage()
    {
        var charges = new SetPassiveCharges();
        charges.Arm(Charge(.2m) with { HealingOrDamage = true }, Now);
        Assert.Equal(1.2m, charges.Consume("SHOT", true, Now, healing: true));
        Assert.Equal(1, charges.Consume("SHOT", true, Now));
    }

    [Fact]
    public void DamagePipelineExcludesUnlabelledSecondaryDamageAndDoesNotSpendOnMiss()
    {
        var owner = CombatActorState.CreateDummy(100, stats: CombatStats.Default with { Accuracy = 0 });
        var target = CombatActorState.CreateDummy(1000);
        var runtime = Runtime([Passive(new(), Charge(.2m))], [owner, target]);
        runtime.Process(Hit(owner, target));
        var secondary = DamagePipeline.Resolve(new(owner, target, 100, DamageType.True,
            CanMiss: false, CanDodge: false, CanCrit: false), new FixedRandom(), Now);
        Assert.Equal(100, secondary.HpDamage);
        var missed = DamagePipeline.Resolve(new(owner, target, 100, DamageType.True,
            DefinitionId: "SHOT"), new FixedRandom(), Now);
        Assert.Equal(DamageAvoidance.Miss, missed.Avoidance);
        var direct = DamagePipeline.Resolve(new(owner, target, 100, DamageType.True,
            CanMiss: false, CanDodge: false, CanCrit: false, DefinitionId: "SHOT"), new FixedRandom(), Now);
        Assert.Equal(120, direct.HpDamage);
    }

    private static SetPassiveDefinition Passive(SetPassiveConditionDefinition conditions, SetPassiveActionDefinition action) =>
        new("TEST_EFFECT", SetId, 6, new(CombatEventType.DamageDealt, SetPassiveActorRole.Source), conditions, [action]);

    private static SetPassiveActionDefinition Charge(decimal amount) =>
        new(SetPassiveActionKind.EmpowerNextDirect, "NEXT_HIT", amount, TimeSpan.FromSeconds(8));

    private static CombatEvent Hit(CombatActorState source, CombatActorState target) =>
        new(CombatEventType.DamageDealt, Now, source.ActorId, "SHOT", 10, source.ActorId, target.ActorId);

    private static decimal Multiplier(CombatActorState source, CombatActorState target, DateTimeOffset? now = null) =>
        source.SetPassiveMultiplier!("SHOT", false, target, now ?? Now, false);

    private static SetPassiveCombatRuntime Runtime(SetPassiveDefinition[] effects, CombatActorState[] actors,
        Dictionary<Guid, Guid>? companions = null, Dictionary<string, DateTimeOffset>? cooldowns = null) =>
        new(effects, actors.ToDictionary(a => a.ActorId,
                _ => (IReadOnlyDictionary<string, int>)new Dictionary<string, int> { [SetId] = 6 }),
            id => actors.SingleOrDefault(a => a.ActorId == id),
            new Dictionary<string, AbilityDefinition>(), _ => cooldowns, companions);

    private sealed class FixedRandom : IGameRandom
    {
        public decimal NextUnit() => 0;
    }
}
