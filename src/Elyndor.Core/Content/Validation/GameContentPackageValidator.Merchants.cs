using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateMerchantDefinitions(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        IReadOnlyList<MerchantDefinition> merchants = package.Merchants ?? [];
        if (merchants.Count == 0) return;

        HashSet<string> itemIds = (package.Items ?? [])
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ItemDefinition> itemsById = (package.Items ?? [])
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> locationIds = package.Locations
            .Select(location => location.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> merchantIds = new(StringComparer.Ordinal);

        for (var index = 0; index < merchants.Count; index++)
        {
            MerchantDefinition merchant = merchants[index];
            string path = $"merchants[{index}]";

            bool idIsValid = ValidateIdentifier(
                merchant.Id,
                "INVALID_MERCHANT_ID",
                $"{path}.id",
                errors);
            if (idIsValid && !merchantIds.Add(merchant.Id))
            {
                errors.Add(new(
                    "DUPLICATE_MERCHANT_ID",
                    path,
                    $"Merchant '{merchant.Id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(merchant.Name)
                || string.IsNullOrWhiteSpace(merchant.Description))
            {
                errors.Add(new(
                    "INVALID_MERCHANT_DEFINITION",
                    path,
                    $"Merchant '{merchant.Id}' must define a name and description."));
            }

            if (!locationIds.Contains(merchant.LocationId))
            {
                errors.Add(new(
                    "MISSING_MERCHANT_LOCATION",
                    $"{path}.locationId",
                    $"Merchant '{merchant.Id}' references missing location '{merchant.LocationId}'."));
            }

            HashSet<string> listedItemIds = new(StringComparer.Ordinal);
            for (var itemIndex = 0; itemIndex < merchant.ItemIds.Count; itemIndex++)
            {
                string itemId = merchant.ItemIds[itemIndex];
                string itemPath = $"{path}.itemIds[{itemIndex}]";
                if (!listedItemIds.Add(itemId))
                {
                    errors.Add(new(
                        "DUPLICATE_MERCHANT_ITEM",
                        itemPath,
                        $"Merchant '{merchant.Id}' lists item '{itemId}' more than once."));
                    continue;
                }

                if (!itemIds.Contains(itemId))
                {
                    errors.Add(new(
                        "MISSING_MERCHANT_ITEM_REFERENCE",
                        itemPath,
                        $"Merchant '{merchant.Id}' references missing item '{itemId}'."));
                    continue;
                }

                if (itemsById[itemId].BuyPriceGold <= 0)
                {
                    errors.Add(new(
                        "INVALID_MERCHANT_BUY_PRICE",
                        itemPath,
                        $"Merchant item '{itemId}' must have a positive buy price."));
                }
            }
        }
    }
}
