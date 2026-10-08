using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;

namespace Elyndor.ContentValidator;

public sealed record AffixBalanceMetrics(decimal PhysicalAutoDps, decimal DirectSpellDps, decimal DirectHps,
    decimal PhysicalEffectiveHp, decimal MagicalEffectiveHp, decimal CompanionDps);
public sealed record AffixBalanceRow(int Level, string ClassId, string Scenario, string StatId, decimal PowerBudget,
    decimal StatValue, CharacterStats Stats, AffixBalanceMetrics Metrics);

public static class AffixBalanceBenchmark
{
    private static readonly string[] BaselineStatIds = ["BASELINE"];
    public static IReadOnlyList<AffixBalanceRow> Run(GameContentPackage package)
    {
        if (package.StatFormula is null || package.Itemization is null) return [];
        CharacterStatCalculator calculator = new(package.StatFormula, package.ClassProfiles ?? []);
        List<AffixBalanceRow> rows = [];
        foreach (int level in new[] { 10, 20, 30, 40, 60 })
        foreach (ClassProfile profile in package.ClassProfiles ?? [])
        foreach (string scenario in new[] { "ordinary", "near-caps", "at-caps" })
        {
            decimal budget = package.Itemization.TemplateBasePower * ItemFamilyScalingPolicy.LevelMultiplier(package.Itemization, level) / 10m;
            decimal defenseConstant = DefenseMitigationFormula.BaseMitigationConstant + DefenseMitigationFormula.MitigationConstantPerLevel * level;
            CharacterEquipmentDerivedModifiers baseline = new(ArmorFlat: defenseConstant / 2, MagicResistanceFlat: defenseConstant / 2);
            CharacterStats bare = calculator.Calculate(profile.Id, level);
            if (profile.Id is "WARRIOR" or "PALADIN")
                baseline = baseline with { BlockChancePercent = 20, BlockValueMin = 5 + level / 2m, BlockValueMax = 5 + level / 2m };
            if (scenario != "ordinary")
                baseline = baseline with
                {
                    CriticalChancePercent = (scenario == "at-caps" ? 60 : 58) - bare.CriticalChance,
                    AttackSpeedPercent = scenario == "at-caps" ? 50 : 48,
                    DodgePercent = (scenario == "at-caps" ? 35 : 33) - bare.Dodge,
                    AccuracyPercent = (scenario == "at-caps" ? 100 : 99) - bare.Accuracy,
                    ArmorPenetrationPercent = scenario == "at-caps" ? 100 : 98,
                    MagicPenetrationPercent = scenario == "at-caps" ? 100 : 98,
                    BlockChancePercent = baseline.BlockChancePercent == 0 ? 0 : scenario == "at-caps" ? 60 : 58,
                    ArmorFlat = defenseConstant * (scenario == "at-caps" ? 1.5m : 1.45m) - bare.Armor,
                    MagicResistanceFlat = defenseConstant * (scenario == "at-caps" ? 1.5m : 1.45m) - bare.MagicResistance
                };
            foreach (string statId in BaselineStatIds.Concat(package.Itemization.StatPowerWeights.Keys))
            {
                decimal value = statId == "BASELINE" ? 0 : budget / package.Itemization.StatPowerWeights[statId];
                ItemDefinition probe = new("PROBE", "Probe", ItemType.Equipment, ItemRarity.Common, level, false, 1,
                    EquipmentSlot.MainHand, new PrimaryStats(0, 0, 0, 0), "", WeaponDamageMin: 20, WeaponDamageMax: 20);
                ItemDefinition added = ItemInstanceGenerator.ApplyGeneratedAffixes(probe,
                    [new("A", statId, statId, value, 0, value, 0.1m, 1, false, false, 0)]);
                CharacterEquipmentDerivedModifiers bonus = baseline with
                {
                    MaxHpFlat = added.MaxHpFlat, AttackPowerFlat = added.AttackPowerFlat, SpellPowerFlat = added.SpellPowerFlat,
                    CriticalChancePercent = baseline.CriticalChancePercent + added.CriticalChancePercent,
                    CriticalDamagePercent = added.CriticalDamagePercent, AccuracyPercent = baseline.AccuracyPercent + added.AccuracyPercent,
                    AttackSpeedPercent = baseline.AttackSpeedPercent + added.AttackSpeedPercent,
                    DodgePercent = baseline.DodgePercent + added.DodgePercent,
                    ArmorFlat = baseline.ArmorFlat + added.ArmorFlat, MagicResistanceFlat = baseline.MagicResistanceFlat + added.MagicResistanceFlat,
                    ArmorPenetrationPercent = baseline.ArmorPenetrationPercent + added.ArmorPenetrationPercent,
                    MagicPenetrationPercent = baseline.MagicPenetrationPercent + added.MagicPenetrationPercent,
                    BlockChancePercent = baseline.BlockChancePercent + added.BlockChancePercent,
                    BlockValueMin = baseline.BlockValueMin + added.BlockValueMin,
                    BlockValueMax = baseline.BlockValueMax + added.BlockValueMax
                };
                CharacterStats stats = calculator.Calculate(profile.Id, level, CharacterStatInputs.Empty with { Equipment = added.Stats, EquipmentDerived = bonus });
                rows.Add(new(level, profile.Id, scenario, statId, budget, value, stats,
                    Measure(level, stats, profile, added.WeaponDamageMin ?? 20)));
            }
        }
        return rows;
    }

    public static AffixBalanceMetrics Measure(int level, CharacterStats stats, ClassProfile profile, decimal weaponDamage)
    {
        CombatStats combat = new(level, stats.Accuracy, stats.Dodge, stats.CriticalChance, stats.CriticalDamage / 100m,
            stats.Armor, stats.MagicResistance, stats.ArmorPenetration / 100m, stats.MagicPenetration / 100m,
            stats.AttackPower, stats.SpellPower, stats.BlockChance, stats.BlockValueMin, stats.BlockValueMax);
        CombatActorState source = new(Guid.NewGuid(), stats.MaxHp, stats.MaxHp, 100, 100, combat);
        decimal defense = (50 + 40 * level) / 2m;
        CombatActorState target = new(Guid.NewGuid(), 1_000_000_000, 1_000_000_000, 0, 0,
            CombatStats.Default with { Level = level, Armor = defense, MagicResistance = defense, Dodge = 10 });
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        decimal Expected(CombatActorState actor, decimal amount, DamageType type)
        {
            // Stratify avoidance rolls; average critical outcomes exactly. This uses the
            // production damage path, including its mitigation, rounding and miss rules.
            decimal total = 0;
            for (int index = 0; index < 100; index++)
            {
                decimal roll = (index + 0.5m) / 100;
                DamageRequest request = new(actor, target, amount, type, CanCrit: false, CanBlock: false);
                decimal ordinary = DamagePipeline.Resolve(request, new SequenceGameRandom(roll), now).HpDamage;
                decimal critical = DamagePipeline.Resolve(request with { ForceCritical = true }, new SequenceGameRandom(roll), now).HpDamage;
                decimal chance = decimal.Clamp(actor.Stats.CriticalChance / 100m, 0, 1);
                total += ordinary * (1 - chance) + critical * chance;
            }
            return total / 100;
        }
        decimal healing = HealingPipeline.Resolve(new(target, 100, Source: source, SpellPowerCoefficient: 0.7m)).ModifiedAmount;
        decimal criticalHealing = HealingPipeline.Resolve(new(target, 100, Source: source, SpellPowerCoefficient: 0.7m, ForceCritical: true)).ModifiedAmount;
        decimal critChance = decimal.Clamp(stats.CriticalChance / 100m, 0, 1);
        decimal petDps = 0;
        if (profile.CompanionProfiles is { Count: > 0 } companions)
        {
            CompanionProfileDefinition companion = companions[0];
            CombatParticipantDefinition pet = ArcherCompanionRuntimeResolver.Resolve(companion, stats, level, ResolvedTalentModifiers.Empty);
            petDps = Expected(pet.Actor, (companion.BaseDamageMin + companion.BaseDamageMax) / 2m
                + pet.Actor.Stats.AttackPower, DamageType.Physical) / (decimal)companion.AutoAttackInterval.TotalSeconds;
        }
        decimal incoming = 100 + 10 * level;
        CombatActorState defender = new(Guid.NewGuid(), 1_000_000_000, 1_000_000_000, 0, 0, combat);
        DamageRequest incomingRequest = new(target, defender, incoming, DamageType.Physical, CanMiss: false, CanDodge: false, CanCrit: false);
        decimal unblocked = DamagePipeline.Resolve(incomingRequest, new SequenceGameRandom(0.999m, 0.5m), now).HpDamage;
        decimal blocked = DamagePipeline.Resolve(incomingRequest, new SequenceGameRandom(0m, 0.5m), now).HpDamage;
        decimal expectedIncoming = (unblocked * (1 - stats.BlockChance / 100m) + blocked * stats.BlockChance / 100m) * (1 - stats.Dodge / 100m);
        decimal magicalIncoming = DamagePipeline.Resolve(incomingRequest with { Type = DamageType.Magical, CanBlock = false }, new SequenceGameRandom(), now).HpDamage;
        return new(
            Expected(source, weaponDamage + stats.AttackPower * 0.65m, DamageType.Physical) / 2.4m * stats.AttackSpeed,
            Expected(source, 100 + stats.SpellPower * 0.7m, DamageType.Magical) / 2.5m,
            (healing * (1 - critChance) + criticalHealing * critChance) / 2.5m,
            stats.MaxHp * incoming / Math.Max(1, expectedIncoming),
            stats.MaxHp * incoming / Math.Max(1, magicalIncoming), petDps);
    }
}
