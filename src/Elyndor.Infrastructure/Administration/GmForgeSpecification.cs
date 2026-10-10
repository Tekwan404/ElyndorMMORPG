using System.Globalization;
using Elyndor.Core.Items;

namespace Elyndor.Infrastructure.Administration;

/// <summary>
/// Admin-only input. No part of this parser is exposed through player item APIs.
/// </summary>
public sealed record GmForgeSpecification(
    string ItemDefinitionId,
    Guid? CloneItemId,
    string QualityProfileId,
    bool Perfect,
    int? ForcedStars,
    int? EnhancementLevel,
    IReadOnlyDictionary<string, decimal> StatOverrides,
    int Quantity = 1)
{
    private static readonly HashSet<string> QualityProfiles =
        new(["NORMAL", "ELITE", "BOSS", "PERFECT"], StringComparer.Ordinal);

    public static bool TryParse(string? raw, out GmForgeSpecification? specification)
    {
        specification = null;
        string[] tokens = raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (tokens.Length is < 1 or > 30) return false;

        string itemId = tokens[0].ToUpperInvariant();
        Guid? cloneId = null;
        if (itemId.StartsWith("CLONE:", StringComparison.Ordinal))
        {
            if (!Guid.TryParse(itemId["CLONE:".Length..], out Guid parsedClone) || parsedClone == Guid.Empty)
                return false;
            cloneId = parsedClone;
            itemId = string.Empty;
        }
        else if (itemId.Length is < 1 or > 64)
        {
            return false;
        }

        string quality = "NORMAL";
        bool perfect = false;
        int? stars = null;
        int? enhancement = null;
        int quantity = 1;
        bool qualitySeen = false, starsSeen = false, enhancementSeen = false, quantitySeen = false;
        Dictionary<string, decimal> stats = new(StringComparer.Ordinal);
        foreach (string token in tokens.Skip(1))
        {
            if (string.Equals(token, "PERFECT", StringComparison.OrdinalIgnoreCase))
            {
                if (qualitySeen) return false;
                qualitySeen = true;
                quality = "PERFECT";
                perfect = true;
                continue;
            }

            int separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1) return false;
            string key = token[..separator].ToUpperInvariant();
            string value = token[(separator + 1)..];
            switch (key)
            {
                case "QUALITY":
                    if (qualitySeen || !QualityProfiles.Contains(value.ToUpperInvariant())) return false;
                    qualitySeen = true;
                    quality = value.ToUpperInvariant();
                    perfect = quality == "PERFECT";
                    break;
                case "STARS":
                    if (starsSeen || !int.TryParse(value, out int parsedStars) || parsedStars is < 1 or > 5)
                        return false;
                    starsSeen = true;
                    stars = parsedStars;
                    break;
                case "QTY":
                    if (quantitySeen || !int.TryParse(value, out int parsedQuantity)
                        || parsedQuantity is < 1 or > 20)
                        return false;
                    quantitySeen = true;
                    quantity = parsedQuantity;
                    break;
                case "ENHANCE":
                    if (enhancementSeen || !int.TryParse(value, out int parsedEnhancement)
                        || parsedEnhancement is < 0 or > 5)
                        return false;
                    enhancementSeen = true;
                    enhancement = parsedEnhancement;
                    break;
                default:
                    if (!ItemStatIds.ApprovedV1.Contains(key)
                        || !decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture, out decimal amount)
                        || amount < 0m || amount > 1_000_000m || !stats.TryAdd(key, amount))
                        return false;
                    break;
            }
        }

        if (perfect && stars.HasValue && stars.Value != 5)
            return false;
        specification = new GmForgeSpecification(itemId, cloneId, perfect ? "BOSS" : quality,
            perfect, stars, enhancement, stats, quantity);
        return true;
    }
}
