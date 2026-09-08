namespace Elyndor.Core.Identity;

public static class TelegramUsernamePolicy
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = value.Trim();
        if (normalized.StartsWith('@'))
            normalized = normalized[1..];

        if (normalized.Length is < 5 or > 32
            || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("Telegram username has an invalid format.", nameof(value));
        }

        return normalized.ToLowerInvariant();
    }
}
