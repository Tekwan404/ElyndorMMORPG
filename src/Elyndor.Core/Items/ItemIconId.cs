using System.Text.RegularExpressions;

namespace Elyndor.Core.Items;

public static partial class ItemIconId
{
    public static bool IsCanonical(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && CanonicalPattern().IsMatch(value);

    [GeneratedRegex(
        "^(?:[a-z0-9](?:[a-z0-9_-]*[a-z0-9])?/)*[a-z0-9](?:[a-z0-9_-]*[a-z0-9])?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalPattern();
}
