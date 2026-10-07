namespace Elyndor.Core.Items;

public static class WeaponAttackIntervalPolicy
{
    public static decimal ResolveSeconds(
        string? weaponCategory,
        decimal? authoredIntervalSeconds,
        decimal fallbackIntervalSeconds)
    {
        if (authoredIntervalSeconds is { } authored)
        {
            if (authored <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(authoredIntervalSeconds),
                    "Weapon attack interval must be positive.");

            return authored;
        }

        if (fallbackIntervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(fallbackIntervalSeconds),
                "Fallback attack interval must be positive.");

        return weaponCategory switch
        {
            EquipmentCategoryIds.Dagger => 1.6m,
            EquipmentCategoryIds.Wand => 1.7m,
            EquipmentCategoryIds.OneHandSword => 1.8m,
            EquipmentCategoryIds.OneHandStaff => 1.9m,
            EquipmentCategoryIds.Axe => 2.0m,
            EquipmentCategoryIds.Mace => 2.1m,
            EquipmentCategoryIds.Bow => 2.6m,
            EquipmentCategoryIds.Polearm => 2.7m,
            EquipmentCategoryIds.Staff => 2.8m,
            EquipmentCategoryIds.TwoHandSword => 2.9m,
            EquipmentCategoryIds.Crossbow => 3.0m,
            EquipmentCategoryIds.TwoHandAxe => 3.0m,
            EquipmentCategoryIds.TwoHandMace => 3.1m,
            _ => fallbackIntervalSeconds
        };
    }
}
