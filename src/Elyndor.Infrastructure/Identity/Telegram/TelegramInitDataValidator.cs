using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Elyndor.Infrastructure.Identity.Telegram;

public sealed class TelegramInitDataValidator(TimeProvider timeProvider)
{
    private const string WebAppDataKey = "WebAppData";

    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public TelegramInitDataValidationResult Validate(
        string initData,
        string botToken,
        TimeSpan maxAge,
        TimeSpan maxFutureSkew)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botToken);

        if (maxAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge), "Maximum age must be positive.");
        }

        if (maxFutureSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFutureSkew),
                "Maximum future clock skew cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(initData))
        {
            return Failure(TelegramInitDataValidationErrorCodes.Missing);
        }

        TelegramInitDataValidationResult? parseFailure = TryParseFields(initData, out Dictionary<string, string> fields);
        if (parseFailure is not null)
        {
            return parseFailure;
        }

        if (!fields.Remove("hash", out string? hashText))
        {
            return Failure(TelegramInitDataValidationErrorCodes.HashMissing);
        }

        if (!TryDecodeHash(hashText, out byte[] suppliedHash))
        {
            return Failure(TelegramInitDataValidationErrorCodes.HashMalformed);
        }

        string dataCheckString = string.Join(
            '\n',
            fields.OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => $"{field.Key}={field.Value}"));

        byte[] computedHash = ComputeHash(dataCheckString, botToken);
        if (!CryptographicOperations.FixedTimeEquals(computedHash, suppliedHash))
        {
            return Failure(TelegramInitDataValidationErrorCodes.HashInvalid);
        }

        if (!fields.TryGetValue("auth_date", out string? authDateText)
            || !long.TryParse(authDateText, NumberStyles.None, CultureInfo.InvariantCulture, out long authDateSeconds))
        {
            return Failure(TelegramInitDataValidationErrorCodes.AuthDateInvalid);
        }

        DateTimeOffset authenticatedAtUtc;
        try
        {
            authenticatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(authDateSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failure(TelegramInitDataValidationErrorCodes.AuthDateInvalid);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (authenticatedAtUtc > now + maxFutureSkew)
        {
            return Failure(TelegramInitDataValidationErrorCodes.AuthDateInFuture);
        }

        if (now - authenticatedAtUtc > maxAge)
        {
            return Failure(TelegramInitDataValidationErrorCodes.Expired);
        }

        if (!fields.TryGetValue("user", out string? userJson))
        {
            return Failure(TelegramInitDataValidationErrorCodes.UserMissing);
        }

        if (!TryGetTelegramUserId(userJson, out long telegramUserId))
        {
            return Failure(TelegramInitDataValidationErrorCodes.UserInvalid);
        }

        return TelegramInitDataValidationResult.Success(
            new TelegramInitData(telegramUserId, authenticatedAtUtc));
    }

    private static TelegramInitDataValidationResult? TryParseFields(
        string initData,
        out Dictionary<string, string> fields)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string pair in initData.Split('&', StringSplitOptions.None))
        {
            int separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                return Failure(TelegramInitDataValidationErrorCodes.Malformed);
            }

            string encodedKey = pair[..separatorIndex];
            string encodedValue = pair[(separatorIndex + 1)..];
            if (!HasValidPercentEncoding(encodedKey) || !HasValidPercentEncoding(encodedValue))
            {
                return Failure(TelegramInitDataValidationErrorCodes.Malformed);
            }

            string key = WebUtility.UrlDecode(encodedKey);
            string value = WebUtility.UrlDecode(encodedValue);
            if (string.IsNullOrEmpty(key))
            {
                return Failure(TelegramInitDataValidationErrorCodes.Malformed);
            }

            if (!fields.TryAdd(key, value))
            {
                return Failure(TelegramInitDataValidationErrorCodes.DuplicateKey);
            }
        }

        return null;
    }

    private static bool HasValidPercentEncoding(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length
                || !Uri.IsHexDigit(value[index + 1])
                || !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }

    private static bool TryDecodeHash(string hashText, out byte[] hash)
    {
        hash = [];
        if (hashText.Length != SHA256.HashSizeInBytes * 2)
        {
            return false;
        }

        try
        {
            hash = Convert.FromHexString(hashText);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] ComputeHash(string dataCheckString, string botToken)
    {
        byte[] secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebAppDataKey),
            Encoding.UTF8.GetBytes(botToken));

        return HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
    }

    private static bool TryGetTelegramUserId(string userJson, out long telegramUserId)
    {
        telegramUserId = 0;

        try
        {
            using JsonDocument user = JsonDocument.Parse(userJson);
            return user.RootElement.ValueKind == JsonValueKind.Object
                && user.RootElement.TryGetProperty("id", out JsonElement id)
                && id.TryGetInt64(out telegramUserId)
                && telegramUserId > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TelegramInitDataValidationResult Failure(string errorCode) =>
        TelegramInitDataValidationResult.Failure(errorCode);
}
