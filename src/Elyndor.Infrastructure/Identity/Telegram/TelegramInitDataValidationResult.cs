namespace Elyndor.Infrastructure.Identity.Telegram;

public sealed record TelegramInitData(
    long TelegramUserId,
    DateTimeOffset AuthenticatedAtUtc);

public sealed record TelegramInitDataValidationResult(
    bool IsValid,
    TelegramInitData? Data,
    string? ErrorCode)
{
    public static TelegramInitDataValidationResult Success(TelegramInitData data) =>
        new(true, data, null);

    public static TelegramInitDataValidationResult Failure(string errorCode) =>
        new(false, null, errorCode);
}

public static class TelegramInitDataValidationErrorCodes
{
    public const string Missing = "telegram_init_data_missing";
    public const string Malformed = "telegram_init_data_malformed";
    public const string DuplicateKey = "telegram_init_data_duplicate_key";
    public const string HashMissing = "telegram_init_data_hash_missing";
    public const string HashMalformed = "telegram_init_data_hash_malformed";
    public const string HashInvalid = "telegram_init_data_hash_invalid";
    public const string AuthDateInvalid = "telegram_init_data_auth_date_invalid";
    public const string AuthDateInFuture = "telegram_init_data_auth_date_in_future";
    public const string Expired = "telegram_init_data_expired";
    public const string UserMissing = "telegram_init_data_user_missing";
    public const string UserInvalid = "telegram_init_data_user_invalid";
}
