using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Items;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaOtherClassProductionParityTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("IMMOLATION_TRAP")]
    [InlineData("EXPLOSIVE_TRAP")]
    public async Task PrimaryClassAbilityDamageIsNotMisclassifiedAsAProc(string abilityId)
    {
        var duel = await Duel("ARCHER", [abilityId], []);
        Cast(duel, abilityId);
        CombatEvent damage = Assert.Single(duel.Session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.DamageDealt && item.DefinitionId == abilityId);
        Assert.True(damage.Amount > 0);
        Assert.False(damage.IsProc);
        Assert.Equal(0, damage.ProcDepth);
    }

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    public async Task BerserkerBloodRageChangesActualStrikeOnlyBelowThreshold(int hpPercent, bool empowered)
    {
        var plain = await Duel("WARRIOR", ["STRIKE"], [], hpPercent: hpPercent);
        var talented = await Duel("WARRIOR", ["STRIKE"], ["B-2-1"], hpPercent: hpPercent);
        Cast(plain, "STRIKE");
        Cast(talented, "STRIKE");
        decimal baseline = 10000 - plain.Target.Actor.CurrentHp;
        decimal actual = 10000 - talented.Target.Actor.CurrentHp;
        Assert.True(baseline > 0);
        if (empowered) Assert.True(actual > baseline, $"Blood Rage damage {actual} <= baseline {baseline}");
        else Assert.Equal(baseline, actual);
    }

    [Fact]
    public async Task GuardianBlockedPlayerAttackGrantsShieldFuryRageAndOpensRevenge()
    {
        var plain = await Duel("WARRIOR", ["REVENGE"], [], resource: 0, shield: true);
        var talented = await Duel("WARRIOR", [], ["G-2-2", "G-2-5"], resource: 0, shield: true);
        Assert.False(talented.Session.UseAbility(talented.Source.AccountId, "premature", "REVENGE",
            talented.Target.Actor.ActorId, Start).Succeeded);
        AttackSource(plain);
        AttackSource(talented);
        Assert.Contains(talented.Session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.DamageBlocked && combatEvent.TargetActorId == talented.Source.Actor.ActorId);
        Assert.True(talented.Source.Actor.CurrentResource > plain.Source.Actor.CurrentResource,
            $"Shield Fury rage: talented={talented.Source.Actor.CurrentResource}, plain={plain.Source.Actor.CurrentResource}");
        Assert.Contains(talented.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "GUARDIAN_REVENGE_WINDOW");
        decimal before = talented.Target.Actor.CurrentHp;
        Cast(talented, "REVENGE", at: Start.AddSeconds(2));
        Assert.True(talented.Target.Actor.CurrentHp < before);
        Assert.True(talented.Session.CooldownsFor(talented.Source.AccountId)["REVENGE"] > Start.AddSeconds(2));
    }

    [Fact]
    public async Task GuardianShieldBlockUnlockCastsSpendsRageAndInstallsTimedShieldModifiers()
    {
        var plain = await Duel("WARRIOR", [], [], shield: true);
        var talented = await Duel("WARRIOR", [], ["G-3-1"], shield: true);
        Assert.False(plain.Session.UseAbility(plain.Source.AccountId, "locked", "SHIELD_BLOCK",
            plain.Source.Actor.ActorId, Start).Succeeded);
        Cast(talented, "SHIELD_BLOCK", self: true);
        Assert.Equal(80, talented.Source.Actor.CurrentResource);
        Assert.True(talented.Session.CooldownsFor(talented.Source.AccountId)["SHIELD_BLOCK"] > Start);
        Assert.Contains(talented.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "GUARDIAN_SHIELD_BLOCK_CHANCE");
        Assert.Contains(talented.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "GUARDIAN_SHIELD_BLOCK_VALUE_MIN");
        Assert.Empty(talented.Target.Actor.ActiveEffects);
        talented.Session.AdvanceTo(Start.AddSeconds(6));
        Assert.DoesNotContain(talented.Source.Actor.ActiveEffects,
            effect => effect.Definition.Id.StartsWith("GUARDIAN_SHIELD_BLOCK_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WarlordBattleCrySpendsLessRageLastsLongerAndBuffsOnlyCaster()
    {
        var plain = await Duel("WARRIOR", ["BATTLE_CRY", "STRIKE"], []);
        var talented = await Duel("WARRIOR", ["STRIKE"], ["W-2-1", "W-1-1", "W-4-4", "W-4-1"]);
        Cast(plain, "BATTLE_CRY", self: true);
        Cast(talented, "BATTLE_CRY", self: true);
        Assert.True(talented.Source.Actor.CurrentResource > plain.Source.Actor.CurrentResource);
        Assert.NotEmpty(talented.Source.Actor.ActiveEffects);
        Assert.Empty(talented.Target.Actor.ActiveEffects);
        Assert.True(talented.Source.Actor.ActiveEffects.Max(effect => effect.ExpiresAtUtc)
            > plain.Source.Actor.ActiveEffects.Max(effect => effect.ExpiresAtUtc));
        Assert.True(talented.Source.Actor.ActiveEffects.Count > plain.Source.Actor.ActiveEffects.Count);
        Assert.True(talented.Session.CooldownsFor(talented.Source.AccountId)["BATTLE_CRY"] > Start);
        var unbuffed = await Duel("WARRIOR", ["STRIKE"], []);
        Cast(unbuffed, "STRIKE", at: Start.AddSeconds(2));
        Cast(talented, "STRIKE", at: Start.AddSeconds(2));
        Assert.True(talented.Target.Actor.CurrentHp < unbuffed.Target.Actor.CurrentHp);
    }

    [Theory]
    [InlineData("PIERCING_ARROW")]
    [InlineData("AIMED_SHOT")]
    public async Task MarksmanShotModifiersIncreaseRealDamageAndReduceFocusCost(string abilityId)
    {
        var plain = await Duel("ARCHER", [abilityId], []);
        var talented = await Duel("ARCHER", [abilityId], ["M-5-2", "M-1-2"]);
        Cast(plain, abilityId);
        Cast(talented, abilityId);
        plain.Session.AdvanceTo(Start.AddSeconds(5));
        talented.Session.AdvanceTo(Start.AddSeconds(5));
        Assert.True(plain.Target.Actor.CurrentHp < 10000);
        Assert.True(talented.Target.Actor.CurrentHp < plain.Target.Actor.CurrentHp);
        Assert.True(talented.Source.Actor.CurrentResource > plain.Source.Actor.CurrentResource);
        Assert.True(talented.Session.CooldownsFor(talented.Source.AccountId)[abilityId] > Start);
    }

    [Fact]
    public async Task MarksmanKillingShotCritBonusChangesShotOutcomeAtDeterministicRoll()
    {
        var plain = await Duel("ARCHER", ["PIERCING_ARROW"], [], roll: 0.01m);
        var talented = await Duel("ARCHER", ["PIERCING_ARROW"], ["M-1-1"], roll: 0.01m);
        Cast(plain, "PIERCING_ARROW");
        Cast(talented, "PIERCING_ARROW");
        Assert.True(plain.Target.Actor.CurrentHp < 10000);
        Assert.True(talented.Target.Actor.CurrentHp < plain.Target.Actor.CurrentHp);
        Assert.DoesNotContain(plain.Session.GetEventsAfter(0),
            combatEvent => combatEvent.DefinitionId == "PIERCING_ARROW" && combatEvent.Type == CombatEventType.CriticalHit);
        Assert.Contains(talented.Session.GetEventsAfter(0),
            combatEvent => combatEvent.DefinitionId == "PIERCING_ARROW" && combatEvent.Type == CombatEventType.CriticalHit);
    }

    [Fact]
    public async Task SurvivalSerpentStingUnlockCastsSpendsFocusAndAmplifiesPeriodicDamage()
    {
        var plain = await Duel("ARCHER", ["SERPENT_STING"], []);
        var talented = await Duel("ARCHER", [], ["S-1-3", "S-1-4"]);
        Cast(plain, "SERPENT_STING");
        Cast(talented, "SERPENT_STING");
        Assert.True(talented.Source.Actor.CurrentResource < 100);
        Assert.NotEmpty(talented.Target.Actor.ActiveEffects);
        plain.Session.AdvanceTo(Start.AddSeconds(11));
        talented.Session.AdvanceTo(Start.AddSeconds(11));
        Assert.True(plain.Target.Actor.CurrentHp < 10000);
        Assert.True(talented.Target.Actor.CurrentHp < plain.Target.Actor.CurrentHp,
            $"Serpent Sting damage: talented={10000 - talented.Target.Actor.CurrentHp}, plain={10000 - plain.Target.Actor.CurrentHp}");
    }

    [Fact]
    public async Task SurvivalFreezingTrapUnlockControlsOpponentAndCleverTrapsExtendsControl()
    {
        var plain = await Duel("ARCHER", ["FREEZING_TRAP"], []);
        var talented = await Duel("ARCHER", [], ["S-2-1", "S-2-4"]);
        Cast(plain, "FREEZING_TRAP");
        Cast(talented, "FREEZING_TRAP");
        Assert.True(talented.Source.Actor.CurrentResource < 100);
        Assert.True(talented.Session.CooldownsFor(talented.Source.AccountId)["FREEZING_TRAP"] > Start);
        Assert.NotEmpty(plain.Target.Actor.ActiveEffects);
        var plainControl = Assert.Single(plain.Target.Actor.ActiveEffects,
            effect => effect.Definition.Kind == Elyndor.Core.Combat.Effects.EffectKind.Stun);
        var talentedControl = Assert.Single(talented.Target.Actor.ActiveEffects,
            effect => effect.Definition.Kind == Elyndor.Core.Combat.Effects.EffectKind.Stun);
        Assert.True(talentedControl.ExpiresAtUtc > plainControl.ExpiresAtUtc);
        Assert.Empty(talented.Source.Actor.ActiveEffects);
        Assert.Equal(10000, talented.Target.Actor.CurrentHp);
    }

    [Fact]
    public async Task HolyHealingLightIncreasesCompletedHealAndEconomyReducesActualManaSpend()
    {
        var plain = await Duel("PALADIN", ["HOLY_LIGHT"], [], hpPercent: 20);
        var talented = await Duel("PALADIN", ["HOLY_LIGHT"], ["H-1-2", "H-1-3"], hpPercent: 20);
        Cast(plain, "HOLY_LIGHT", self: true);
        Cast(talented, "HOLY_LIGHT", self: true);
        Assert.NotNull(talented.Session.ActiveCastFor(talented.Source.AccountId));
        Assert.Equal(2000, talented.Source.Actor.CurrentHp);
        plain.Session.AdvanceTo(Start.AddSeconds(3));
        talented.Session.AdvanceTo(Start.AddSeconds(3));
        Assert.True(plain.Source.Actor.CurrentHp > 2000);
        Assert.True(talented.Source.Actor.CurrentHp > plain.Source.Actor.CurrentHp);
        Assert.True(talented.Source.Actor.CurrentResource > plain.Source.Actor.CurrentResource,
            $"Light's Economy mana: talented={talented.Source.Actor.CurrentResource}, plain={plain.Source.Actor.CurrentResource}");
        Assert.Null(talented.Session.ActiveCastFor(talented.Source.AccountId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HolyIlluminationRefundsManaOnlyForCriticalDirectHealing(bool critical)
    {
        var plain = await Duel("PALADIN", ["HOLY_LIGHT"], [], hpPercent: 20, critical: critical);
        var talented = await Duel("PALADIN", ["HOLY_LIGHT"], ["H-2-1"], hpPercent: 20, critical: critical);
        Cast(plain, "HOLY_LIGHT", self: true);
        Cast(talented, "HOLY_LIGHT", self: true);
        decimal spent = 100 - talented.Source.Actor.CurrentResource;
        Assert.True(spent > 0);
        plain.Session.AdvanceTo(Start.AddSeconds(3));
        talented.Session.AdvanceTo(Start.AddSeconds(3));
        Assert.Equal(plain.Source.Actor.CurrentHp, talented.Source.Actor.CurrentHp);
        Assert.Equal(plain.Source.Actor.CurrentResource + (critical ? spent * 0.75m : 0),
            talented.Source.Actor.CurrentResource);
    }

    [Fact]
    public async Task ProtectionHolyShieldUnlockSpendsManaAndImprovementExtendsBlockAndIncreasesRetaliation()
    {
        var plain = await Duel("PALADIN", ["HOLY_SHIELD"], [], shield: true);
        var talented = await Duel("PALADIN", [], ["P-3-1", "P-3-2"], shield: true);
        Cast(plain, "HOLY_SHIELD", self: true);
        Cast(talented, "HOLY_SHIELD", self: true);
        Assert.True(talented.Source.Actor.CurrentResource < 100);
        var baseline = Assert.Single(plain.Source.Actor.ActiveEffects,
            effect => effect.Definition.Id == "PALADIN_HOLY_SHIELD_BLOCK");
        var improved = Assert.Single(talented.Source.Actor.ActiveEffects,
            effect => effect.Definition.Id == "PALADIN_HOLY_SHIELD_BLOCK");
        Assert.True(improved.ExpiresAtUtc > baseline.ExpiresAtUtc);
        Assert.True(talented.Session.CooldownsFor(talented.Source.AccountId)["HOLY_SHIELD"] > Start);
        Assert.Empty(talented.Target.Actor.ActiveEffects);
        AttackSource(plain);
        AttackSource(talented);
        Assert.True(plain.Target.Actor.CurrentHp < 10000);
        Assert.True(talented.Target.Actor.CurrentHp < plain.Target.Actor.CurrentHp);
    }

    [Fact]
    public async Task RetributionSealOfCommandUnlockImprovementChangesActualAutoAttackProcAndReplacesSeal()
    {
        var plain = await Duel("PALADIN", ["SEAL_OF_COMMAND", "SEAL_OF_RIGHTEOUSNESS"], [], autoAttack: true);
        var talented = await Duel("PALADIN", ["SEAL_OF_RIGHTEOUSNESS"], ["R-1-4", "R-2-4"], autoAttack: true);
        Cast(plain, "SEAL_OF_COMMAND", self: true);
        Cast(talented, "SEAL_OF_COMMAND", self: true);
        Assert.True(talented.Source.Actor.CurrentResource < 100);
        Assert.Contains(talented.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_SEAL_COMMAND");
        plain.Session.AdvanceTo(Start.AddSeconds(3));
        talented.Session.AdvanceTo(Start.AddSeconds(3));
        Assert.True(plain.Target.Actor.CurrentHp < 10000);
        Assert.True(talented.Target.Actor.CurrentHp < plain.Target.Actor.CurrentHp);
        Cast(talented, "SEAL_OF_RIGHTEOUSNESS", self: true, at: Start.AddSeconds(4));
        Assert.DoesNotContain(talented.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_SEAL_COMMAND");
        Assert.Contains(talented.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_SEAL_RIGHTEOUSNESS");
    }

    private sealed record DuelState(ArenaCombatSession Session, ArenaFighter Source, ArenaFighter Target);

    [Theory]
    [InlineData(34, 68)]
    [InlineData(35, 80)]
    public async Task ArdentDefenderReducesActualIncomingHitOnlyBelowThirtyFivePercent(int hpPercent, decimal expectedDamage)
    {
        var plain = await Duel("PALADIN", [], [], hpPercent: hpPercent);
        var talented = await Duel("PALADIN", [], ["P-5-3"], hpPercent: hpPercent);
        decimal before = talented.Source.Actor.CurrentHp;
        AttackSource(plain);
        AttackSource(talented);
        Assert.Equal(80, before - plain.Source.Actor.CurrentHp);
        Assert.Equal(expectedDamage, before - talented.Source.Actor.CurrentHp);
    }

    [Fact]
    public async Task ConsecratedProtectionReducesIncomingDamageOnlyInsideConsecrationWindow()
    {
        var plain = await Duel("PALADIN", ["CONSECRATION"], []);
        var talented = await Duel("PALADIN", ["CONSECRATION"], ["P-7-2"]);
        Cast(plain, "CONSECRATION");
        Cast(talented, "CONSECRATION");
        AttackSource(plain, Start.AddSeconds(2));
        AttackSource(talented, Start.AddSeconds(2));
        Assert.Equal(80, 10000 - plain.Source.Actor.CurrentHp);
        Assert.Equal(75, 10000 - talented.Source.Actor.CurrentHp);
        decimal plainBefore = plain.Source.Actor.CurrentHp;
        decimal talentedBefore = talented.Source.Actor.CurrentHp;
        AttackSource(plain, Start.AddSeconds(8), "expired");
        AttackSource(talented, Start.AddSeconds(8), "expired");
        Assert.Equal(plainBefore - plain.Source.Actor.CurrentHp, talentedBefore - talented.Source.Actor.CurrentHp);
    }

    [Fact]
    public async Task ConvictionChangesAutoAttackCritWithoutIncreasingHealingCrit()
    {
        var plain = await Duel("PALADIN", ["HOLY_LIGHT"], [], hpPercent: 20, autoAttack: true, roll: 0.04m);
        var talented = await Duel("PALADIN", ["HOLY_LIGHT"], ["R-1-3"], hpPercent: 20, autoAttack: true, roll: 0.04m);
        Cast(plain, "HOLY_LIGHT", self: true);
        Cast(talented, "HOLY_LIGHT", self: true);
        plain.Session.AdvanceTo(Start.AddSeconds(3));
        talented.Session.AdvanceTo(Start.AddSeconds(3));
        Assert.Equal(plain.Source.Actor.CurrentHp, talented.Source.Actor.CurrentHp);
        Assert.True(talented.Target.Actor.CurrentHp < plain.Target.Actor.CurrentHp);
        Assert.Contains(talented.Session.GetEventsAfter(0), combatEvent =>
            combatEvent.DefinitionId == "AUTO_ATTACK" && combatEvent.Type == CombatEventType.CriticalHit);
    }

    [Theory]
    [InlineData("HOLY_LIGHT", "H-1-3", true)]
    [InlineData("FLASH_OF_LIGHT", "H-1-3", true)]
    [InlineData("JUDGEMENT", "R-1-2", false)]
    public async Task PaladinNumericCostModifiersChangeActualManaSpend(string abilityId, string talentId, bool self)
    {
        var plain = await Duel("PALADIN", [abilityId], [], hpPercent: 20);
        var talented = await Duel("PALADIN", [abilityId], [talentId], hpPercent: 20);
        Cast(plain, abilityId, self);
        Cast(talented, abilityId, self);
        Assert.True(talented.Source.Actor.CurrentResource > plain.Source.Actor.CurrentResource);
        Assert.True(talented.Source.Actor.CurrentResource < 100);
    }

    [Theory]
    [InlineData("DIVINE_FAVOR", "H-3-2", true, 10)]
    [InlineData("JUDGEMENT", "R-2-1", false, 2.25)]
    [InlineData("LAY_ON_HANDS", "H-3-4", true, 120)]
    [InlineData("HOLY_SHOCK", "H-4-2", true, 3)]
    [InlineData("BLESSING_OF_PROTECTION", "P-2-4", true, 10)]
    [InlineData("AVENGERS_SHIELD", "P-5-2", false, 2)]
    [InlineData("CRUSADER_STRIKE", "R-3-4", false, 1)]
    public async Task PaladinNumericCooldownModifiersChangeAuthoritativeCooldown(
        string abilityId, string talentId, bool self, decimal seconds)
    {
        var plain = await Duel("PALADIN", [abilityId], []);
        var talented = await Duel("PALADIN", [abilityId], [talentId]);
        Cast(plain, abilityId, self);
        Cast(talented, abilityId, self);
        Assert.Equal(TimeSpan.FromSeconds((double)seconds),
            plain.Session.CooldownsFor(plain.Source.AccountId)[abilityId]
            - talented.Session.CooldownsFor(talented.Source.AccountId)[abilityId]);
    }

    [Theory]
    [InlineData("HOLY_LIGHT", "H-2-2", true, 0.01)]
    [InlineData("HOLY_LIGHT", "H-5-1", true, 0.04)]
    [InlineData("JUDGEMENT", "R-1-3", false, 0.04)]
    [InlineData("JUDGEMENT", "R-5-2", false, 0.08)]
    public async Task PaladinNumericCritModifiersChangeRealHealOrDamage(
        string abilityId, string talentId, bool self, decimal roll)
    {
        var plain = await Duel("PALADIN", [abilityId], [], hpPercent: 20, roll: roll);
        var talented = await Duel("PALADIN", [abilityId], [talentId], hpPercent: 20, roll: roll);
        Cast(plain, abilityId, self);
        Cast(talented, abilityId, self);
        plain.Session.AdvanceTo(Start.AddSeconds(3));
        talented.Session.AdvanceTo(Start.AddSeconds(3));
        if (self) Assert.True(talented.Source.Actor.CurrentHp > plain.Source.Actor.CurrentHp);
        else Assert.True(talented.Target.Actor.CurrentHp < plain.Target.Actor.CurrentHp);
    }

    [Fact]
    public async Task DivineFavorSurvivesRejectedHealAndGuaranteesOnlyNextSuccessfulDirectHeal()
    {
        var duel = await Duel("PALADIN", ["HOLY_LIGHT"], ["H-3-1"], hpPercent: 20, resource: 0);
        Cast(duel, "DIVINE_FAVOR", self: true);
        Assert.False(duel.Session.UseAbility(duel.Source.AccountId, "no-mana", "HOLY_LIGHT",
            duel.Source.Actor.ActorId, Start).Succeeded);
        long sequence = duel.Session.Snapshot.Sequence;
        _ = duel.Session.AbilitySnapshotsFor(duel.Source.AccountId);
        _ = duel.Session.AbilitySnapshotsFor(duel.Source.AccountId);
        Assert.Equal(sequence, duel.Session.Snapshot.Sequence);
        Assert.Contains(duel.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_DIVINE_FAVOR_READY");
        duel.Source.Actor.ConfigureResource(100, 100);
        Cast(duel, "HOLY_LIGHT", self: true);
        duel.Session.AdvanceTo(Start.AddSeconds(3));
        Assert.Contains(duel.Session.GetEventsAfter(0), combatEvent =>
            combatEvent.DefinitionId == "HOLY_LIGHT" && combatEvent.Type == CombatEventType.HealingApplied && combatEvent.IsCritical);
        Assert.True(duel.Session.UseAbility(duel.Source.AccountId, "second-heal", "HOLY_LIGHT",
            duel.Source.Actor.ActorId, Start.AddSeconds(4)).Succeeded);
        duel.Session.AdvanceTo(Start.AddSeconds(7));
        Assert.Single(duel.Session.GetEventsAfter(0), combatEvent =>
            combatEvent.DefinitionId == "HOLY_LIGHT" && combatEvent.Type == CombatEventType.HealingApplied && combatEvent.IsCritical);
    }

    [Fact]
    public async Task LightsGraceShortensOnlyNextHolyLightWithinSixSeconds()
    {
        var duel = await Duel("PALADIN", ["HOLY_LIGHT"], ["H-4-4"], hpPercent: 20);
        Cast(duel, "HOLY_LIGHT", self: true);
        duel.Session.AdvanceTo(Start.AddSeconds(3));
        Assert.True(duel.Session.UseAbility(duel.Source.AccountId, "grace", "HOLY_LIGHT",
            duel.Source.Actor.ActorId, Start.AddSeconds(3)).Succeeded);
        var cast = Assert.IsType<ActiveCast>(duel.Session.ActiveCastFor(duel.Source.AccountId));
        Assert.Equal(TimeSpan.FromSeconds(1.75), cast.ResolvesAtUtc - cast.StartedAtUtc);
    }

    [Fact]
    public async Task ArtOfWarRankTwoMakesNextFlashInstantAndConsumesItsProc()
    {
        var duel = await Duel("PALADIN", ["CRUSADER_STRIKE", "FLASH_OF_LIGHT"], ["R-6-1"], hpPercent: 20, critical: true);
        Cast(duel, "CRUSADER_STRIKE");
        decimal before = duel.Source.Actor.CurrentHp;
        Cast(duel, "FLASH_OF_LIGHT", self: true, at: Start.AddSeconds(2));
        Assert.Null(duel.Session.ActiveCastFor(duel.Source.AccountId));
        Assert.True(duel.Source.Actor.CurrentHp > before);
        Assert.DoesNotContain(duel.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_ART_OF_WAR");
    }

    [Fact]
    public async Task SurgeOfLightRankTwoMakesOnlyNextFlashInstantAfterActualCriticalHolyShock()
    {
        var duel = await Duel("PALADIN", ["HOLY_SHOCK", "FLASH_OF_LIGHT"], ["H-6-1"],
            hpPercent: 20, critical: true);
        Cast(duel, "HOLY_SHOCK", self: true);
        Assert.Contains(duel.Session.GetEventsAfter(0), combatEvent =>
            combatEvent.DefinitionId == "HOLY_SHOCK" && combatEvent.Type == CombatEventType.HealingApplied
            && combatEvent.IsCritical);
        Assert.Contains(duel.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_SURGE_OF_LIGHT");
        _ = duel.Session.AbilitySnapshotsFor(duel.Source.AccountId);
        _ = duel.Session.AbilitySnapshotsFor(duel.Source.AccountId);
        decimal before = duel.Source.Actor.CurrentHp;
        Cast(duel, "FLASH_OF_LIGHT", self: true, at: Start.AddSeconds(2));
        Assert.Null(duel.Session.ActiveCastFor(duel.Source.AccountId));
        Assert.True(duel.Source.Actor.CurrentHp > before);
        Assert.DoesNotContain(duel.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_SURGE_OF_LIGHT");
        Assert.True(duel.Session.UseAbility(duel.Source.AccountId, "second-flash", "FLASH_OF_LIGHT",
            duel.Source.Actor.ActorId, Start.AddSeconds(4)).Succeeded);
        ActiveCast cast = Assert.IsType<ActiveCast>(duel.Session.ActiveCastFor(duel.Source.AccountId));
        Assert.True(cast.ResolvesAtUtc > cast.StartedAtUtc);
    }

    [Fact]
    public async Task HeraldThirdDirectHealMakesExactlyOneHolyShockFree()
    {
        var duel = await Duel("PALADIN", ["HOLY_LIGHT", "HOLY_SHOCK"], ["H-9-1"], hpPercent: 20);
        for (int index = 0; index < 3; index++)
        {
            Assert.True(duel.Session.UseAbility(duel.Source.AccountId, $"heal-{index}", "HOLY_LIGHT",
                duel.Source.Actor.ActorId, Start.AddSeconds(index * 3)).Succeeded);
            duel.Session.AdvanceTo(Start.AddSeconds(index * 3 + 3));
        }
        long sequence = duel.Session.Snapshot.Sequence;
        for (int query = 0; query < 2; query++)
            Assert.Equal(0, Assert.Single(duel.Session.AbilitySnapshotsFor(duel.Source.AccountId),
                ability => ability.Id == "HOLY_SHOCK").ResourceCost);
        Assert.Equal(sequence, duel.Session.Snapshot.Sequence);
        duel.Source.Actor.ConfigureResource(100, 0);
        Cast(duel, "HOLY_SHOCK", self: true, at: Start.AddSeconds(9));
        Assert.Equal(0, duel.Source.Actor.CurrentResource);
        Assert.DoesNotContain(duel.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_HERALD_HOLY_SHOCK_FREE");
        CombatAbilitySnapshot next = Assert.Single(duel.Session.AbilitySnapshotsFor(duel.Source.AccountId),
            ability => ability.Id == "HOLY_SHOCK");
        Assert.True(next.ResourceCost > 0);
        DateTimeOffset ready = duel.Session.CooldownsFor(duel.Source.AccountId)["HOLY_SHOCK"];
        Assert.False(duel.Session.UseAbility(duel.Source.AccountId, "second-shock-no-mana", "HOLY_SHOCK",
            duel.Source.Actor.ActorId, ready).Succeeded);
    }

    [Fact]
    public async Task RetributionIncarnationExtendsActualWrathEffectAndGuaranteesFirstVerdictCrit()
    {
        var plain = await Duel("PALADIN", ["AVENGING_WRATH"], []);
        var talented = await Duel("PALADIN", ["AVENGING_WRATH", "TEMPLARS_VERDICT"], ["R-9-1"]);
        Cast(plain, "AVENGING_WRATH", self: true);
        Cast(talented, "AVENGING_WRATH", self: true);
        Assert.True(talented.Source.Actor.ActiveEffects.Max(effect => effect.ExpiresAtUtc)
            > plain.Source.Actor.ActiveEffects.Max(effect => effect.ExpiresAtUtc));
        Cast(talented, "TEMPLARS_VERDICT", at: Start.AddSeconds(2));
        Assert.Contains(talented.Session.GetEventsAfter(0), combatEvent =>
            combatEvent.DefinitionId == "TEMPLARS_VERDICT" && combatEvent.Type == CombatEventType.CriticalHit);
    }

    [Fact]
    public async Task DivinePurposeDiscountAllowsLowManaVerdictWithoutSnapshotConsumptionOrPostHitRefund()
    {
        var duel = await ArmedDivinePurpose();
        duel.Source.Actor.ConfigureResource(100, 17);
        Assert.False(duel.Session.UseAbility(duel.Source.AccountId, "insufficient", "TEMPLARS_VERDICT",
            duel.Target.Actor.ActorId, Start.AddSeconds(18)).Succeeded);
        long sequence = duel.Session.Snapshot.Sequence;
        for (int query = 0; query < 2; query++)
            Assert.Equal(18, Assert.Single(duel.Session.AbilitySnapshotsFor(duel.Source.AccountId),
                ability => ability.Id == "TEMPLARS_VERDICT").ResourceCost);
        Assert.Equal(sequence, duel.Session.Snapshot.Sequence);
        Assert.Contains(duel.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_DIVINE_PURPOSE_READY");
        duel.Source.Actor.AddResource(1);
        Assert.True(duel.Session.UseAbility(duel.Source.AccountId, "discounted", "TEMPLARS_VERDICT",
            duel.Target.Actor.ActorId, Start.AddSeconds(18)).Succeeded);
        Assert.Equal(0, duel.Source.Actor.CurrentResource);
        Assert.Equal(-18, Assert.Single(duel.Session.GetEventsAfter(sequence), combatEvent =>
            combatEvent.Type == CombatEventType.ResourceChanged && combatEvent.DefinitionId == "TEMPLARS_VERDICT").Amount);
        CombatEvent bonus = Assert.Single(duel.Session.GetEventsAfter(sequence), combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt && combatEvent.DefinitionId == "PALADIN_VERDICT_BONUS");
        Assert.Equal(54, bonus.Amount);
        Assert.Equal(DamageType.Magical, bonus.DamageType);
        Assert.DoesNotContain(duel.Source.Actor.ActiveEffects, effect => effect.Definition.Id == "PALADIN_DIVINE_PURPOSE_READY");
        Assert.Equal(30, Assert.Single(duel.Session.AbilitySnapshotsFor(duel.Source.AccountId),
            ability => ability.Id == "TEMPLARS_VERDICT").ResourceCost);
        duel.Source.Actor.ConfigureResource(100, 18);
        Assert.False(duel.Session.UseAbility(duel.Source.AccountId, "second-low-mana", "TEMPLARS_VERDICT",
            duel.Target.Actor.ActorId, Start.AddSeconds(28)).Succeeded);
        duel.Source.Actor.AddResource(12);
        sequence = duel.Session.Snapshot.Sequence;
        Assert.True(duel.Session.UseAbility(duel.Source.AccountId, "ordinary", "TEMPLARS_VERDICT",
            duel.Target.Actor.ActorId, Start.AddSeconds(28)).Succeeded);
        Assert.Equal(0, duel.Source.Actor.CurrentResource);
        Assert.DoesNotContain(duel.Session.GetEventsAfter(sequence), combatEvent =>
            combatEvent.DefinitionId == "PALADIN_VERDICT_BONUS");
    }

    [Theory]
    [InlineData(35, 18, true)]
    [InlineData(36, 30, false)]
    [InlineData(37, 30, false)]
    public async Task DivinePurposeExpiresExactlyTwentySecondsAfterThirdJudgement(int seconds, decimal cost, bool empowered)
    {
        var duel = await ArmedDivinePurpose();
        Assert.Equal(Start.AddSeconds(36), Assert.Single(duel.Source.Actor.ActiveEffects,
            effect => effect.Definition.Id == "PALADIN_DIVINE_PURPOSE_READY").ExpiresAtUtc);
        duel.Session.AdvanceTo(Start.AddSeconds(seconds));
        Assert.Equal(cost, Assert.Single(duel.Session.AbilitySnapshotsFor(duel.Source.AccountId),
            ability => ability.Id == "TEMPLARS_VERDICT").ResourceCost);
        duel.Source.Actor.ConfigureResource(100, cost);
        long sequence = duel.Session.Snapshot.Sequence;
        Cast(duel, "TEMPLARS_VERDICT", at: Start.AddSeconds(seconds));
        Assert.Equal(0, duel.Source.Actor.CurrentResource);
        Assert.Equal(empowered, duel.Session.GetEventsAfter(sequence).Any(combatEvent =>
            combatEvent.DefinitionId == "PALADIN_VERDICT_BONUS" && combatEvent.Type == CombatEventType.DamageDealt));
    }

    private static async Task<DuelState> ArmedDivinePurpose()
    {
        var duel = await Duel("PALADIN", ["JUDGEMENT", "TEMPLARS_VERDICT"], ["R-8-3"]);
        for (int index = 0; index < 3; index++)
            Assert.True(duel.Session.UseAbility(duel.Source.AccountId, $"judgement-{index}", "JUDGEMENT",
                duel.Target.Actor.ActorId, Start.AddSeconds(index * 8)).Succeeded);
        return duel;
    }

    [Fact]
    public async Task DivinePurposeConsumedBySuccessfulCastDoesNotLeakBonusAfterDodgedVerdict()
    {
        var duel = await ArmedDivinePurpose();
        EffectEngine.Apply(duel.Target.Actor, duel.Target.Actor.ActorId,
            new EffectDefinition("TEST_DODGE", EffectKind.StatModifier, TimeSpan.FromSeconds(2),
                1, EffectStackPolicy.Replace, 100, ModifiedStat: EffectStat.Dodge,
                ModifierMode: EffectModifierMode.Flat), Start.AddSeconds(17));
        duel.Source.Actor.ConfigureResource(100, 18);
        long sequence = duel.Session.Snapshot.Sequence;
        Cast(duel, "TEMPLARS_VERDICT", at: Start.AddSeconds(18));
        Assert.Equal(0, duel.Source.Actor.CurrentResource);
        Assert.Contains(duel.Session.GetEventsAfter(sequence), combatEvent =>
            combatEvent.DefinitionId == "TEMPLARS_VERDICT" && combatEvent.Type == CombatEventType.Dodge);
        Assert.DoesNotContain(duel.Session.GetEventsAfter(sequence), combatEvent =>
            combatEvent.DefinitionId == "PALADIN_VERDICT_BONUS");
        duel.Source.Actor.ConfigureResource(100, 30);
        sequence = duel.Session.Snapshot.Sequence;
        Assert.True(duel.Session.UseAbility(duel.Source.AccountId, "after-dodge", "TEMPLARS_VERDICT",
            duel.Target.Actor.ActorId, Start.AddSeconds(28)).Succeeded);
        Assert.Contains(duel.Session.GetEventsAfter(sequence), combatEvent =>
            combatEvent.DefinitionId == "TEMPLARS_VERDICT" && combatEvent.Type == CombatEventType.DamageDealt);
        Assert.DoesNotContain(duel.Session.GetEventsAfter(sequence), combatEvent =>
            combatEvent.DefinitionId == "PALADIN_VERDICT_BONUS");
    }

    [Fact]
    public async Task IncarnationGuaranteesExactlyOneVerdictCritInEachSuccessfulWrathWindow()
    {
        var duel = await Duel("PALADIN", ["AVENGING_WRATH", "TEMPLARS_VERDICT"], ["R-9-1"]);
        foreach (int window in new[] { 0, 120 })
        {
            if (window != 0)
            {
                duel.Session.AdvanceTo(Start.AddSeconds(window));
                duel.Source.Actor.AddResource(100);
            }
            Assert.True(duel.Session.UseAbility(duel.Source.AccountId, $"wrath-{window}", "AVENGING_WRATH",
                duel.Source.Actor.ActorId, Start.AddSeconds(window)).Succeeded);
            for (int index = 0; index < 2; index++)
            {
                // Projection must not consume the next guaranteed critical strike.
                _ = duel.Session.AbilitySnapshotsFor(duel.Source.AccountId);
                _ = duel.Session.AbilitySnapshotsFor(duel.Source.AccountId);
                DateTimeOffset at = Start.AddSeconds(window + 2 + index * 10);
                long sequence = duel.Session.Snapshot.Sequence;
                Assert.True(duel.Session.UseAbility(duel.Source.AccountId, $"verdict-{window}-{index}",
                    "TEMPLARS_VERDICT", duel.Target.Actor.ActorId, at).Succeeded);
                bool critical = duel.Session.GetEventsAfter(sequence).Any(combatEvent =>
                    combatEvent.DefinitionId == "TEMPLARS_VERDICT" && combatEvent.Type == CombatEventType.CriticalHit);
                Assert.Equal(index == 0, critical);
            }
        }
    }

    [Fact]
    public async Task SacredCleansingDoesNotHealWhenNoDebuffWasDispelled()
    {
        var duel = await Duel("PALADIN", ["CLEANSE"], ["H-7-2"], hpPercent: 50);
        decimal before = duel.Source.Actor.CurrentHp;
        Cast(duel, "CLEANSE", self: true);
        Assert.Equal(before, duel.Source.Actor.CurrentHp);
        Assert.DoesNotContain(duel.Session.GetEventsAfter(0),
            combatEvent => combatEvent.Type == CombatEventType.HealingApplied
                && combatEvent.DefinitionId == "H-7-2");
    }

    [Fact]
    public async Task ImprovedLayOnHandsRestoresTenPercentMaximumMana()
    {
        var duel = await Duel("PALADIN", ["LAY_ON_HANDS"], ["H-3-4"], hpPercent: 50, resource: 40);
        Cast(duel, "LAY_ON_HANDS", self: true);
        Assert.Equal(50, duel.Source.Actor.CurrentResource);
    }

    [Fact]
    public async Task LastLightProtectsTargetAfterEffectiveLayOnHandsHealing()
    {
        var duel = await Duel("PALADIN", ["LAY_ON_HANDS"], ["H-8-3"], hpPercent: 50);
        Cast(duel, "LAY_ON_HANDS", self: true);
        Assert.Contains(duel.Source.Actor.ActiveEffects, effect =>
            effect.Definition.Id == "PALADIN_LAST_LIGHT_GUARD"
            && effect.Definition.Magnitude == 0.85m);
    }

    [Fact]
    public async Task BulwarkActuallyGrantsBlockChanceAfterIncomingDamage()
    {
        var duel = await Duel("PALADIN", [], ["P-2-1"], roll: 0);
        AttackSource(duel);
        Assert.Contains(duel.Source.Actor.ActiveEffects, effect =>
            effect.Definition.Id == "PALADIN_BULWARK_BLOCK"
            && effect.Definition.Magnitude == 10m);
    }

    [Fact]
    public async Task ShieldOfFaithCreatesAbsorbAfterNaturalHolyShieldExpiration()
    {
        var duel = await Duel("PALADIN", ["HOLY_SHIELD"], ["P-4-4"], hpPercent: 90);
        Cast(duel, "HOLY_SHIELD", self: true);
        Assert.DoesNotContain(duel.Source.Actor.ActiveEffects,
            effect => effect.Definition.Id == "PALADIN_SHIELD_OF_FAITH");
        duel.Session.AdvanceTo(Start.AddSeconds(8.01));
        Assert.Contains(duel.Source.Actor.ActiveEffects,
            effect => effect.Definition.Id == "PALADIN_SHIELD_OF_FAITH"
                && effect.Definition.Magnitude == 800m);
    }

    [Fact]
    public async Task SanctuaryMasterRefundsManaWhenProtectedPaladinActuallyTakesDamage()
    {
        var duel = await Duel("PALADIN", ["BLESSING_OF_SANCTUARY"], ["P-5-4"]);
        Cast(duel, "BLESSING_OF_SANCTUARY", self: true);
        decimal manaBefore = duel.Source.Actor.CurrentResource;
        AttackSource(duel);
        Assert.True(duel.Source.Actor.CurrentResource > manaBefore);
    }

    [Fact]
    public async Task PerfectSanctuaryGrantsShieldedSelfAdditionalBlockChance()
    {
        var duel = await Duel("PALADIN", ["BLESSING_OF_SANCTUARY"], ["P-8-3"]);
        Cast(duel, "BLESSING_OF_SANCTUARY", self: true);
        Assert.Contains(duel.Source.Actor.ActiveEffects, effect =>
            effect.Definition.Id == "PALADIN_PERFECT_SANCTUARY_BLOCK");
    }

    [Theory]
    [InlineData(300, 30)]
    [InlineData(0, 0)]
    public async Task ToughnessUsesCapturedEquipmentArmorNotTotalActorArmor(decimal equipmentArmor, decimal bonus)
    {
        var plain = await Duel("PALADIN", [], [], armor: 500, equipmentArmor: equipmentArmor);
        var talented = await Duel("PALADIN", [], ["P-1-1"], armor: 500, equipmentArmor: equipmentArmor);
        Assert.Equal(500 + bonus, EffectEngine.CalculateStat(talented.Source.Actor, EffectStat.Armor,
            talented.Source.Actor.Stats.Armor, Start));
        AttackSource(plain);
        AttackSource(talented);
        Assert.True(talented.Source.Actor.CurrentHp >= plain.Source.Actor.CurrentHp);
        if (bonus > 0)
            Assert.Equal(bonus, Assert.Single(talented.Source.Actor.ActiveEffects,
                effect => effect.Definition.Id == "PALADIN_TOUGHNESS_ARMOR").Definition.Magnitude);
        else
            Assert.DoesNotContain(talented.Source.Actor.ActiveEffects,
                effect => effect.Definition.Id == "PALADIN_TOUGHNESS_ARMOR");
    }

    [Theory]
    [InlineData(EquipmentCategoryIds.TwoHandSword, true)]
    [InlineData(EquipmentCategoryIds.OneHandSword, false)]
    [InlineData(null, false)]
    public async Task TwoHandedSpecializationChangesPhysicalAbilityAndAutoAttackOnlyWithCapturedTwoHandWeapon(
        string? weaponCategory, bool specialized)
    {
        var plain = await Duel("PALADIN", ["TEMPLARS_VERDICT"], [], autoAttack: true, weaponCategory: weaponCategory);
        var talented = await Duel("PALADIN", ["TEMPLARS_VERDICT"], ["R-2-3"], autoAttack: true, weaponCategory: weaponCategory);
        Cast(plain, "TEMPLARS_VERDICT");
        Cast(talented, "TEMPLARS_VERDICT");
        decimal VerdictDamage(DuelState state) => state.Session.GetEventsAfter(0)
            .Where(combatEvent => combatEvent.DefinitionId == "TEMPLARS_VERDICT"
                && combatEvent.Type == CombatEventType.DamageDealt).Sum(combatEvent => combatEvent.Amount);
        Assert.Equal(217, VerdictDamage(plain));
        Assert.Equal(specialized ? 237 : 217, VerdictDamage(talented));
        plain.Session.AdvanceTo(Start.AddSeconds(3));
        talented.Session.AdvanceTo(Start.AddSeconds(3));
        decimal[] AutoDamage(DuelState state) => state.Session.GetEventsAfter(0)
            .Where(combatEvent => combatEvent.DefinitionId == "AUTO_ATTACK"
                && combatEvent.Type == CombatEventType.DamageDealt).Select(combatEvent => combatEvent.Amount).ToArray();
        decimal[] ordinaryHits = AutoDamage(plain);
        Assert.NotEmpty(ordinaryHits);
        Assert.Equal(ordinaryHits.Select(amount => specialized
            ? decimal.Round(amount * 1.09m, 0, MidpointRounding.AwayFromZero) : amount), AutoDamage(talented));
    }

    private static async Task<DuelState> Duel(string classId, string[] known, string[] talents,
        int hpPercent = 100, decimal resource = 100, bool shield = false, bool critical = false,
        bool autoAttack = false, decimal roll = 0.1m, decimal armor = 0,
        decimal equipmentArmor = 0, string? weaponCategory = null)
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        TalentTreeDefinition tree = Assert.Single(package.TalentTrees!, tree => tree.ClassId == classId);
        var ranks = talents.ToDictionary(id => id, id => Assert.Single(tree.Nodes, node => node.Id == id).MaxRank,
            StringComparer.Ordinal);
        ResolvedTalentModifiers modifiers = TalentModifierResolver.Resolve(tree, ranks);
        var abilities = package.Abilities!.ToDictionary(ability => ability.Id, StringComparer.Ordinal);
        var stats = CombatStats.Default with { Level = 30, Accuracy = 100, AttackPower = 100,
            SpellPower = 100, Armor = armor, CriticalChance = critical ? 100 : 0, CriticalDamage = 1,
            BlockChance = shield ? 100 : 0, BlockValueMin = shield ? 500 : 0, BlockValueMax = shield ? 500 : 0 };
        var actor = new CombatActorState(Guid.NewGuid(), 10000, hpPercent * 100, 100, resource,
            stats, modifiers.Combat);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player, classId,
            "Production arena fighter", classId == "WARRIOR" ? "RAGE" : classId == "ARCHER" ? "FOCUS" : "MANA",
            new AutoAttackProfile(TimeSpan.FromSeconds(3), 50, 1, 0),
            new HashSet<string>(known, StringComparer.Ordinal), CanAutoAttack: autoAttack,
            EquipmentArmor: equipmentArmor, MainHandWeaponCategory: weaponCategory);
        ArenaFighter source = ArenaFighterAssembler.Create(new CombatPlayerDefinition(Guid.NewGuid(), participant, modifiers),
            30, abilities, hasCompanion: false).Fighter;
        var targetActor = new CombatActorState(Guid.NewGuid(), 10000, 10000, 100, 100,
            CombatStats.Default with { Level = 30, Accuracy = 100, AttackPower = 100 });
        var targetParticipant = new CombatParticipantDefinition(targetActor, CombatActorKind.Player, "WARRIOR",
            "Opponent", "RAGE", new AutoAttackProfile(TimeSpan.FromSeconds(3), 50, 0, 0),
            new HashSet<string>(["STRIKE"], StringComparer.Ordinal), CanAutoAttack: false);
        ArenaFighter target = ArenaFighterAssembler.Create(new CombatPlayerDefinition(Guid.NewGuid(), targetParticipant,
            ResolvedTalentModifiers.Empty), 30, abilities, hasCompanion: false).Fighter;
        return new DuelState(new ArenaCombatSession(Guid.NewGuid(), source, target, new FixedRandom(roll), Start), source, target);
    }

    private static void Cast(DuelState duel, string abilityId, bool self = false, DateTimeOffset? at = null)
    {
        ArenaCommandResult result = duel.Session.UseAbility(duel.Source.AccountId, abilityId, abilityId,
            self ? duel.Source.Actor.ActorId : duel.Target.Actor.ActorId, at ?? Start);
        Assert.True(result.Succeeded, $"{abilityId}: {result.ErrorCode}");
    }

    private static void AttackSource(DuelState duel, DateTimeOffset? at = null, string command = "incoming")
    {
        ArenaCommandResult result = duel.Session.UseAbility(duel.Target.AccountId, command, "STRIKE",
            duel.Source.Actor.ActorId, at ?? Start);
        Assert.True(result.Succeeded, result.ErrorCode);
    }

    private sealed class FixedRandom(decimal value) : IGameRandom
    {
        public decimal NextUnit() => value;
    }

    private static string RepositoryContentPath()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
