using Elyndor.Core.Items;
using Elyndor.Core.World;

namespace Elyndor.Core.Content;

internal static class PlayerFacingTextValidator
{
    private static readonly string[] ForbiddenFragments =
    [
        "Материал Elyndor",
        "таблицах добычи",
        "таблицы добычи",
        "процедурно генерируем",
        "тестовой версии",
        "испытательной версии",
        "релизного среза",
        "контент 1–40",
        "тестовый предмет",
        "для совместимости",
        "Визуальная тема:"
    ];

    private static readonly string[] ClassIds =
        ["WARRIOR", "MAGE", "ARCHER", "PALADIN"];

    private static readonly string[] ClassBoundEquipmentPhrases =
        ["экипировка для", "броня для", "доспех для", "предмет для"];

    public static void Validate(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        ValidateItems(package.Items ?? [], errors);
        ValidateLocations(package.Locations, errors);
    }

    private static void ValidateItems(
        IReadOnlyList<ItemDefinition> items,
        List<ContentValidationError> errors)
    {
        for (var index = 0; index < items.Count; index++)
        {
            ItemDefinition item = items[index];
            string path = $"items[{index}].description";
            ValidateForbiddenFragments(item.Description, path, $"Item '{item.Id}'", errors);

            if (item.Type != ItemType.Equipment || string.IsNullOrWhiteSpace(item.Description))
                continue;

            foreach (string phrase in ClassBoundEquipmentPhrases)
            {
                foreach (string classId in ClassIds)
                {
                    string forbidden = $"{phrase} {classId}";
                    if (!item.Description.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                        continue;

                    errors.Add(new ContentValidationError(
                        "PLAYER_FACING_CLASS_BOUND_EQUIPMENT_TEXT",
                        path,
                        $"Item '{item.Id}' contains class-bound ordinary equipment text '{forbidden}'. "
                        + "Use ArmorCategory/WeaponCategory and class equipment rules instead of lore text."));
                }
            }
        }
    }

    private static void ValidateLocations(
        IReadOnlyList<LocationDefinition> locations,
        List<ContentValidationError> errors)
    {
        for (var index = 0; index < locations.Count; index++)
        {
            LocationDefinition location = locations[index];
            ValidateForbiddenFragments(
                location.Description,
                $"locations[{index}].description",
                $"Location '{location.Id}'",
                errors);
        }
    }

    private static void ValidateForbiddenFragments(
        string? value,
        string path,
        string subject,
        List<ContentValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (string fragment in ForbiddenFragments)
        {
            if (!value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                continue;

            errors.Add(new ContentValidationError(
                "PLAYER_FACING_TECHNICAL_TEXT",
                path,
                $"{subject} contains technical or development-facing text '{fragment}'."));
        }
    }
}