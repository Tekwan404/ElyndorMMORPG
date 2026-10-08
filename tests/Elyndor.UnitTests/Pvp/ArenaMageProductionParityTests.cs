using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaMageProductionParityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LevelCostIsAppliedOnceBeforeTalentDiscountInProductionArena()
    {
        var package = await GameContentPackageLoader.LoadAsync(ContentPath());
        var fireball = package.Abilities!.Single(a => a.Id == "MAGE_FIREBALL");
        decimal expected = AbilityResourceCostScaling.Apply(fireball, 60).ResourceCost * .94m;
        Fight fight = await Create(["F-1-3"], useLevelCosts: true);
        Cast(fight, fireball.Id, Now);
        Assert.Equal(1000 - expected, fight.Mage.Actor.CurrentResource);
        Assert.Equal(expected, fight.Mage.Abilities[fireball.Id].ResourceCost);
    }

    [Fact]
    public async Task CounterspellTalentUpgradesAnAlreadyKnownCoreAbility()
    {
        Fight ordinary = await Create([]);
        Fight improved = await Create(["A-3-4"]);
        Cast(ordinary, "MAGE_COUNTERSPELL", Now);
        Cast(improved, "MAGE_COUNTERSPELL", Now);
        Assert.Equal(ordinary.Session.CooldownsFor(ordinary.Mage.AccountId)["MAGE_COUNTERSPELL"].AddSeconds(-3),
            improved.Session.CooldownsFor(improved.Mage.AccountId)["MAGE_COUNTERSPELL"]);
    }

    [Fact]
    public async Task CompletedMissilesEchoUsesAllFourTicksAndClearcastingRollsOnce()
    {
        Fight fight = await Create(["A-1-2", "A-2-2", "A-5-1", "A-7-4"], roll: 0);
        Cast(fight, "MAGE_ARCANE_POWER", Now);
        Cast(fight, "MAGE_ARCANE_MISSILES", Now.AddSeconds(2));
        fight.Session.AdvanceTo(Now.AddSeconds(5));
        Assert.DoesNotContain(fight.Mage.Actor.ActiveEffects, e => e.Definition.Id == "MAGE_CLEARCASTING");
        fight.Session.AdvanceTo(Now.AddSeconds(6));
        decimal direct = fight.Session.GetEventsAfter(0).Where(e => e.Type == CombatEventType.DamageDealt
            && e.DefinitionId == "MAGE_ARCANE_MISSILES").Sum(e => e.Amount);
        Assert.Equal(direct * .3m, Effect(fight.Target, "MAGE_ARCANE_ECHO").Definition.Magnitude);
        Assert.Single(fight.Session.GetEventsAfter(0), e => e.Type == CombatEventType.EffectApplied
            && e.DefinitionId == "MAGE_CLEARCASTING");
    }

    [Theory]
    [InlineData("MAGE_FIREBALL", 18.8)]
    [InlineData("MAGE_SCORCH", 9.4)]
    public async Task BurningSoulComposesWithUnlockedFireSpellsAndSpendsDiscountedMana(
        string abilityId, double cost)
    {
        Fight fight = await Create(["F-1-1", "F-1-3", "F-3-1"]);
        decimal before = fight.Mage.Actor.CurrentResource;
        Cast(fight, abilityId, Now);
        Assert.Equal(before - (decimal)cost, fight.Mage.Actor.CurrentResource);
        Complete(fight, Now);
        Assert.True(fight.Target.Actor.CurrentHp < fight.Target.Actor.MaxHp);
        Assert.Null(fight.Session.ActiveCastFor(fight.Mage.AccountId));
    }

    [Fact]
    public async Task FireCriticalHitProducesIgniteWhichDealsPeriodicDamage()
    {
        Fight fight = await Create(["F-1-1", "F-2-1", "F-8-2"], criticalChance: 100);
        Cast(fight, "MAGE_FIREBALL", Now);
        DateTimeOffset impact = Complete(fight, Now);
        decimal directDamage = fight.Target.Actor.MaxHp - fight.Target.Actor.CurrentHp;
        ActiveEffect ignite = Assert.Single(fight.Target.Actor.ActiveEffects,
            effect => effect.Definition.Id.Contains("IGNITE", StringComparison.Ordinal));
        Assert.Equal(fight.Mage.Actor.ActorId, ignite.SourceId);
        Assert.Equal(directDamage * 0.40m * 1.20m / 4m, ignite.Definition.Magnitude);
        decimal hp = fight.Target.Actor.CurrentHp;
        fight.Session.AdvanceTo(impact.AddSeconds(4));
        Assert.True(fight.Target.Actor.CurrentHp < hp);
        Assert.Contains(fight.Session.GetEventsAfter(0), e =>
            e.Type == CombatEventType.DamageDealt && e.DefinitionId == ignite.Definition.Id
            && e.Amount > 0);
    }

    [Fact]
    public async Task FrostCostModifiersComposeAndCompletedIceShardAppliesChill()
    {
        Fight fight = await Create(["I-1-1", "I-1-2", "I-1-3", "I-3-4"]);
        decimal mana = fight.Mage.Actor.CurrentResource;
        Cast(fight, "MAGE_ICE_SHARD", Now);
        Assert.Equal(mana - 18m * 0.94m * 0.88m, fight.Mage.Actor.CurrentResource);
        Assert.Equal(Now.AddSeconds(1.25), fight.Session.ActiveCastFor(fight.Mage.AccountId)!.ResolvesAtUtc);
        Complete(fight, Now);
        Effect(fight.Target, "MAGE_CHILL");
        Assert.True(fight.Target.Actor.CurrentHp < fight.Target.Actor.MaxHp);
    }

    [Fact]
    public async Task ImprovedArcaneMissilesSpendsLessManaAndDealsMoreDamageThanBaseline()
    {
        Fight improved = await Create(["A-1-1", "A-2-2", "A-2-3"]);
        Fight ordinary = await Create(["A-1-1", "A-2-2"]);
        decimal mana = improved.Mage.Actor.CurrentResource;
        Cast(improved, "MAGE_ARCANE_MISSILES", Now);
        Cast(ordinary, "MAGE_ARCANE_MISSILES", Now);
        Assert.Equal(mana - 28m * 0.85m, improved.Mage.Actor.CurrentResource);
        Complete(improved, Now);
        Complete(ordinary, Now);
        Assert.Null(improved.Session.ActiveCastFor(improved.Mage.AccountId));
        Assert.True(improved.Target.Actor.CurrentHp < ordinary.Target.Actor.CurrentHp);
    }

    [Fact]
    public async Task ImprovedScorchStacksVulnerabilityAndIncreasesSubsequentFireDamage()
    {
        Fight talented = await Create(["F-1-1", "F-3-1", "F-3-2"]);
        Fight baseline = await Create(["F-1-1", "F-3-1"]);
        foreach (Fight fight in new[] { talented, baseline })
        {
            Cast(fight, "MAGE_SCORCH", Now);
            Complete(fight, Now);
            Cast(fight, "MAGE_SCORCH", Now.AddSeconds(2));
            Complete(fight, Now.AddSeconds(2));
        }
        ActiveEffect vulnerability = Effect(talented.Target, "MAGE_FIRE_VULNERABILITY");
        Assert.Equal(2, vulnerability.Stacks);
        decimal talentedHp = talented.Target.Actor.CurrentHp;
        decimal baselineHp = baseline.Target.Actor.CurrentHp;
        Cast(talented, "MAGE_FIREBALL", Now.AddSeconds(4));
        Cast(baseline, "MAGE_FIREBALL", Now.AddSeconds(4));
        Complete(talented, Now.AddSeconds(4));
        Complete(baseline, Now.AddSeconds(4));
        Assert.True(talentedHp - talented.Target.Actor.CurrentHp > baselineHp - baseline.Target.Actor.CurrentHp);
    }

    [Fact]
    public async Task ClearcastingProcPaysForNextFrostCastAndIsConsumed()
    {
        // 0.05 is inside the production 10% proc chance, but above the zero crit chance.
        Fight fight = await Create(["A-1-1", "A-1-2", "A-6-2", "I-1-1"], roll: 0.05m);
        Cast(fight, "MAGE_ARCANE_SPARK", Now);
        Effect(fight.Mage, "MAGE_CLEARCASTING");
        decimal mana = fight.Mage.Actor.CurrentResource;
        Cast(fight, "MAGE_ICE_SHARD", Now.AddSeconds(2));
        Assert.Equal(mana, fight.Mage.Actor.CurrentResource);
        Assert.DoesNotContain(fight.Mage.Actor.ActiveEffects, e => e.Definition.Id == "MAGE_CLEARCASTING");
        Effect(fight.Mage, "MAGE_CLEARCASTING_REGEN");
        Complete(fight, Now.AddSeconds(2));
        Assert.True(fight.Target.Actor.CurrentHp < fight.Target.Actor.MaxHp);
    }

    [Fact]
    public async Task PresenceOfMindComposesWithAbsolutePresenceAndQuickThinking()
    {
        Fight fight = await Create(["A-4-2", "A-7-2", "A-8-2", "I-1-1"]);
        Cast(fight, "MAGE_PRESENCE_OF_MIND", Now);
        Assert.Equal(Now.AddSeconds(30), fight.Session.CooldownsFor(fight.Mage.AccountId)["MAGE_PRESENCE_OF_MIND"]);
        decimal mana = fight.Mage.Actor.CurrentResource;
        Cast(fight, "MAGE_ICE_SHARD", Now.AddMilliseconds(1));
        Assert.Equal(mana - 9m, fight.Mage.Actor.CurrentResource);
        Assert.DoesNotContain(fight.Mage.Actor.ActiveEffects, e => e.Definition.Id == "MAGE_PRESENCE_OF_MIND_ACTIVE");
        fight.Session.AdvanceTo(Now.AddMilliseconds(1));
        Assert.Null(fight.Session.ActiveCastFor(fight.Mage.AccountId));
        Assert.True(fight.Target.Actor.CurrentHp < fight.Target.Actor.MaxHp);
    }

    [Fact]
    public async Task ImprovedManaShieldAbsorbsSameHitForLessMana()
    {
        Fight ordinary = await Create(["A-3-2"]);
        Fight improved = await Create(["A-3-2", "A-3-3", "A-5-4"]);
        foreach (Fight fight in new[] { ordinary, improved })
        {
            Cast(fight, "MAGE_MANA_SHIELD", Now);
            Effect(fight.Mage, "MAGE_MANA_SHIELD_EFFECT");
            decimal hp = fight.Mage.Actor.CurrentHp;
            Cast(fight, "MAGE_FIRE_BLAST", Now.AddSeconds(2), opponent: true);
            Assert.Equal(hp, fight.Mage.Actor.CurrentHp);
        }
        Assert.True(improved.Mage.Actor.CurrentResource > ordinary.Mage.Actor.CurrentResource);
        decimal spent = improved.Mage.Actor.CurrentResource;
        improved.Session.AdvanceTo(Now.AddSeconds(4.01));
        Assert.True(improved.Mage.Actor.CurrentResource > spent);
    }

    [Fact]
    public async Task ControlledArcanePowerIncreasesDamageAndChargesReducedSurcharge()
    {
        Fight powered = await Create(["A-1-1", "A-5-1", "A-7-1"]);
        Fight baseline = await Create(["A-1-1"]);
        Cast(powered, "MAGE_ARCANE_POWER", Now);
        Effect(powered.Mage, "MAGE_ARCANE_POWER_ACTIVE");
        decimal mana = powered.Mage.Actor.CurrentResource;
        Cast(powered, "MAGE_ARCANE_SPARK", Now.AddSeconds(2));
        Cast(baseline, "MAGE_ARCANE_SPARK", Now.AddSeconds(2));
        Assert.Equal(mana - 16.5m, powered.Mage.Actor.CurrentResource);
        Assert.True(powered.Target.Actor.CurrentHp < baseline.Target.Actor.CurrentHp);
        Assert.Equal(Now.AddSeconds(90), powered.Session.CooldownsFor(powered.Mage.AccountId)["MAGE_ARCANE_POWER"]);
    }

    [Fact]
    public async Task EvocationRestoresManaOnEachTickAndCompletesAfterFourSeconds()
    {
        Fight fight = await Create(["A-6-4"], mana: 100);
        Cast(fight, "MAGE_EVOCATION", Now);
        fight.Session.AdvanceTo(Now.AddSeconds(3.9));
        Assert.Equal(400m, fight.Mage.Actor.CurrentResource);
        fight.Session.AdvanceTo(Now.AddSeconds(4));
        Assert.Equal(500m, fight.Mage.Actor.CurrentResource);
        Assert.Null(fight.Session.ActiveCastFor(fight.Mage.AccountId));
        Assert.True(fight.Session.CooldownsFor(fight.Mage.AccountId)["MAGE_EVOCATION"] > Now);
    }

    [Fact]
    public async Task ColdSnapAllowsSecondNovaWithDiminishedFreezeAndRetainsOwnCooldown()
    {
        Fight fight = await Create(["I-2-3", "I-4-1"]);
        Cast(fight, "MAGE_FROST_NOVA", Now);
        ActiveEffect first = Effect(fight.Target, "MAGE_FREEZE");
        TimeSpan firstDuration = first.ExpiresAtUtc - first.AppliedAtUtc;
        Cast(fight, "MAGE_COLD_SNAP", Now.AddSeconds(3));
        Assert.False(fight.Session.CooldownsFor(fight.Mage.AccountId).TryGetValue("MAGE_FROST_NOVA", out DateTimeOffset ready)
            && ready > Now.AddSeconds(3));
        Cast(fight, "MAGE_FROST_NOVA", Now.AddSeconds(3.01));
        ActiveEffect second = Effect(fight.Target, "MAGE_FREEZE");
        Assert.Equal(firstDuration / 2, second.ExpiresAtUtc - second.AppliedAtUtc);
        Assert.True(fight.Session.CooldownsFor(fight.Mage.AccountId)["MAGE_COLD_SNAP"] > Now.AddSeconds(3));
    }

    [Fact]
    public async Task ProductionFrostNovaTraversesAllFourDrLevelsAndResetsAfterWindow()
    {
        // Only Cold Snap's cooldown is removed in this fixture so its production reset
        // handler can expose all DR levels without reflection or private runtime access.
        // Frost Nova's definition, talent hooks, cost and effect handler stay unchanged.
        Fight fight = await Create(["I-2-3", "I-4-1"], repeatColdSnap: true);
        for (int application = 0; application < 4; application++)
        {
            DateTimeOffset at = Now.AddSeconds(application * 3);
            if (application > 0)
                Cast(fight, "MAGE_COLD_SNAP", at.AddMilliseconds(-1));

            decimal mana = fight.Mage.Actor.CurrentResource;
            long sequence = fight.Session.Snapshot.Sequence;
            Cast(fight, "MAGE_FROST_NOVA", at);
            Assert.Equal(mana - 20m, fight.Mage.Actor.CurrentResource);
            Assert.Equal(at.AddSeconds(25), fight.Session.CooldownsFor(fight.Mage.AccountId)["MAGE_FROST_NOVA"]);
            if (application < 3)
            {
                ActiveEffect freeze = Effect(fight.Target, "MAGE_FREEZE");
                Assert.Equal(TimeSpan.FromSeconds(2d / (1 << application)), freeze.ExpiresAtUtc - at);
            }
            else
            {
                Assert.DoesNotContain(fight.Target.Actor.ActiveEffects, e => e.Definition.Id == "MAGE_FREEZE");
                Assert.Contains(fight.Session.GetEventsAfter(sequence), e =>
                    e.Type == CombatEventType.EffectImmune && e.DefinitionId == "MAGE_FREEZE"
                    && e.TargetActorId == fight.Target.Actor.ActorId);
            }
        }

        DateTimeOffset resetAt = Now.AddSeconds(6.5) + CrowdControlDiminishingReturns.ResetWindow
            + TimeSpan.FromMilliseconds(1);
        Cast(fight, "MAGE_COLD_SNAP", resetAt.AddMilliseconds(-1));
        Cast(fight, "MAGE_FROST_NOVA", resetAt);
        Assert.Equal(TimeSpan.FromSeconds(2), Effect(fight.Target, "MAGE_FREEZE").ExpiresAtUtc - resetAt);
    }

    [Fact]
    public async Task PerfectPowerExemptsFirstTwoPaidSpellsFromSurchargeThenChargesThird()
    {
        Fight fight = await Create(["A-1-1", "A-5-1", "A-8-3"]);
        Cast(fight, "MAGE_ARCANE_POWER", Now);
        for (int cast = 0; cast < 3; cast++)
        {
            decimal mana = fight.Mage.Actor.CurrentResource;
            Cast(fight, "MAGE_ARCANE_SPARK", Now.AddSeconds(2 + cast * 3));
            Assert.Equal(mana - (cast < 2 ? 15m : 18m), fight.Mage.Actor.CurrentResource);
        }
    }

    [Fact]
    public async Task IceBlockSuppressesProductionPyroblastBurnDamageUntilImmunityExpires()
    {
        Fight fight = await Create(["I-4-2"], opponentTalentIds: ["F-4-1"]);
        Cast(fight, "MAGE_PYROBLAST", Now, opponent: true);
        DateTimeOffset impact = fight.Session.ActiveCastFor(fight.Target.AccountId)!.ResolvesAtUtc;
        fight.Session.AdvanceTo(impact);
        Assert.Single(fight.Mage.Actor.ActiveEffects,
            e => e.Definition.Kind == EffectKind.DamageOverTime);
        Cast(fight, "MAGE_ICE_BLOCK", impact.AddMilliseconds(1));
        decimal hp = fight.Mage.Actor.CurrentHp;
        fight.Session.AdvanceTo(impact.AddSeconds(3.002));
        Assert.Equal(hp, fight.Mage.Actor.CurrentHp);
        fight.Session.AdvanceTo(impact.AddSeconds(4));
        Assert.True(fight.Mage.Actor.CurrentHp < hp);
    }

    [Fact]
    public async Task ImprovedIceBarrierAbsorbsRealDamageAndUsesProductionDuration()
    {
        Fight fight = await Create(["I-5-2", "I-5-3"]);
        Cast(fight, "MAGE_ICE_BARRIER", Now);
        ActiveEffect barrier = Effect(fight.Mage, "MAGE_ICE_BARRIER_EFFECT");
        Assert.Equal(fight.Mage.Actor.MaxHp * 0.21m, barrier.RemainingMagnitude);
        Assert.Equal(TimeSpan.FromSeconds(12), barrier.ExpiresAtUtc - barrier.AppliedAtUtc);
        decimal absorb = barrier.RemainingMagnitude;
        decimal hp = fight.Mage.Actor.CurrentHp;
        Cast(fight, "MAGE_FIRE_BLAST", Now.AddSeconds(2), opponent: true);
        Assert.Equal(hp, fight.Mage.Actor.CurrentHp);
        Assert.True(barrier.RemainingMagnitude < absorb);
        Assert.Equal(Now.AddSeconds(30), fight.Session.CooldownsFor(fight.Mage.AccountId)["MAGE_ICE_BARRIER"]);
    }

    [Fact]
    public async Task IceBlockPreventsDamageAndActionsThenColdBloodDiscountsNextFrostCast()
    {
        Fight fight = await Create(["I-1-1", "I-4-2", "I-7-3"]);
        Cast(fight, "MAGE_ICE_BLOCK", Now);
        decimal hp = fight.Mage.Actor.CurrentHp;
        decimal mana = fight.Mage.Actor.CurrentResource;
        ArenaCommandResult rejected = fight.Session.UseAbility(fight.Mage.AccountId, "blocked", "MAGE_ICE_SHARD",
            fight.Target.Actor.ActorId, Now.AddSeconds(1));
        Assert.False(rejected.Succeeded);
        Assert.Equal(mana, fight.Mage.Actor.CurrentResource);
        Cast(fight, "MAGE_FIRE_BLAST", Now.AddSeconds(2), opponent: true);
        Assert.Equal(hp, fight.Mage.Actor.CurrentHp);
        fight.Session.AdvanceTo(Now.AddSeconds(3.01));
        Effect(fight.Mage, "MAGE_COLD_BLOOD");
        Cast(fight, "MAGE_ICE_SHARD", Now.AddSeconds(3.02));
        Assert.Equal(mana - 9m, fight.Mage.Actor.CurrentResource);
        Assert.DoesNotContain(fight.Mage.Actor.ActiveEffects, e => e.Definition.Id == "MAGE_COLD_BLOOD");
        Complete(fight, Now.AddSeconds(3.02));
        Assert.True(fight.Target.Actor.CurrentHp < fight.Target.Actor.MaxHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManaShieldCannotAbsorbMoreThanRemainingManaCanPay(bool improved)
    {
        Fight fight = await Create(improved ? ["A-3-2", "A-3-3"] : ["A-3-2"], mana: 50);
        Cast(fight, "MAGE_MANA_SHIELD", Now);
        Cast(fight, "MAGE_FIREBALL", Now.AddSeconds(2));
        Complete(fight, Now.AddSeconds(2));
        decimal mana = fight.Mage.Actor.CurrentResource;
        Assert.Equal(20m, mana);
        long sequence = fight.Session.Snapshot.Sequence;
        Cast(fight, "MAGE_FIRE_BLAST", Now.AddSeconds(4), opponent: true);
        decimal absorbed = fight.Session.GetEventsAfter(sequence)
            .Where(e => e.Type == CombatEventType.ShieldAbsorbed && e.DefinitionId == "MAGE_MANA_SHIELD_EFFECT")
            .Sum(e => e.Amount);
        Assert.True(absorbed > 0);
        decimal requiredMana = absorbed * (improved ? 0.7m : 1m);
        Assert.True(requiredMana <= mana, $"Absorbed {absorbed} requiring {requiredMana} Mana, but only {mana} was available.");
        Assert.Equal(mana - requiredMana, fight.Mage.Actor.CurrentResource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MagicAbsorptionRefundsOnlyActuallySpentManaAfterTwoSeconds(bool improved)
    {
        Fight fight = await Create(improved ? ["A-3-2", "A-3-3", "A-5-4"] : ["A-3-2", "A-5-4"], mana: 50);
        Cast(fight, "MAGE_MANA_SHIELD", Now);
        Cast(fight, "MAGE_FIREBALL", Now.AddSeconds(2));
        Complete(fight, Now.AddSeconds(2));
        decimal before = fight.Mage.Actor.CurrentResource;
        Cast(fight, "MAGE_FIRE_BLAST", Now.AddSeconds(4), opponent: true);
        decimal after = fight.Mage.Actor.CurrentResource;
        Assert.True(before > after);
        fight.Session.AdvanceTo(Now.AddSeconds(5.999));
        Assert.Equal(after, fight.Mage.Actor.CurrentResource);
        fight.Session.AdvanceTo(Now.AddSeconds(6));
        Assert.Equal(after + (before - after) * 0.10m, fight.Mage.Actor.CurrentResource);
        decimal refunded = fight.Mage.Actor.CurrentResource;
        fight.Session.AdvanceTo(Now.AddSeconds(7));
        Assert.Equal(refunded, fight.Mage.Actor.CurrentResource);
    }

    [Fact]
    public async Task BoneChillExtensionKeepsNovaDiminishedUntilWindowAfterActualFreezeEnd()
    {
        Fight fight = await Create(["I-2-3", "I-3-1", "I-4-1", "I-6-1", "I-6-3"], criticalChance: 100);
        Cast(fight, "MAGE_FROST_NOVA", Now);
        Cast(fight, "MAGE_ICE_LANCE", Now.AddSeconds(1.5));
        ActiveEffect extended = Effect(fight.Target, "MAGE_FREEZE");
        Assert.Equal(Now.AddSeconds(4), extended.ExpiresAtUtc);
        // The original end + 15 seconds has passed; the extended end + 15 has not.
        DateTimeOffset reapplyAt = Now.AddSeconds(17.1);
        Cast(fight, "MAGE_COLD_SNAP", reapplyAt.AddMilliseconds(-1));
        Cast(fight, "MAGE_FROST_NOVA", reapplyAt);
        Assert.Equal(TimeSpan.FromSeconds(1), Effect(fight.Target, "MAGE_FREEZE").ExpiresAtUtc - reapplyAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManaShieldCommitsPaymentBetweenTwoDamageActionsAndRefundsActualTotal(bool improved)
    {
        Fight fight = await Create(improved ? ["A-3-2", "A-3-3", "A-5-4"] : ["A-3-2", "A-5-4"],
            mana: 50, twoDamageActions: true);
        Cast(fight, "MAGE_MANA_SHIELD", Now);
        Cast(fight, "MAGE_FIREBALL", Now.AddSeconds(2));
        Complete(fight, Now.AddSeconds(2));
        decimal available = fight.Mage.Actor.CurrentResource;
        decimal hp = fight.Mage.Actor.CurrentHp;
        long sequence = fight.Session.Snapshot.Sequence;
        Cast(fight, "MAGE_FIRE_BLAST", Now.AddSeconds(4), opponent: true);
        CombatEvent[] events = fight.Session.GetEventsAfter(sequence).ToArray();
        CombatEvent[] hits = events.Where(e => e.Type == CombatEventType.DamageDealt
            && e.TargetActorId == fight.Mage.Actor.ActorId).ToArray();
        Assert.Equal(2, hits.Length);
        decimal absorbed = events.Where(e => e.Type == CombatEventType.ShieldAbsorbed
            && e.DefinitionId == "MAGE_MANA_SHIELD_EFFECT").Sum(e => e.Amount);
        Assert.InRange(absorbed * (improved ? 0.7m : 1m), available - 0.000001m, available);
        Assert.InRange(fight.Mage.Actor.CurrentResource, 0, 0.000001m);
        decimal debited = -events.Where(e => e.Type == CombatEventType.ResourceChanged
            && e.ActorId == fight.Mage.Actor.ActorId && e.DefinitionId == "MAGE_MANA_SHIELD_EFFECT").Sum(e => e.Amount);
        Assert.InRange(debited, available - 0.000001m, available);
        Assert.InRange(hp - fight.Mage.Actor.CurrentHp,
            hits.Sum(e => e.Amount) - 0.000001m, hits.Sum(e => e.Amount) + 0.000001m);
        Assert.True(hits[1].Amount > 0);
        fight.Session.AdvanceTo(Now.AddSeconds(5.999));
        Assert.InRange(fight.Mage.Actor.CurrentResource, 0, 0.000001m);
        fight.Session.AdvanceTo(Now.AddSeconds(6));
        Assert.InRange(fight.Mage.Actor.CurrentResource, available * 0.1m - 0.000001m, available * 0.1m + 0.000001m);
    }

    [Fact]
    public async Task GenericResourceBackedShieldPaysInPipelineWithoutMageTalentObserver()
    {
        Fight fight = await Create([], mana: 20, twoDamageActions: true);
        EffectEngine.Apply(fight.Mage.Actor, fight.Mage.Actor.ActorId, new EffectDefinition(
            "TEST_RESOURCE_SHIELD", EffectKind.Shield, TimeSpan.FromSeconds(10), 1,
            EffectStackPolicy.Replace, 100, ResourceCostPerAbsorbedDamage: 2), Now);
        long sequence = fight.Session.Snapshot.Sequence;
        Cast(fight, "MAGE_FIRE_BLAST", Now, opponent: true);
        CombatEvent[] events = fight.Session.GetEventsAfter(sequence).ToArray();
        Assert.Equal(0m, fight.Mage.Actor.CurrentResource);
        Assert.Equal(10m, events.Where(e => e.Type == CombatEventType.ShieldAbsorbed).Sum(e => e.Amount));
        Assert.Equal(-20m, Assert.Single(events, e => e.Type == CombatEventType.ResourceChanged
            && e.DefinitionId == "TEST_RESOURCE_SHIELD").Amount);
        Assert.Equal(2, events.Count(e => e.Type == CombatEventType.DamageDealt));
    }

    [Fact]
    public async Task ArcaneFortitudeRechecksManaBetweenDamageActionsAfterManaShieldPayment()
    {
        Fight fortified = await Create(["A-3-2", "A-4-4"], mana: 600, twoDamageActions: true);
        Fight baseline = await Create(["A-3-2"], mana: 600, twoDamageActions: true);
        CombatEvent[] IncomingHits(Fight fight)
        {
            Cast(fight, "MAGE_MANA_SHIELD", Now);
            Assert.Equal(590m, fight.Mage.Actor.CurrentResource);
            long sequence = fight.Session.Snapshot.Sequence;
            Cast(fight, "MAGE_FIRE_BLAST", Now.AddSeconds(2), opponent: true);
            return fight.Session.GetEventsAfter(sequence).Where(e => e.Type == CombatEventType.DamageDealt
                && e.TargetActorId == fight.Mage.Actor.ActorId).ToArray();
        }
        CombatEvent[] ordinary = IncomingHits(baseline);
        CombatEvent[] actual = IncomingHits(fortified);
        Assert.Equal(2, ordinary.Length);
        Assert.Equal(2, actual.Length);
        Assert.Equal(ordinary[0].DamageBeforeBlock, ordinary[1].DamageBeforeBlock);
        decimal firstDamage = decimal.Round(ordinary[0].DamageBeforeBlock * 0.94m, 0, MidpointRounding.AwayFromZero);
        Assert.Equal(firstDamage, actual[0].DamageBeforeBlock);
        Assert.True(590m - firstDamage < 500m);
        Assert.Equal(ordinary[1].DamageBeforeBlock, actual[1].DamageBeforeBlock);
        Assert.Equal(0m, fortified.Mage.Actor.CurrentResource);
        Assert.True(actual[1].Amount > 0);
    }

    [Theory]
    [InlineData(EffectKind.Shield, -1)]
    [InlineData(EffectKind.Buff, 1)]
    public async Task ResourceBackedShieldRejectsNegativeCostAndNonShieldCost(EffectKind kind, int cost)
    {
        Fight fight = await Create([]);
        Assert.Throws<ArgumentException>(() => EffectEngine.Apply(fight.Mage.Actor,
            fight.Mage.Actor.ActorId, new EffectDefinition("TEST_INVALID_RESOURCE_SHIELD", kind,
                TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 100,
                ResourceCostPerAbsorbedDamage: cost), Now));
    }

    [Fact]
    public async Task ArchmagePresenceDamageBonusDoesNotRequireArcanePower()
    {
        Fight archmage = await Create(["A-4-2", "A-9-1", "I-1-1"]);
        Fight baseline = await Create(["A-4-2", "I-1-1"]);
        foreach (Fight fight in new[] { archmage, baseline })
        {
            Cast(fight, "MAGE_PRESENCE_OF_MIND", Now);
            Cast(fight, "MAGE_ICE_SHARD", Now.AddMilliseconds(1));
            Complete(fight, Now.AddMilliseconds(1));
        }
        decimal ordinaryDamage = baseline.Target.Actor.MaxHp - baseline.Target.Actor.CurrentHp;
        decimal enhancedDamage = archmage.Target.Actor.MaxHp - archmage.Target.Actor.CurrentHp;
        Assert.True(ordinaryDamage > 0);
        Assert.Equal(decimal.Round(ordinaryDamage * 1.10m, 0, MidpointRounding.AwayFromZero), enhancedDamage);
    }

    [Fact]
    public async Task ArchmageCountsPaidUtilitySpellTowardThirdSpellClearcasting()
    {
        Fight fight = await Create(["A-1-1", "A-3-2", "A-5-1", "A-9-1"]);
        Cast(fight, "MAGE_ARCANE_POWER", Now);
        Cast(fight, "MAGE_MANA_SHIELD", Now.AddMilliseconds(1));
        Cast(fight, "MAGE_ARCANE_SPARK", Now.AddSeconds(2));
        Assert.DoesNotContain(fight.Mage.Actor.ActiveEffects, e => e.Definition.Id == "MAGE_CLEARCASTING");
        Cast(fight, "MAGE_FIREBALL", Now.AddSeconds(4));
        Complete(fight, Now.AddSeconds(4));
        Effect(fight.Mage, "MAGE_CLEARCASTING");
    }

    [Theory]
    [InlineData("MAGE_PYROBLAST", "F-4-1", "F-5-2", 10)]
    [InlineData("MAGE_PYROBLAST", "F-4-1", "F-9-1", 5)]
    [InlineData("MAGE_PYROBLAST", "F-4-1", "A-5-3", 8)]
    [InlineData("MAGE_FLAMESTRIKE", "F-4-3", "F-5-2", 10)]
    [InlineData("MAGE_FLAMESTRIKE", "F-4-3", "F-9-1", 5)]
    [InlineData("MAGE_FLAMESTRIKE", "F-4-3", "A-5-3", 8)]
    public async Task FireBurnTicksReceiveProductionFireOrSpellPowerTalentBonus(
        string abilityId, string unlock, string modifier, int percent)
    {
        // Large enough burns keep each production bonus visible after integer damage rounding.
        Fight enhanced = await Create([unlock, modifier], spellPower: 1000);
        Fight baseline = await Create([unlock], spellPower: 1000);
        foreach (Fight fight in new[] { enhanced, baseline })
        {
            Cast(fight, abilityId, Now);
            DateTimeOffset impact = Complete(fight, Now);
            fight.Session.AdvanceTo(impact.AddSeconds(1));
        }
        decimal TickDamage(Fight fight) => Assert.Single(fight.Session.GetEventsAfter(0), e =>
            e.Type == CombatEventType.DamageDealt && e.IsPeriodic
            && e.SourceActorId == fight.Mage.Actor.ActorId && e.DefinitionId?.EndsWith("_BURN", StringComparison.Ordinal) == true).Amount;
        decimal ordinary = TickDamage(baseline);
        Assert.True(ordinary > 0);
        Assert.Equal(decimal.Round(ordinary * (1 + percent / 100m), 0, MidpointRounding.AwayFromZero), TickDamage(enhanced));
    }

    [Fact]
    public async Task DeepMeditationBoostsInitialIdleRegenWithoutRequiringPreviousManaSpend()
    {
        Fight fight = await Create(["A-6-3"], mana: 100, resourceRegen: 10);
        fight.Session.AdvanceTo(Now.AddSeconds(3.001));
        decimal before = fight.Mage.Actor.CurrentResource;
        fight.Session.AdvanceTo(Now.AddSeconds(4.001));
        Assert.InRange(fight.Mage.Actor.CurrentResource - before, 24.99999m, 25.00001m);
    }

    private static ActiveEffect Effect(ArenaFighter fighter, string id) =>
        Assert.Single(fighter.Actor.ActiveEffects, effect => effect.Definition.Id == id);

    private static void Cast(Fight fight, string id, DateTimeOffset at, bool opponent = false)
    {
        ArenaFighter source = opponent ? fight.Target : fight.Mage;
        ArenaFighter target = opponent ? fight.Mage : fight.Target;
        if (source.Abilities[id].TargetType is AbilityTargetType.Self or AbilityTargetType.SingleAlly)
            target = source;
        ArenaCommandResult result = fight.Session.UseAbility(source.AccountId, Guid.NewGuid().ToString("N"),
            id, target.Actor.ActorId, at);
        Assert.True(result.Succeeded, $"{id}: {result.ErrorCode}");
    }

    private static DateTimeOffset Complete(Fight fight, DateTimeOffset at)
    {
        DateTimeOffset completed = fight.Session.ActiveCastFor(fight.Mage.AccountId)?.ResolvesAtUtc ?? at;
        fight.Session.AdvanceTo(completed);
        return completed;
    }

    private static async Task<Fight> Create(string[] talentIds, decimal mana = 1000,
        decimal criticalChance = 0, decimal roll = 0.5m, bool repeatColdSnap = false,
        string[]? opponentTalentIds = null, decimal resourceRegen = 0, decimal spellPower = 100,
        bool twoDamageActions = false, bool useLevelCosts = false)
    {
        var package = await GameContentPackageLoader.LoadAsync(ContentPath());
        TalentTreeDefinition tree = Assert.Single(package.TalentTrees ?? [], t => t.ClassId == "MAGE");
        Dictionary<string, int> ranks = talentIds.ToDictionary(id => id,
            id => tree.Nodes.Single(node => node.Id == id).MaxRank, StringComparer.Ordinal);
        ResolvedTalentModifiers talents = TalentModifierResolver.Resolve(tree, ranks);
        Dictionary<string, AbilityDefinition> abilities = (package.Abilities ?? [])
            // These cases isolate talent multipliers and shield payments from level budgets.
            .Select(a => useLevelCosts ? a : a with { ResourceCostByLevel = null })
            .ToDictionary(a => a.Id, StringComparer.Ordinal);
        if (repeatColdSnap)
            abilities["MAGE_COLD_SNAP"] = abilities["MAGE_COLD_SNAP"] with { Cooldown = TimeSpan.Zero };
        if (twoDamageActions)
        {
            AbilityDefinition fireBlast = abilities["MAGE_FIRE_BLAST"];
            IReadOnlyList<AbilityActionDefinition> actions = Assert.IsAssignableFrom<IReadOnlyList<AbilityActionDefinition>>(fireBlast.Actions);
            abilities[fireBlast.Id] = fireBlast with
            {
                Actions = actions.Concat(actions.Where(a => a.Type == AbilityActionType.Damage)).ToArray()
            };
        }
        string[] coreAbilities = package.ClassProfiles!.Single(profile => profile.Id == "MAGE").StartingAbilityIds!.ToArray();
        ArenaFighter mage = Fighter(talents, coreAbilities, mana, criticalChance, abilities, resourceRegen, spellPower);
        ResolvedTalentModifiers opponentTalents = opponentTalentIds is null
            ? ResolvedTalentModifiers.Empty
            : TalentModifierResolver.Resolve(tree, opponentTalentIds.ToDictionary(id => id,
                id => tree.Nodes.Single(node => node.Id == id).MaxRank, StringComparer.Ordinal));
        ArenaFighter target = Fighter(opponentTalents, ["MAGE_FIRE_BLAST"], 1000, 0, abilities);
        var session = new ArenaCombatSession(Guid.NewGuid(), mage, target,
            new SequenceGameRandom(Enumerable.Repeat(roll, 4096).ToArray()), Now);
        return new Fight(session, mage, target);
    }

    private static ArenaFighter Fighter(ResolvedTalentModifiers talents, string[] known, decimal mana,
        decimal criticalChance, IReadOnlyDictionary<string, AbilityDefinition> abilities,
        decimal resourceRegen = 0, decimal spellPower = 100)
    {
        var actor = new CombatActorState(Guid.NewGuid(), 10000, 10000, 1000, mana,
            CombatStats.Default with { Level = 60, SpellPower = spellPower, Accuracy = 100, CriticalChance = criticalChance });
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player, "MAGE", "Mage", "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(known, StringComparer.Ordinal), ResourceRegenPerSecond: resourceRegen, CanAutoAttack: false);
        return ArenaFighterAssembler.Create(new CombatPlayerDefinition(Guid.NewGuid(), participant, talents),
            60, abilities, hasCompanion: false).Fighter;
    }

    private static string ContentPath()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Repository content package was not found.");
    }

    private sealed record Fight(ArenaCombatSession Session, ArenaFighter Mage, ArenaFighter Target);
}
