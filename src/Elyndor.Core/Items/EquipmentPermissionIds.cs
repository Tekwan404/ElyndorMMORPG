namespace Elyndor.Core.Items;

public static class EquipmentPermissionIds
{
    public const string DualWieldOneHandWeapon = "DUAL_WIELD_ONE_HAND_WEAPON";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DualWieldOneHandWeapon
    };
}
