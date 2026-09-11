using Elyndor.Core.Economy;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidatePromoCodes(GameContentPackage package, List<ContentValidationError> errors)
    {
        Dictionary<string, ItemDefinition> items = (package.Items ?? []).ToDictionary(item => item.Id, StringComparer.Ordinal);
        HashSet<string> codes = new(StringComparer.Ordinal);
        PromoCodeDefinition[] promos = (package.PromoCodes ?? []).ToArray();
        for (int index = 0; index < promos.Length; index++)
        {
            PromoCodeDefinition promo = promos[index];
            string path = $"promoCodes[{index}]";
            bool invalid = !IsCanonicalIdentifier(promo.Code) || !codes.Add(promo.Code)
                || promo.CrystalAmount < 0 || promo.GlobalRedemptionLimit is <= 0 || promo.PerAccountRedemptionLimit is <= 0
                || promo.StartsAtUtc?.Offset != TimeSpan.Zero || promo.ExpiresAtUtc?.Offset != TimeSpan.Zero
                || promo.ExpiresAtUtc <= promo.StartsAtUtc
                || (promo.CrystalAmount == 0 && (promo.ItemRewards?.Count ?? 0) == 0);
            if (invalid) errors.Add(new("INVALID_PROMO_CODE", path, "Promo code fields or reward definition are invalid."));
            foreach (PromoItemRewardDefinition reward in promo.ItemRewards ?? [])
            {
                if (reward.Quantity <= 0 || !items.TryGetValue(reward.ItemDefinitionId, out ItemDefinition? item)
                    || item.Type == ItemType.Equipment)
                    errors.Add(new("INVALID_PROMO_REWARD", path, "Promo reward must reference a non-equipment item with a positive quantity."));
            }
        }
    }
}
