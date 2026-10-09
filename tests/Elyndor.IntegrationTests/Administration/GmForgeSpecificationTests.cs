using Elyndor.Infrastructure.Administration;
using Elyndor.Core.Items;

namespace Elyndor.IntegrationTests.Administration;

public sealed class GmForgeSpecificationTests
{
    [Fact]
    public void ParsesPerfectFiveStarSwordWithExperimentalDamage()
    {
        bool ok = GmForgeSpecification.TryParse(
            "UNIQUE_WARRIOR_BLACKHEART quality=PERFECT stars=5 enhance=5 WEAPON_DAMAGE=1500 CRITICAL_DAMAGE=150",
            out GmForgeSpecification? spec);

        Assert.True(ok);
        Assert.NotNull(spec);
        Assert.True(spec.Perfect);
        Assert.Equal("BOSS", spec.QualityProfileId);
        Assert.Equal(5, spec.ForcedStars);
        Assert.Equal(5, spec.EnhancementLevel);
        Assert.Equal(1500m, spec.StatOverrides[ItemStatIds.WeaponDamage]);
        Assert.Equal(150m, spec.StatOverrides[ItemStatIds.CriticalDamage]);
    }

    [Theory]
    [InlineData("SWORD quality=PERFECT stars=4")]
    [InlineData("SWORD enhance=6")]
    [InlineData("SWORD stars=0")]
    [InlineData("SWORD UNKNOWN_STAT=100")]
    [InlineData("SWORD CRITICAL_DAMAGE=-1")]
    [InlineData("SWORD WEAPON_DAMAGE=1000001")]
    [InlineData("SWORD WEAPON_DAMAGE=1 WEAPON_DAMAGE=2")]
    [InlineData("SWORD quality=BOSS quality=PERFECT")]
    [InlineData("clone:bad-guid CRITICAL_DAMAGE=150")]
    public void RejectsInvalidOrDuplicateModifiers(string text)
    {
        Assert.False(GmForgeSpecification.TryParse(text, out _));
    }

    [Fact]
    public void CloneUsesExistingInstanceId()
    {
        Guid source = Guid.CreateVersion7();
        Assert.True(GmForgeSpecification.TryParse(
            $"clone:{source:D} stars=5", out GmForgeSpecification? spec));
        Assert.Equal(source, spec!.CloneItemId);
        Assert.Equal(string.Empty, spec.ItemDefinitionId);
    }
}
