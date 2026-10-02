using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class HealingPotionBalanceTests
{
    [Fact]
    public async Task SmallHealingPotionUsesFiveSecondSharedCooldown()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition potion = Assert.Single(
            package.Items!,
            item => item.Id == "SMALL_HEALING_POTION");

        Assert.Equal(5m, potion.ConsumableCooldownSeconds);
        Assert.Equal("HEALING_POTION", potion.ConsumableCooldownCategoryId);
        ConsumableActionDefinition action = Assert.Single(potion.ConsumableActions!);
        Assert.Equal(ConsumableActionType.RestoreHp, action.Type);
        Assert.Equal(120m, action.Amount);
        [Theory]
    [InlineData("MINOR_HEALING_POTION", 150)]
    [InlineData("HEALING_POTION", 200)]
    [InlineData("MAJOR_HEALING_POTION", 250)]
    [InlineData("SUPERIOR_HEALING_POTION", 500)]
    public async Task HealingPotionTiersRestoreConfiguredHealth(
        string itemId,
        decimal expectedHealing)
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition potion = Assert.Single(
            package.Items!,
            item => item.Id == itemId);

        Assert.Equal(5m, potion.ConsumableCooldownSeconds);
        Assert.Equal("HEALING_POTION", potion.ConsumableCooldownCategoryId);
        ConsumableActionDefinition action = Assert.Single(potion.ConsumableActions!);
        Assert.Equal(ConsumableActionType.RestoreHp, action.Type);
        Assert.Equal(expectedHealing, action.Amount);
    }

    [Fact]
    public async Task MarcusStocksEveryHealingPotionTier()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        MerchantDefinition merchant = Assert.Single(
            package.Merchants!,
            candidate => candidate.Id == "MARCUS_SUPPLIES");

        string[] expected =
        [
            "SMALL_HEALING_POTION",
            "MINOR_HEALING_POTION",
            "HEALING_POTION",
            "MAJOR_HEALING_POTION",
            "SUPERIOR_HEALING_POTION"
        ];

        Assert.All(expected, itemId => Assert.Contains(itemId, merchant.ItemIds));
    }

}
}
