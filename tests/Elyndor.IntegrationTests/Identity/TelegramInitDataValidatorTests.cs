using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Infrastructure.Identity.Telegram;

namespace Elyndor.IntegrationTests.Identity;

public sealed class TelegramInitDataValidatorTests
{
    private const string BotToken = "123456:TEST_TOKEN";
    private const string ValidInitData =
        "auth_date=1788048000&query_id=AAEAAAE&user=%7B%22id%22%3A42%2C%22first_name%22%3A%22Test%22%7D"
        + "&hash=b56cf8f51cc2cb391171b7dbbcac72e8f2aee00d6a7a284abdb3ee9caf016a0a";

    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromSeconds(30);

    [Fact]
    public void ValidateAcceptsOfficialBotTokenHmacFlow()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:02:00Z");

        TelegramInitDataValidationResult result = validator.Validate(
            ValidInitData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Data);
        Assert.Equal(42, result.Data.TelegramUserId);
        Assert.Equal(
            DateTimeOffset.Parse("2026-08-30T00:00:00Z", CultureInfo.InvariantCulture),
            result.Data.AuthenticatedAtUtc);
    }

    [Fact]
    public void ValidateRejectsInvalidHash()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:02:00Z");
        string initData = ValidInitData.Replace("b56cf8", "a56cf8", StringComparison.Ordinal);

        TelegramInitDataValidationResult result = validator.Validate(
            initData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.False(result.IsValid);
        Assert.Equal(TelegramInitDataValidationErrorCodes.HashInvalid, result.ErrorCode);
    }

    [Fact]
    public void ValidateRejectsExpiredAuthenticationDate()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:06:00Z");

        TelegramInitDataValidationResult result = validator.Validate(
            ValidInitData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.Equal(TelegramInitDataValidationErrorCodes.Expired, result.ErrorCode);
    }

    [Fact]
    public void ValidateRejectsAuthenticationDateBeyondFutureSkew()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-29T23:59:00Z");

        TelegramInitDataValidationResult result = validator.Validate(
            ValidInitData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.Equal(TelegramInitDataValidationErrorCodes.AuthDateInFuture, result.ErrorCode);
    }

    [Fact]
    public void ValidateRejectsDuplicateQueryKeys()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:02:00Z");

        TelegramInitDataValidationResult result = validator.Validate(
            $"{ValidInitData}&auth_date=1788048000",
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.Equal(TelegramInitDataValidationErrorCodes.DuplicateKey, result.ErrorCode);
    }

    [Fact]
    public void ValidateRejectsMalformedHash()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:02:00Z");
        string initData = ValidInitData[..ValidInitData.LastIndexOf("hash=", StringComparison.Ordinal)]
            + "hash=xyz";

        TelegramInitDataValidationResult result = validator.Validate(
            initData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.Equal(TelegramInitDataValidationErrorCodes.HashMalformed, result.ErrorCode);
    }

    [Fact]
    public void ValidateRejectsInvalidAuthenticationDate()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:02:00Z");
        string initData = CreateSignedInitData(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["auth_date"] = "not-a-timestamp",
                ["user"] = "{\"id\":42}"
            });

        TelegramInitDataValidationResult result = validator.Validate(
            initData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.Equal(TelegramInitDataValidationErrorCodes.AuthDateInvalid, result.ErrorCode);
    }

    [Fact]
    public void ValidateRejectsMissingUser()
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:02:00Z");
        string initData = CreateSignedInitData(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["auth_date"] = "1788048000"
            });

        TelegramInitDataValidationResult result = validator.Validate(
            initData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.Equal(TelegramInitDataValidationErrorCodes.UserMissing, result.ErrorCode);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"id\":0}")]
    public void ValidateRejectsInvalidUser(string userJson)
    {
        TelegramInitDataValidator validator = CreateValidator("2026-08-30T00:02:00Z");
        string initData = CreateSignedInitData(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["auth_date"] = "1788048000",
                ["user"] = userJson
            });

        TelegramInitDataValidationResult result = validator.Validate(
            initData,
            BotToken,
            MaxAge,
            MaxFutureSkew);

        Assert.Equal(TelegramInitDataValidationErrorCodes.UserInvalid, result.ErrorCode);
    }

    private static TelegramInitDataValidator CreateValidator(string utcNow) =>
        new(new FixedTimeProvider(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture)));

    private static string CreateSignedInitData(IReadOnlyDictionary<string, string> fields)
    {
        string dataCheckString = string.Join(
            '\n',
            fields.OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => $"{field.Key}={field.Value}"));

        byte[] secretKey;
        using (HMACSHA256 secretHmac = new(Encoding.UTF8.GetBytes("WebAppData")))
        {
            secretKey = secretHmac.ComputeHash(Encoding.UTF8.GetBytes(BotToken));
        }

        byte[] hash;
        using (HMACSHA256 dataHmac = new(secretKey))
        {
            hash = dataHmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        }

        IEnumerable<string> encodedFields = fields.Select(field =>
            $"{WebUtility.UrlEncode(field.Key)}={WebUtility.UrlEncode(field.Value)}");

        return $"{string.Join('&', encodedFields)}&hash={Convert.ToHexStringLower(hash)}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
