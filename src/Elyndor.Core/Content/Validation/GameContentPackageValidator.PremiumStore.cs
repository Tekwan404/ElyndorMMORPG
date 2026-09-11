using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidatePremiumStore(GameContentPackage package, List<ContentValidationError> errors)
    {
        IReadOnlyList<PremiumStoreOfferDefinition> offers = package.PremiumStoreOffers ?? [];
        HashSet<string> skus = new(StringComparer.Ordinal);
        IReadOnlyDictionary<string, ItemDefinition> items = GameContentIndexes.For(package).ItemsById;
        for (int index = 0; index < offers.Count; index++)
        {
            PremiumStoreOfferDefinition offer = offers[index];
            string path = $"premiumStoreOffers[{index}]";
            if (!IsCanonicalIdentifier(offer.Sku) || !skus.Add(offer.Sku)
                || offer.Quantity <= 0 || offer.CrystalPrice <= 0 || offer.PerAccountLimit is <= 0)
                errors.Add(new("INVALID_PREMIUM_STORE_OFFER", path, "Offer has invalid SKU, quantity, price, or limit."));
            if (!items.TryGetValue(offer.ItemDefinitionId, out ItemDefinition? item) || !item.PremiumEligible)
                errors.Add(new("FORBIDDEN_PREMIUM_STORE_ITEM", path, "Offer item is missing or prohibited from premium sale."));
        }
    }
}
