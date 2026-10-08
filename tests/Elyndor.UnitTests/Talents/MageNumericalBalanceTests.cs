using Elyndor.Core.Combat.Abilities;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Talents;

/// <summary>Guards authored coefficients; full rotation DPS still requires combat simulations.</summary>
public sealed class MageNumericalBalanceTests
{
    private static async Task<IReadOnlyDictionary<string, AbilityDefinition>> Load()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json")))
            root = root.Parent;
        var content = await GameContentPackageLoader.LoadAsync(
            Path.Combine(root!.FullName, "content", "package.json"));
        Assert.Equal("0.46.0", content.ContentVersion);
        Assert.Equal("0.36.0", content.BalanceVersion);
        return content.Abilities!.Where(a => a.Id.StartsWith("MAGE_", StringComparison.Ordinal))
            .ToDictionary(a => a.Id, StringComparer.Ordinal);
    }

    private static decimal Raw(AbilityDefinition ability, decimal spellPower = 400m)
    {
        AbilityActionDefinition a = Assert.Single(ability.Actions!, a => a.Type == AbilityActionType.Damage);
        return a.Amount + 60m * a.DamagePerCharacterLevel + spellPower * a.SpellPowerCoefficient;
    }

    [Fact]
    public async Task FireAoeWindowIncreasesWithoutChangingFireballMana()
    {
        var a = await Load();
        Assert.Equal(TimeSpan.FromSeconds(3), a["MAGE_FLAMESTRIKE"].Cooldown);
        Assert.Equal(1.15m, a["MAGE_FLAMESTRIKE"].Actions![0].SpellPowerCoefficient);
        Assert.Equal(107.76m, a["MAGE_FIREBALL"].ResourceCostByLevel!.Single(p => p.Level == 60).Cost);
        Assert.True(Raw(a["MAGE_FLAMESTRIKE"]) > Raw(a["MAGE_BLIZZARD"]));
    }

    [Fact]
    public async Task ArcaneFourTickChannelRewardsCommitment()
    {
        var a = await Load();
        var missiles = a["MAGE_ARCANE_MISSILES"];
        Assert.Equal(.82m, a["MAGE_ARCANE_SPARK"].Actions![0].SpellPowerCoefficient);
        Assert.Equal(.72m, missiles.Actions![0].SpellPowerCoefficient);
        Assert.Equal(TimeSpan.FromSeconds(4), missiles.CastTime);
        Assert.Equal(TimeSpan.FromSeconds(1), missiles.ChannelTickInterval);
        Assert.Equal(258.244m, missiles.ResourceCostByLevel!.Single(p => p.Level == 60).Cost);
        Assert.True(4m * Raw(missiles) > 2m * Raw(a["MAGE_ARCANE_SPARK"]));
        Assert.Equal(TimeSpan.FromSeconds(4), a["MAGE_ARCANE_EXPLOSION"].Cooldown);
        Assert.Equal(1.05m, a["MAGE_ARCANE_EXPLOSION"].Actions![0].SpellPowerCoefficient);
    }

    [Fact]
    public async Task FrostChannelHasBoundedRawAoeRatioToFire()
    {
        var a = await Load();
        var blizzard = a["MAGE_BLIZZARD"];
        Assert.Equal(22m, blizzard.Actions![0].Amount);
        Assert.Equal(.48m, blizzard.Actions![0].SpellPowerCoefficient);
        Assert.Equal(TimeSpan.FromSeconds(4), blizzard.CastTime);
        Assert.Equal(TimeSpan.FromSeconds(1), blizzard.ChannelTickInterval);
        Assert.Equal(127.47m, blizzard.ResourceCostByLevel!.Single(p => p.Level == 60).Cost);
        // Illustrative direct coefficients, not full rotations with procs or armor.
        decimal frost = Raw(blizzard);
        decimal fire = (Raw(a["MAGE_FLAMESTRIKE"]) + 3m * 400m * .04m) / 3m;
        Assert.InRange(frost / fire, 1.10m, 1.50m);
    }
}
