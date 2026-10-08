using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.SetPassives;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class SetEffectRuntimeContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(35)]
    [InlineData(45)]
    public async Task StrongStandaloneRollCanBeatAWeakLevelingSetPiece(int level)
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        foreach (string classId in new[] { "WARRIOR", "MAGE", "ARCHER", "PALADIN" })
        {
            var standalone = package.Items!.Single(i => i.RequiredLevel == 23 && i.Rarity == ItemRarity.Legendary
                && i.Slot == EquipmentSlot.Chest && i.AllowedClassIds is { Count: 1 } && i.AllowedClassIds[0] == classId);
            var setPiece = package.Items!.Single(i => i.RequiredLevel == level && i.SetId is not null
                && i.Slot == EquipmentSlot.Chest && i.AllowedClassIds is { Count: 1 } && i.AllowedClassIds[0] == classId);
            GeneratedItemInstance Roll(ItemDefinition item, decimal unit) => ItemInstanceGenerator.Generate(item,
                ItemizationBudgetPolicy.NormalizeForTemplate(item, package.Itemization!), "NORMAL",
                new ConstantRandom(unit), overrides: new(level, level));
            Assert.True(Roll(standalone, .999999m).ActualItemPower > Roll(setPiece, 0).ActualItemPower,
                $"Standalone choice disappeared for {classId} L{level}.");
        }
    }

    private sealed class ConstantRandom(decimal unit) : IGameRandom
    {
        public decimal NextUnit() => unit;
    }

    [Fact]
    public async Task FlatBonusPowerGrowsAcrossAllProgressionTiers()
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var weights = package.Itemization!.StatPowerWeights;
        decimal FlatPower(EquipmentSetBonusDefinition b) => b.MaxHpFlat * weights["MAX_HP"]
            + b.MaxResourceFlat * weights["MAX_RESOURCE"] + b.AttackPowerFlat * weights["ATTACK_POWER"]
            + b.SpellPowerFlat * weights["SPELL_POWER"] + b.ArmorFlat * weights["ARMOR"]
            + b.MagicResistanceFlat * weights["MAGIC_RESISTANCE"];
        decimal previous = 0;
        foreach (int level in new[] { 18, 35, 45, 55, 60 })
        {
            var setIds = package.Items!.Where(i => i.RequiredLevel == level && i.SetId is not null
                && (level != 60 || i.SetId.StartsWith("SET_L60_NORMAL_", StringComparison.Ordinal)))
                .Select(i => i.SetId).ToHashSet();
            decimal power = package.EquipmentSets!.Where(s => setIds.Contains(s.Id))
                .SelectMany(s => s.Bonuses).Max(FlatPower);
            Assert.True(power > previous, $"Flat bonus budget regressed at L{level}.");
            previous = power;
        }
        Assert.All(package.EquipmentSets!.Where(s => s.Id.StartsWith("SET_L60_PVE_T1_", StringComparison.Ordinal)),
            set => Assert.All(set.Bonuses.Where(b => b.RequiredPieces >= 4), b => Assert.Equal(0, FlatPower(b))));
        Assert.All(package.EquipmentSets!.Where(s => s.Id.StartsWith("SET_L60_NORMAL_", StringComparison.Ordinal)
            || s.Id.StartsWith("SET_L60_PVP_T1_", StringComparison.Ordinal)), s => Assert.Empty(s.SpecialEffects ?? []));
    }

    [Fact]
    public async Task InvalidCooldownReferenceCannotBePublished()
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var set = package.EquipmentSets!.Single(s => s.Id == "SET_L60_PVE_T1_MAGE_ARCANE");
        var effects = set.SpecialEffects!.ToArray();
        effects[1] = effects[1] with { Actions = [new(SetPassiveActionKind.ReduceCooldown, "MISSING_ABILITY", 1)] };
        var invalid = package with { EquipmentSets = package.EquipmentSets!.Select(s => s.Id == set.Id
            ? s with { SpecialEffects = effects } : s).ToArray() };
        Assert.Contains(Elyndor.Core.Content.GameContentPackageValidator.Validate(invalid),
            e => e.Code == "INVALID_SET_SPECIAL_EFFECT");
    }

    [Theory]
    [InlineData("WARRIOR_GUARDIAN", "SHIELD_BLOCK", "REVENGE", 1.25)]
    [InlineData("WARRIOR_BERSERKER", "WILD_STRIKE", "AUTO_ATTACK", 1.20)]
    [InlineData("WARRIOR_WARLORD", "BATTLE_STANDARD", "BATTLE_CRY", 1.25)]
    [InlineData("MAGE_FIRE", "MAGE_FIREBALL", "MAGE_PYROBLAST", 1.20)]
    [InlineData("MAGE_FROST", "MAGE_ICE_SHARD", "MAGE_ICE_LANCE", 1.25)]
    [InlineData("ARCHER_MARKSMAN", "QUICK_SHOT", "AIMED_SHOT", 1.20)]
    [InlineData("ARCHER_BEAST_MASTERY", "AUTO_ATTACK", "QUICK_SHOT", 1.15)]
    [InlineData("ARCHER_SURVIVAL", "SERPENT_STING", "QUICK_SHOT", 1.20)]
    [InlineData("PALADIN_HOLY", "HOLY_LIGHT", "HOLY_SHOCK", 1.20)]
    [InlineData("PALADIN_PROTECTION", "AUTO_ATTACK", "AUTO_ATTACK", 1.20)]
    [InlineData("PALADIN_RETRIBUTION", "JUDGEMENT", "TEMPLARS_VERDICT", 1.20)]
    public async Task BundledBranchMechanicsReachTheAuthoritativeRuntime(
        string branch, string triggerAbility, string empoweredAbility, decimal expected)
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        string setId = $"SET_L60_PVE_T1_{branch}";
        var owner = CombatActorState.CreateDummy(1000);
        var target = CombatActorState.CreateDummy(1000);
        var pet = CombatActorState.CreateDummy(1000);
        var actors = new[] { owner, target, pet }.ToDictionary(a => a.ActorId);
        var pieces = new Dictionary<Guid, IReadOnlyDictionary<string, int>>
            { [owner.ActorId] = new Dictionary<string, int> { [setId] = 6 } };
        Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal);
        var abilities = package.Abilities!.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var runtime = new SetPassiveCombatRuntime(EquipmentSetEffectResolver.Resolve(package.EquipmentSets!),
            pieces, id => actors.GetValueOrDefault(id), abilities, _ => cooldowns,
            new Dictionary<Guid, Guid> { [pet.ActorId] = owner.ActorId });
        void Effect(CombatActorState actor, string id, EffectKind kind = EffectKind.Buff) =>
            EffectEngine.Apply(actor, owner.ActorId, new EffectDefinition(id, kind, TimeSpan.FromSeconds(10),
                1, EffectStackPolicy.Refresh, 1), Now);

        bool healing = branch == "PALADIN_HOLY";
        CombatEventType type = branch switch
        {
            "WARRIOR_GUARDIAN" or "WARRIOR_WARLORD" or "ARCHER_SURVIVAL" => CombatEventType.AbilityCompleted,
            "PALADIN_HOLY" => CombatEventType.HealingApplied,
            "PALADIN_PROTECTION" => CombatEventType.DamageBlocked,
            _ => CombatEventType.DamageDealt
        };
        if (branch == "MAGE_FIRE") Effect(target, "MAGE_PYROBLAST_BURN");
        if (branch == "ARCHER_SURVIVAL") Effect(target, "ARCHER_SERPENT_STING");
        if (branch == "PALADIN_PROTECTION") Effect(owner, "PALADIN_HOLY_SHIELD_BLOCK");
        int events = branch is "MAGE_FROST" or "ARCHER_MARKSMAN" or "ARCHER_BEAST_MASTERY" or "PALADIN_HOLY" or "PALADIN_PROTECTION" ? 3 : 1;
        for (int index = 0; index < events; index++)
        {
            Guid source = branch == "ARCHER_BEAST_MASTERY" ? pet.ActorId : owner.ActorId;
            runtime.Process(new CombatEvent(type, Now.AddSeconds(index), source, triggerAbility,
                10, source, branch == "PALADIN_PROTECTION" ? owner.ActorId : target.ActorId,
                IsCritical: branch is "WARRIOR_BERSERKER" or "ARCHER_BEAST_MASTERY",
                HealingOrigin: healing ? HealingOrigin.Direct : null));
        }
        decimal result = branch == "WARRIOR_WARLORD"
            ? owner.SetPassiveEffectMultiplier!(empoweredAbility, Now.AddSeconds(3))
            : owner.SetPassiveMultiplier!(empoweredAbility, abilities.GetValueOrDefault(empoweredAbility)?.IsSpell ?? false,
                target, Now.AddSeconds(3), healing);
        Assert.Equal(expected, result);
        if (healing)
            Assert.Equal(1, owner.SetPassiveMultiplier!("HOLY_SHOCK_OFFENSIVE", true, target, Now.AddSeconds(3), false));
    }

    [Fact]
    public async Task WarlordFourPieceMakesEveryCryCreateAMeaningfulFollowupWindow()
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var owner = CombatActorState.CreateDummy(1000);
        var target = CombatActorState.CreateDummy(1000);
        var actors = new[] { owner, target }.ToDictionary(a => a.ActorId);
        var pieces = new Dictionary<Guid, IReadOnlyDictionary<string, int>>
            { [owner.ActorId] = new Dictionary<string, int> { ["SET_L60_PVE_T1_WARRIOR_WARLORD"] = 4 } };
        var abilities = package.Abilities!.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var runtime = new SetPassiveCombatRuntime(EquipmentSetEffectResolver.Resolve(package.EquipmentSets!),
            pieces, id => actors.GetValueOrDefault(id), abilities, _ => new Dictionary<string, DateTimeOffset>());

        runtime.Process(new CombatEvent(CombatEventType.AbilityCompleted, Now, owner.ActorId, "BATTLE_CRY",
            SourceActorId: owner.ActorId, TargetActorId: owner.ActorId));

        Assert.Equal(1.20m, owner.SetPassiveMultiplier!("REVENGE", false, target, Now.AddSeconds(1), false));
        Assert.Equal(1m, owner.SetPassiveMultiplier!("REVENGE", false, target, Now.AddSeconds(1), false));
    }

    [Fact]
    public async Task RetributionFourPieceDoesNotRequireCrusaderStrikeToCrit()
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var owner = CombatActorState.CreateDummy(1000);
        Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal)
            { ["JUDGEMENT"] = Now.AddSeconds(8) };
        var pieces = new Dictionary<Guid, IReadOnlyDictionary<string, int>>
            { [owner.ActorId] = new Dictionary<string, int> { ["SET_L60_PVE_T1_PALADIN_RETRIBUTION"] = 4 } };
        var runtime = new SetPassiveCombatRuntime(EquipmentSetEffectResolver.Resolve(package.EquipmentSets!),
            pieces, _ => owner, package.Abilities!.ToDictionary(a => a.Id), _ => cooldowns);

        runtime.Process(new CombatEvent(CombatEventType.DamageDealt, Now, owner.ActorId, "CRUSADER_STRIKE",
            100, owner.ActorId, Guid.NewGuid(), IsCritical: false));

        Assert.Equal(Now.AddSeconds(7), cooldowns["JUDGEMENT"]);
    }

    [Fact]
    public async Task ArcaneSignatureReducesArcanePowerAndHasAnInternalCooldown()
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var owner = CombatActorState.CreateDummy(100);
        Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal) { ["MAGE_ARCANE_POWER"] = Now.AddSeconds(60) };
        var pieces = new Dictionary<Guid, IReadOnlyDictionary<string, int>>
            { [owner.ActorId] = new Dictionary<string, int> { ["SET_L60_PVE_T1_MAGE_ARCANE"] = 6 } };
        var runtime = new SetPassiveCombatRuntime(EquipmentSetEffectResolver.Resolve(package.EquipmentSets!),
            pieces, _ => owner, package.Abilities!.ToDictionary(a => a.Id), _ => cooldowns);
        CombatEvent Hit() => new(CombatEventType.DamageDealt, Now, owner.ActorId, "MAGE_ARCANE_SPARK", 10, owner.ActorId);
        runtime.Process(Hit());
        runtime.Process(Hit());
        Assert.Equal(Now.AddSeconds(59), cooldowns["MAGE_ARCANE_POWER"]);
    }

    [Fact]
    public async Task SetDensityReferencesAndLootSourcesMatchTheProgressionContract()
    {
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var items = package.Items!;
        Assert.DoesNotContain(items, i => i.RequiredLevel < 18 && i.SetId is not null);
        foreach (int level in new[] { 18, 35, 45, 55, 60 })
        {
            var members = items.Where(i => i.RequiredLevel == level && i.SetId is not null).ToArray();
            Assert.Equal(level == 60 ? 36 : 4, members.Select(i => i.SetId).Distinct().Count());
            Assert.All(members.GroupBy(i => i.SetId), g => Assert.Equal(level == 18 ? 4 : level == 60 ? 8 : 6, g.Count()));
        }
        Assert.Equal(52, package.EquipmentSets!.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(package.EquipmentSets!.Where(s => s.Id.StartsWith("SET_L60_", StringComparison.Ordinal)), s =>
            Assert.Equal(6, s.Bonuses.Max(b => b.RequiredPieces)));
        Assert.All(package.EquipmentSets!.Where(s => s.Id.StartsWith("SET_L60_PVE_T1_", StringComparison.Ordinal)), s =>
        {
            Assert.Equal(2, s.SpecialEffects!.Count);
            Assert.All(s.Bonuses.Where(b => b.RequiredPieces >= 4), b => Assert.NotEmpty(b.SpecialEffectIds!));
        });
        var reachable = package.Monsters!.Where(m => m.LootTableId is not null).Select(m => m.LootTableId).ToHashSet();
        var lootItems = package.LootTables!.Where(t => reachable.Contains(t.Id))
            .SelectMany(t => t.Entries.Select(e => e.ItemId).Concat((t.SelectionGroups ?? []).SelectMany(g => g.Entries).Select(e => e.ItemId)))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(items.Where(i => i.Id.StartsWith("L35_", StringComparison.Ordinal)
            || i.Id.StartsWith("L45_", StringComparison.Ordinal) || i.Id.StartsWith("L55_", StringComparison.Ordinal)), i => Assert.Contains(i.Id, lootItems));
    }
}
