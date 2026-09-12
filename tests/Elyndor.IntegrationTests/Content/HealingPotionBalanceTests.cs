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
        Assert.Equal(50m, action.Amount);
    }
}
