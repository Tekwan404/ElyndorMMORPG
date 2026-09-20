namespace Elyndor.Core.Items;

public sealed record ItemEnhancementSalvageRefund(
    string? EnhancementMaterialItemId,
    int EnhancementMaterialQuantity,
    string? CatalystItemId,
    int CatalystQuantity)
{
    public static ItemEnhancementSalvageRefund None { get; } = new(null, 0, null, 0);
}

public static class ItemEnhancementSalvageRefundRules
{
    public static ItemEnhancementSalvageRefund Resolve(
        ItemStarUpgradeProfileDefinition profile,
        int enhancementLevel)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (enhancementLevel is < ItemEnhancementRules.MinimumLevel or > ItemEnhancementRules.MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(enhancementLevel));
        if (enhancementLevel == ItemEnhancementRules.MinimumLevel)
            return ItemEnhancementSalvageRefund.None;

        ItemEnhancementInvestment investment = ItemEnhancementCostRules.ResolveInvestment(profile, enhancementLevel);
        int refundPercent = enhancementLevel switch
        {
            1 => 30,
            2 => 35,
            3 => 40,
            4 => 45,
            5 => 50,
            _ => throw new ArgumentOutOfRangeException(nameof(enhancementLevel))
        };

        int materialQuantity = RefundQuantity(
            investment.EnhancementMaterialQuantity,
            refundPercent,
            guaranteeOne: true);
        int catalystQuantity = RefundQuantity(
            investment.CatalystQuantity,
            refundPercent,
            guaranteeOne: false);

        return new ItemEnhancementSalvageRefund(
            materialQuantity > 0 ? investment.EnhancementMaterialItemId : null,
            materialQuantity,
            catalystQuantity > 0 ? investment.CatalystItemId : null,
            catalystQuantity);
    }

    private static int RefundQuantity(int investedQuantity, int refundPercent, bool guaranteeOne)
    {
        if (investedQuantity <= 0)
            return 0;

        int refund = checked(investedQuantity * refundPercent) / 100;
        return guaranteeOne ? Math.Max(1, refund) : refund;
    }
}
