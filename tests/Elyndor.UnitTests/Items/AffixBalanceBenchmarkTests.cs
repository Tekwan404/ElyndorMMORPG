using Elyndor.ContentValidator;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;

namespace Elyndor.UnitTests.Items;

public sealed class AffixBalanceBenchmarkTests
{
    [Fact]
    public async Task NearCapScenariosReachIntendedFinalStatsAndShieldAffixesHaveValue()
    {
        var rows = AffixBalanceBenchmark.Run(await AffixV2CatalogTests.LoadAsync());
        Assert.All(rows.Where(row => row.Scenario == "near-caps" && row.StatId == "BASELINE"), row =>
        {
            Assert.Equal(58, row.Stats.CriticalChance);
            Assert.Equal(33, row.Stats.Dodge);
            Assert.Equal(99, row.Stats.Accuracy);
        });
        foreach (var baseline in rows.Where(row => row.Scenario == "ordinary" && row.StatId == "BASELINE" && row.ClassId is "WARRIOR" or "PALADIN"))
        {
            foreach (string stat in new[] { "BLOCK_CHANCE", "BLOCK_VALUE" })
                Assert.True(rows.Single(row => row.Level == baseline.Level && row.ClassId == baseline.ClassId
                    && row.Scenario == baseline.Scenario && row.StatId == stat).Metrics.PhysicalEffectiveHp > baseline.Metrics.PhysicalEffectiveHp);
        }
    }
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(60)]
    public void ProductionDamageHealingAndDefensePathsRespectCaps(int level)
    {
        var profile = new ClassProfile("MAGE", "INTELLECT", "MANA", new(0, 0, 0, 0), new(0, 0, 0, 0), [], [], "Benchmark");
        CharacterStats stats = new(0, 0, 0, 0, 1000, 100, 100, 60, 150, 100, 0, 0, 1.5m, 100000, 100000, 35);
        AffixBalanceMetrics baseline = AffixBalanceBenchmark.Measure(level, stats, profile, 20);
        AffixBalanceMetrics moreSpellPower = AffixBalanceBenchmark.Measure(level, stats with { SpellPower = 150 }, profile, 20);
        AffixBalanceMetrics moreArmor = AffixBalanceBenchmark.Measure(level, stats with { Armor = 200000, MagicResistance = 200000 }, profile, 20);
        Assert.True(moreSpellPower.DirectHps > baseline.DirectHps);
        Assert.True(moreSpellPower.DirectSpellDps > baseline.DirectSpellDps);
        Assert.Equal(baseline.PhysicalEffectiveHp, moreArmor.PhysicalEffectiveHp);
        Assert.Equal(baseline.MagicalEffectiveHp, moreArmor.MagicalEffectiveHp);
        Assert.Equal(baseline.DirectHps, AffixBalanceBenchmark.Measure(level, stats with { AttackSpeed = 2 }, profile, 20).DirectHps);
    }
}
