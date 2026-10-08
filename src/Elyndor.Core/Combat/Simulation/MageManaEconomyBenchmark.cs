using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Simulation;

public sealed record MageManaEconomyResult(int Level, string BranchId, CombatSimulationGearState Gear,
    decimal MaxMana, decimal? SecondsToOom, decimal RemainingMana, decimal FailedSpellCost,
    int Casts, decimal ResourceSpent, decimal ResourceGained, IReadOnlyDictionary<string, int> TalentRanks);

public sealed partial class CombatSimulationRunner
{
    private static readonly Dictionary<string, string[]> MageManaTalentPriorities = new()
    {
        ["FIRE"] = ["F-1-1", "F-1-4", "F-1-2", "F-2-1", "F-2-3", "F-2-4", "F-3-1", "F-3-2", "F-3-3", "F-3-4", "F-4-1", "F-4-2", "F-5-2", "F-5-3", "F-5-4", "F-6-1", "F-6-2", "F-6-4", "F-7-1", "F-7-2", "F-7-4", "F-8-1", "F-8-2", "F-8-3", "F-9-1"],
        ["ARCANE"] = ["A-1-1", "A-1-2", "A-1-3", "A-2-1", "A-2-2", "A-2-3", "A-4-2", "A-4-3", "A-5-1", "A-5-2", "A-5-3", "A-6-1", "A-6-2", "A-6-3", "A-6-4", "A-7-1", "A-7-2", "A-7-3", "A-7-4", "A-8-1", "A-8-2", "A-8-3", "A-9-1"],
        ["FROST"] = ["I-1-1", "I-1-2", "I-1-3", "I-1-4", "I-2-1", "I-2-2", "I-2-3", "I-2-4", "I-3-1", "I-3-4", "I-5-1", "I-6-1", "I-6-2", "I-7-1", "I-7-2", "I-7-4", "I-8-1", "I-8-2", "I-9-1"]
    };
    public MageManaEconomyResult RunMageManaEconomy(int level, string branchId,
        CombatSimulationGearState gear = CombatSimulationGearState.Normal, int durationSeconds = 600,
        int seed = 1337, bool useTalents = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 60);
        ArgumentOutOfRangeException.ThrowIfLessThan(durationSeconds, 1);
        if (branchId is not ("FIRE" or "ARCANE" or "FROST")) throw new ArgumentException("Unknown Mage branch.", nameof(branchId));
        var profile = content.ClassProfiles!.Single(p => p.Id == "MAGE");
        var tree = content.TalentTrees!.Single(t => t.ClassId == "MAGE");
        IReadOnlyDictionary<string, int> ranks = new Dictionary<string, int>();
        if (useTalents)
        {
            // Legal sustained-damage build, with a stable priority for useful offensive
            // and economy talents rather than filling defensive nodes by ID order.
            while (ranks.Values.Sum() < level - 1)
            {
                TalentLearnResult? learned = MageManaTalentPriorities[branchId]
                    .Concat(tree.Nodes.Where(n => n.BranchId == branchId).Select(n => n.Id))
                    .Select(id => TalentRules.TryLearn(tree, level, ranks, id)).FirstOrDefault(r => r.IsSuccess);
                if (learned is null) break;
                ranks = learned.SelectedRanks;
            }
        }
        var talents = TalentModifierResolver.Resolve(tree, ranks);
        var scenario = new CombatSimulationScenario("MAGE", level, "MANA_BENCHMARK", GearState: gear);
        var equipment = ResolveSimulationEquipment(scenario, profile, preferManaBudget: true);
        var modifiers = equipment.Modifiers;
        var stats = new CharacterStatCalculator(content.StatFormula!, content.ClassProfiles!).Calculate("MAGE", level,
            CharacterStatInputs.Empty with
            {
                Equipment = modifiers.PrimaryStats,
                EquipmentDerived = new(SpellPowerFlat: modifiers.SpellPowerFlat,
                    CriticalChancePercent: modifiers.CriticalChancePercent, CriticalDamagePercent: modifiers.CriticalDamagePercent,
                    MagicPenetrationPercent: modifiers.MagicPenetrationPercent),
                TalentPercentages = new(talents.Stats.StrengthPercent, talents.Stats.AgilityPercent,
                    talents.Stats.IntellectPercent, talents.Stats.StaminaPercent), TalentDerived = talents.Stats
            });
        var resource = CharacterResourceProfileResolver.Resolve(content.ResourceProfiles!.Single(p => p.Id == "MANA"),
            content.ResourceScaling, stats, talents.Stats.MaxResourceFlat + modifiers.MaxResourceFlat,
            talents.Stats.MaxResourcePercent);
        var abilities = content.Abilities!.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var known = CharacterKnownAbilityResolver.Resolve(profile, level, talents.UnlockedAbilityIds).ToHashSet(StringComparer.Ordinal);
        var owner = new CombatActorState(Guid.NewGuid(), 1_000_000, 1_000_000, resource.MaxValue, resource.MaxValue, ToCombatStats(level, stats));
        var enemy = new CombatActorState(Guid.NewGuid(), 1_000_000_000, 1_000_000_000, 0, 0,
            CombatStats.Default with { Level = level, Dodge = 0, Armor = 0, MagicResistance = 0 });
        CombatParticipantDefinition Participant(CombatActorState actor, bool player) => new(actor,
            player ? CombatActorKind.Player : CombatActorKind.Monster, player ? "MAGE" : "MANA_BENCHMARK", "Mana benchmark",
            player ? "MANA" : "NONE", new(TimeSpan.FromHours(1), 0, 0, 0), player ? known : new HashSet<string>(),
            ResourceRegenPerSecond: player ? resource.CombatRegenPerSecond : 0, CanAutoAttack: false,
            EquippedSetPieces: player ? equipment.SetPieces : null,
            SetPassives: player ? Items.EquipmentSetEffectResolver.Resolve(content.EquipmentSets ?? []) : null);
        var session = new CombatSession(Guid.NewGuid(), Participant(owner, true), Participant(enemy, false), abilities,
            new MonsterAiProfile("MANA_BENCHMARK", []), talents, new SeededSimulationRandom(seed), SimulationEpoch);
        string filler = branchId switch { "FIRE" => "MAGE_FIREBALL", "ARCANE" => "MAGE_ARCANE_SPARK", _ => "MAGE_ICE_SHARD" };
        // Utility/defense, potions and Evocation are deliberately excluded. Maintenance
        // spells are refreshed at a fixed interval, not spammed ahead of the filler.
        (string Id, decimal Interval)[] rotation = branchId switch
        {
            "FIRE" => [("MAGE_SCORCH", 10), ("MAGE_FIRE_BLAST", 8), (filler, 0)],
            "ARCANE" => [("MAGE_ARCANE_MISSILES", 0), (filler, 0)],
            _ => [("MAGE_ICE_LANCE", 6), (filler, 0)]
        };
        var lastUse = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal? oom = null;
        decimal failedCost = 0;
        int casts = 0, command = 0;
        DateTimeOffset castReadyAt = SimulationEpoch;
        for (decimal seconds = 0; seconds < durationSeconds; seconds += .1m)
        {
            DateTimeOffset now = SimulationEpoch.AddSeconds((double)seconds);
            session.AdvanceTo(now);
            if (owner.IsDead) throw new InvalidOperationException("Mana benchmark actor died.");
            if (now < castReadyAt) continue;
            var currentRotation = rotation;
            if (branchId == "FIRE" && known.Contains("MAGE_PYROBLAST")
                && owner.ActiveEffects.Any(effect => effect.Definition.Id == "MAGE_HOT_STREAK" && effect.ExpiresAtUtc > now))
                currentRotation = [("MAGE_PYROBLAST", 0), .. rotation];
            foreach ((string id, decimal interval) in currentRotation)
            {
                if (!known.Contains(id) || lastUse.TryGetValue(id, out decimal last) && seconds - last < interval) continue;
                var result = session.Handle(new UseAbilityCommand($"mana:{command++}", id, enemy.ActorId), now);
                if (result.Succeeded)
                {
                    lastUse[id] = seconds;
                    castReadyAt = session.Snapshot().Player.ActiveCast?.ResolvesAtUtc ?? now;
                    casts++;
                    break;
                }
                if (result.ErrorCode == CombatErrorCodes.InsufficientResource)
                {
                    // Stop on the first unaffordable scheduled rotation spell; falling
                    // back to a cheap filler would conceal lost rotation uptime.
                    oom = seconds;
                    failedCost = session.Snapshot().Player.Abilities.Single(a => a.Id == id).ResourceCost;
                    break;
                }
            }
            if (oom is not null) break;
        }
        var totals = session.Snapshot().Statistics!;
        return new(level, branchId, gear, resource.MaxValue, oom, owner.CurrentResource, failedCost,
            casts, totals.ResourceSpent, totals.ResourceGained, ranks);
    }
}
