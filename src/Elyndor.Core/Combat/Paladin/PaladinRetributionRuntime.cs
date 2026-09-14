namespace Elyndor.Core.Combat.Paladin;

public sealed class PaladinRetributionRuntime
{
    private const int MaximumVengeanceStacks = 3;
    private const int DivinePurposeJudgementCount = 3;

    private DateTimeOffset? _vengeanceEndsAtUtc;
    private DateTimeOffset? _avengingWrathEndsAtUtc;
    private int _successfulJudgementsSinceDivinePurpose;
    private bool _divinePurposeArmed;
    private bool _zealArmed;
    private bool _incarnationTemplarCriticalAvailable;
    private bool _incarnationJudgementResetAvailable;

    public int VengeanceStacks { get; private set; }
    public DateTimeOffset? VengeanceEndsAtUtc => _vengeanceEndsAtUtc;
    public DateTimeOffset? AvengingWrathEndsAtUtc => _avengingWrathEndsAtUtc;
    public bool DivinePurposeArmed => _divinePurposeArmed;
    public bool ZealArmed => _zealArmed;

    public void RecordPhysicalOrHolyCritical(
        DateTimeOffset now,
        TimeSpan vengeanceDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            vengeanceDuration,
            TimeSpan.Zero);

        ExpireVengeance(now);
        VengeanceStacks = Math.Min(MaximumVengeanceStacks, VengeanceStacks + 1);
        _vengeanceEndsAtUtc = now + vengeanceDuration;
    }

    public void ExpireVengeance(DateTimeOffset now)
    {
        if (_vengeanceEndsAtUtc is { } endsAt && endsAt <= now)
        {
            VengeanceStacks = 0;
            _vengeanceEndsAtUtc = null;
        }
    }

    public bool RecordSuccessfulJudgement(bool hasDivinePurpose)
    {
        _zealArmed = true;
        if (!hasDivinePurpose)
        {
            return false;
        }

        _successfulJudgementsSinceDivinePurpose++;
        if (_successfulJudgementsSinceDivinePurpose < DivinePurposeJudgementCount)
        {
            return false;
        }

        _successfulJudgementsSinceDivinePurpose = 0;
        _divinePurposeArmed = true;
        return true;
    }

    public bool ConsumeZealForNextAutoAttack()
    {
        if (!_zealArmed)
        {
            return false;
        }

        _zealArmed = false;
        return true;
    }

    public bool ConsumeDivinePurposeForTemplarsVerdict()
    {
        if (!_divinePurposeArmed)
        {
            return false;
        }

        _divinePurposeArmed = false;
        return true;
    }

    public void ActivateAvengingWrath(
        DateTimeOffset now,
        TimeSpan duration,
        bool hasIncarnationOfRetribution)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        _avengingWrathEndsAtUtc = now + duration;
        _incarnationTemplarCriticalAvailable = hasIncarnationOfRetribution;
        _incarnationJudgementResetAvailable = hasIncarnationOfRetribution;
    }

    public bool IsAvengingWrathActive(DateTimeOffset now) =>
        _avengingWrathEndsAtUtc is { } endsAt && endsAt > now;

    public bool ConsumeIncarnationTemplarCritical(DateTimeOffset now)
    {
        if (!IsAvengingWrathActive(now)
            || !_incarnationTemplarCriticalAvailable)
        {
            return false;
        }

        _incarnationTemplarCriticalAvailable = false;
        return true;
    }

    public bool ConsumeIncarnationJudgementCrusaderReset(DateTimeOffset now)
    {
        if (!IsAvengingWrathActive(now)
            || !_incarnationJudgementResetAvailable)
        {
            return false;
        }

        _incarnationJudgementResetAvailable = false;
        return true;
    }
}
