using System.Security.Cryptography;
using System.Text;
using Elyndor.Infrastructure.Administration;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public enum AdminWebAuthenticationIssueStatus
{
    Issued,
    NotAllowed,
    RateLimited,
    DeliveryFailed
}

public sealed record AdminWebAuthenticationIssueResult(
    AdminWebAuthenticationIssueStatus Status,
    Guid? ChallengeId = null,
    DateTimeOffset? ExpiresAtUtc = null);

public enum AdminWebAuthenticationVerificationStatus
{
    Success,
    Invalid,
    Expired,
    NotAllowed
}

public enum AdminWebPasswordVerificationStatus
{
    Success,
    Invalid,
    RateLimited,
    Disabled,
    NotAllowed
}

public sealed class AdminWebAuthenticationService(
    ITelegramMessageSender messageSender,
    IOptions<TelegramAdminOptions> adminOptions,
    IOptions<AdminWebAuthenticationOptions> webAuthenticationOptions,
    TimeProvider timeProvider)
{
    public const int CodeLifetimeMinutes = 5;
    public const int RequestCooldownSeconds = 30;
    public const int MaximumFailedAttempts = 5;
    public const int PasswordLockoutMinutes = 5;

    private readonly object _gate = new();
    private readonly Dictionary<long, Challenge> _challenges = [];
    private readonly Dictionary<long, PasswordFailureState> _passwordFailures = [];

    public async Task<AdminWebAuthenticationIssueResult> IssueCodeAsync(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        if (!adminOptions.Value.IsAllowedUser(telegramUserId))
        {
            return new(AdminWebAuthenticationIssueStatus.NotAllowed);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid challengeId = Guid.CreateVersion7();
        string code = RandomNumberGenerator
            .GetInt32(100_000, 1_000_000)
            .ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset expiresAtUtc = now.AddMinutes(CodeLifetimeMinutes);
        Challenge challenge = new(
            challengeId,
            telegramUserId,
            Hash(challengeId, code),
            now,
            expiresAtUtc);

        lock (_gate)
        {
            RemoveExpiredUnsafe(now);

            if (_challenges.TryGetValue(telegramUserId, out Challenge? existing)
                && now - existing.IssuedAtUtc
                    < TimeSpan.FromSeconds(RequestCooldownSeconds))
            {
                return new(AdminWebAuthenticationIssueStatus.RateLimited);
            }

            _challenges[telegramUserId] = challenge;
        }

        try
        {
            await messageSender.SendAsync(
                telegramUserId,
                $"Elyndor Admin\n\nКод входа: {code}\n\n"
                + "Код действует 5 минут. Если вы не запрашивали вход, "
                + "проигнорируйте сообщение.",
                cancellationToken);
        }
        catch
        {
            lock (_gate)
            {
                if (_challenges.TryGetValue(telegramUserId, out Challenge? current)
                    && current.Id == challengeId)
                {
                    _challenges.Remove(telegramUserId);
                }
            }

            return new(AdminWebAuthenticationIssueStatus.DeliveryFailed);
        }

        return new(
            AdminWebAuthenticationIssueStatus.Issued,
            challengeId,
            expiresAtUtc);
    }

    public AdminWebAuthenticationVerificationStatus VerifyCode(
        Guid challengeId,
        long telegramUserId,
        string? code)
    {
        if (!adminOptions.Value.IsAllowedUser(telegramUserId))
        {
            return AdminWebAuthenticationVerificationStatus.NotAllowed;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        lock (_gate)
        {
            if (!_challenges.TryGetValue(telegramUserId, out Challenge? challenge)
                || challenge.Id != challengeId)
            {
                return AdminWebAuthenticationVerificationStatus.Invalid;
            }

            if (now >= challenge.ExpiresAtUtc)
            {
                _challenges.Remove(telegramUserId);
                return AdminWebAuthenticationVerificationStatus.Expired;
            }

            bool codeShapeValid = code is { Length: 6 }
                && code.All(char.IsAsciiDigit);
            bool isValid = codeShapeValid
                && CryptographicOperations.FixedTimeEquals(
                    challenge.CodeHash,
                    Hash(challenge.Id, code!));

            if (!isValid)
            {
                challenge.FailedAttempts++;
                if (challenge.FailedAttempts >= MaximumFailedAttempts)
                {
                    _challenges.Remove(telegramUserId);
                }

                return AdminWebAuthenticationVerificationStatus.Invalid;
            }

            _challenges.Remove(telegramUserId);
            return AdminWebAuthenticationVerificationStatus.Success;
        }
    }

    public AdminWebPasswordVerificationStatus VerifyEmergencyPassword(
        long telegramUserId,
        string? password)
    {
        if (!adminOptions.Value.IsAllowedUser(telegramUserId))
        {
            return AdminWebPasswordVerificationStatus.NotAllowed;
        }

        AdminWebAuthenticationOptions options = webAuthenticationOptions.Value;
        if (!options.EmergencyPasswordEnabled)
        {
            return AdminWebPasswordVerificationStatus.Disabled;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        lock (_gate)
        {
            if (_passwordFailures.TryGetValue(
                    telegramUserId,
                    out PasswordFailureState? failureState)
                && failureState.LockedUntilUtc is DateTimeOffset lockedUntilUtc)
            {
                if (now < lockedUntilUtc)
                {
                    return AdminWebPasswordVerificationStatus.RateLimited;
                }

                _passwordFailures.Remove(telegramUserId);
            }

            bool passwordShapeValid = password is { Length: > 0 and <= 512 };
            bool isValid = passwordShapeValid
                && CryptographicOperations.FixedTimeEquals(
                    HashPassword(options.EmergencyPassword),
                    HashPassword(password!));

            if (isValid)
            {
                _passwordFailures.Remove(telegramUserId);
                return AdminWebPasswordVerificationStatus.Success;
            }

            PasswordFailureState state = _passwordFailures.TryGetValue(
                telegramUserId,
                out PasswordFailureState? existing)
                    ? existing
                    : new PasswordFailureState();
            state.FailedAttempts++;

            if (state.FailedAttempts >= MaximumFailedAttempts)
            {
                state.LockedUntilUtc = now.AddMinutes(PasswordLockoutMinutes);
            }

            _passwordFailures[telegramUserId] = state;
            return AdminWebPasswordVerificationStatus.Invalid;
        }
    }

    private void RemoveExpiredUnsafe(DateTimeOffset now)
    {
        long[] expired = _challenges
            .Where(pair => now >= pair.Value.ExpiresAtUtc)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (long telegramUserId in expired)
        {
            _challenges.Remove(telegramUserId);
        }
    }

    private static byte[] Hash(Guid challengeId, string code) =>
        SHA256.HashData(
            Encoding.UTF8.GetBytes($"{challengeId:N}:{code}"));

    private static byte[] HashPassword(string password) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(password));

    private sealed class PasswordFailureState
    {
        public int FailedAttempts { get; set; }

        public DateTimeOffset? LockedUntilUtc { get; set; }
    }

    private sealed class Challenge(
        Guid id,
        long telegramUserId,
        byte[] codeHash,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        public Guid Id { get; } = id;
        public long TelegramUserId { get; } = telegramUserId;
        public byte[] CodeHash { get; } = codeHash;
        public DateTimeOffset IssuedAtUtc { get; } = issuedAtUtc;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public int FailedAttempts { get; set; }
    }
}
