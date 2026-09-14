namespace Elyndor.Core.Combat.Paladin;

public sealed class PaladinProtectionRuntime
{
    private DateTimeOffset? _holyShieldEndsAtUtc;
    private DateTimeOffset? _consecrationEndsAtUtc;

    public DateTimeOffset? HolyShieldEndsAtUtc => _holyShieldEndsAtUtc;
    public DateTimeOffset? ConsecrationEndsAtUtc => _consecrationEndsAtUtc;

    public void ActivateHolyShield(DateTimeOffset now, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        _holyShieldEndsAtUtc = now + duration;
    }

    public void ActivateConsecration(DateTimeOffset now, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        _consecrationEndsAtUtc = now + duration;
    }

    public bool IsHolyShieldActive(DateTimeOffset now) =>
        _holyShieldEndsAtUtc is { } endsAt && endsAt > now;

    public bool IsConsecrationActive(DateTimeOffset now) =>
        _consecrationEndsAtUtc is { } endsAt && endsAt > now;

    public bool CanTriggerHolyShieldBlockEffect(DateTimeOffset now) =>
        IsHolyShieldActive(now);

    public static bool IsArdentDefenderActive(decimal currentHp, decimal maximumHp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentHp);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHp);
        return currentHp / maximumHp < 0.35m;
    }

    public decimal ResolveArdentDefenderReductionPercent(
        decimal currentHp,
        decimal maximumHp,
        decimal configuredReductionPercent)
    {
        ValidatePercent(configuredReductionPercent, nameof(configuredReductionPercent));
        return IsArdentDefenderActive(currentHp, maximumHp)
            ? configuredReductionPercent
            : 0;
    }

    public decimal ResolveConsecratedProtectionReductionPercent(
        DateTimeOffset now,
        decimal configuredReductionPercent)
    {
        ValidatePercent(configuredReductionPercent, nameof(configuredReductionPercent));
        return IsConsecrationActive(now)
            ? configuredReductionPercent
            : 0;
    }

    public static decimal ResolveIntercessionRedirectDamage(
        decimal postMitigationDamage,
        decimal configuredRedirectPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(postMitigationDamage);
        ValidatePercent(configuredRedirectPercent, nameof(configuredRedirectPercent));
        return decimal.Round(
            postMitigationDamage * configuredRedirectPercent / 100m,
            0,
            MidpointRounding.AwayFromZero);
    }

    private static void ValidatePercent(decimal value, string parameterName)
    {
        if (value is < 0 or > 100)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
