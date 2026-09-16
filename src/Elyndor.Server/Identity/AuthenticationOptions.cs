using System.Globalization;
using System.Text;

namespace Elyndor.Server.Identity;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public const int AccessTokenLifetimeMinutes = 15;
    public const int TokenValidationClockSkewSeconds = 30;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public TelegramAuthenticationOptions Telegram { get; init; } = new();

    public DevelopmentAuthenticationOptions Development { get; init; } = new();

    public bool IsValid()
    {
        int signingKeyByteCount = Encoding.UTF8.GetByteCount(SigningKey);

        return !string.IsNullOrWhiteSpace(Issuer)
            && !string.IsNullOrWhiteSpace(Audience)
            && signingKeyByteCount >= 32
            && !string.IsNullOrWhiteSpace(Telegram.BotToken)
            && Telegram.InitDataMaxAgeSeconds > 0
            && Telegram.MaxFutureSkewSeconds >= 0
            && Telegram.Web.IsValid()
            && (!Development.Enabled || Development.TelegramUserId > 0);
    }
}

public sealed class TelegramAuthenticationOptions
{
    public string BotToken { get; init; } = string.Empty;

    public int InitDataMaxAgeSeconds { get; init; } = 43200;

    public int MaxFutureSkewSeconds { get; init; } = 30;

    public TelegramWebAuthenticationOptions Web { get; init; } = new();
}

public sealed class TelegramWebAuthenticationOptions
{
    public bool Enabled { get; init; }

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;

    public int SessionLifetimeHours { get; init; } = 12;

    public bool IsValid()
    {
        if (!Enabled)
            return true;

        return long.TryParse(
                ClientId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long clientId)
            && clientId > 0
            && !string.IsNullOrWhiteSpace(ClientSecret)
            && IsAllowedRedirectUri(RedirectUri)
            && SessionLifetimeHours is >= 1 and <= 24;
    }

    private static bool IsAllowedRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }
}

public sealed class DevelopmentAuthenticationOptions
{
    public bool Enabled { get; init; }

    public long TelegramUserId { get; init; }
}
