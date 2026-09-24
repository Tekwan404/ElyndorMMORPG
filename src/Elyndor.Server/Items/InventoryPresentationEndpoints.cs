using Elyndor.Contracts.Items;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.Server.Items;

public static class InventoryPresentationEndpoints
{
    public static IEndpointRouteBuilder MapInventoryPresentationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/inventory/presentation", GetPresentation)
            .RequireAuthorization()
            .WithTags("Inventory");
        return endpoints;
    }

    internal static InventoryPresentationResponse GetPresentation(IContentSnapshotProvider contentProvider)
    {
        GameContentPackage content = contentProvider.GetCurrent().Package;

        EquipmentSetPresentationResponse[] sets = (content.EquipmentSets ?? [])
            .OrderBy(set => set.Id, StringComparer.Ordinal)
            .Select(set => new EquipmentSetPresentationResponse(
                set.Id,
                set.Name,
                set.Bonuses
                    .OrderBy(bonus => bonus.RequiredPieces)
                    .Select(ToResponse)
                    .ToArray()))
            .ToArray();

        ClassEquipmentRulesResponse[] classRules = (content.ClassProfiles ?? [])
            .OrderBy(profile => profile.Id, StringComparer.Ordinal)
            .Select(profile => new ClassEquipmentRulesResponse(
                profile.Id,
                profile.AllowedWeaponCategories.ToArray(),
                profile.AllowedArmorCategories.ToArray(),
                (profile.AllowedOffHandCategories ?? []).ToArray()))
            .ToArray();

        return new InventoryPresentationResponse(sets, classRules);
    }

    private static EquipmentSetBonusPresentationResponse ToResponse(
        EquipmentSetBonusDefinition bonus) =>
        new(
            bonus.RequiredPieces,
            bonus.AttackSpeedPercent,
            bonus.DodgePercent,
            bonus.MaxHpFlat,
            bonus.AttackPowerFlat,
            bonus.SpellPowerFlat,
            bonus.CriticalChancePercent,
            bonus.CriticalDamagePercent,
            bonus.AccuracyPercent,
            bonus.ArmorFlat,
            bonus.MagicResistanceFlat,
            bonus.ArmorPenetrationPercent,
            bonus.MagicPenetrationPercent,
            bonus.MaxResourceFlat);
}