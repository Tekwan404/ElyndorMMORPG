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
            && (!Development.Enabled || Development.TelegramUserId > 0);
    }
}

public sealed class TelegramAuthenticationOptions
{
    public string BotToken { get; init; } = string.Empty;

    public int InitDataMaxAgeSeconds { get; init; } = 300;

    public int MaxFutureSkewSeconds { get; init; } = 30;
}

public sealed class DevelopmentAuthenticationOptions
{
    public bool Enabled { get; init; }

    public long TelegramUserId { get; init; }
}
