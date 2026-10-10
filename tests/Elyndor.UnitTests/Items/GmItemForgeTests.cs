using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class GmItemForgeTests
{
    private static GeneratedItemInstance Sword() => new(
        60,
        [
            new GeneratedItemAffix("AFFIX_1", "STRENGTH", "STRENGTH",
                30m, 10m, 40m, 1m, 4, true, false, 0)
        ],
        100m, 150m, 200m, 50m, 3, false, null, null, null, "Sword", 2);

    [Fact]
    public void PerfectMakesEveryExistingAffixMaximumWithoutModifyingOriginal()
    {
        GeneratedItemInstance original = Sword();

        GeneratedItemInstance forged = GmItemForge.Apply(original, perfect: true,
            forcedStars: null, statOverrides: new Dictionary<string, decimal>());

        Assert.Equal(40m, forged.Affixes[0].Value);
        Assert.Equal(30m, original.Affixes[0].Value);
        Assert.Equal(5, forged.Stars);
        Assert.Equal(100m, forged.RollQuality);
        Assert.True(forged.IsPerfect);
        Assert.Equal(GmItemForge.SourceType, forged.PerfectOrigin);
    }

    [Fact]
    public void ExperimentalStatsCanExceedOrdinaryRangesAndAddNewAffixes()
    {
        GeneratedItemInstance forged = GmItemForge.Apply(Sword(), false, 5,
            new Dictionary<string, decimal>
            {
                [ItemStatIds.Strength] = 500m,
                [ItemStatIds.WeaponDamage] = 1500m,
                [ItemStatIds.CriticalDamage] = 150m
            });

        Assert.Equal(3, forged.Affixes.Count);
        Assert.Equal(500m, forged.Affixes.Single(x => x.StatId == ItemStatIds.Strength).Value);
        Assert.Equal(1500m, forged.Affixes.Single(x => x.StatId == ItemStatIds.WeaponDamage).Value);
        Assert.Equal(150m, forged.Affixes.Single(x => x.StatId == ItemStatIds.CriticalDamage).Value);
        Assert.Equal(5, forged.Stars);
        Assert.False(forged.IsPerfect);
        Assert.Equal(30m, Sword().Affixes[0].Value);
    }

    [Fact]
    public void ForgedItemIsBoundLockedAndCannotBeUnlockedByPlayerMutation()
    {
        CharacterItem item = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "SWORD", 1, DateTimeOffset.UtcNow);
        ItemGenerationKey key = ItemGenerationKey.Create(Guid.CreateVersion7());
        item.ApplyGeneratedInstance(Sword(), key.AuditHash, GmItemForge.SourceType,
            Guid.CreateVersion7(), "admin");
        item.MarkDeveloperOnly();

        Assert.Equal(ItemBindStates.Bound, item.BindState);
        Assert.True(item.IsLocked);
        item.SetLocked(false);
        Assert.True(item.IsLocked);
    }

    [Fact]
    public void NormalItemsCannotAcquireGmBindingByAccident()
    {
        CharacterItem item = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "SWORD", 1, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => item.MarkDeveloperOnly());
    }
}
