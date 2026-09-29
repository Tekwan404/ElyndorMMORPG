using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Pvp;

public enum CrowdControlCategory
{
    Stun,
    Root,
    Fear,
    Incapacitate,
    Silence
}

public readonly record struct CrowdControlDrResolution(
    CrowdControlCategory Category,
    int Level,
    bool IsImmune,
    decimal DurationMultiplier,
    TimeSpan EffectiveDuration);

public sealed class CrowdControlDiminishingReturns
{
    public static readonly TimeSpan ResetWindow = TimeSpan.FromSeconds(15);

    private readonly Dictionary<CrowdControlCategory, CrowdControlDrState> _states = [];

    public CrowdControlDrResolution Resolve(
        CrowdControlCategory category,
        TimeSpan baseDuration,
        DateTimeOffset now)
    {
        if (baseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDuration));

        int level = GetLevel(category, now);
        if (level >= 3)
            return new CrowdControlDrResolution(category, level, true, 0, TimeSpan.Zero);

        decimal multiplier = level switch
        {
            0 => 1m,
            1 => 0.5m,
            2 => 0.25m,
            _ => 0m
        };
        long effectiveTicks = Math.Max(
            1L,
            checked((long)Math.Ceiling(baseDuration.Ticks * multiplier)));

        return new CrowdControlDrResolution(
            category,
            level,
            false,
            multiplier,
            TimeSpan.FromTicks(effectiveTicks));
    }

    public void Commit(
        CrowdControlDrResolution resolution,
        DateTimeOffset controlEndsAtUtc)
    {
        if (resolution.IsImmune)
            return;
        if (controlEndsAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Control end time must be UTC.", nameof(controlEndsAtUtc));

        _states[resolution.Category] = new CrowdControlDrState(
            Math.Min(3, resolution.Level + 1),
            controlEndsAtUtc + ResetWindow);
    }

    public int GetLevel(CrowdControlCategory category, DateTimeOffset now)
    {
        ResetIfExpired(category, now);
        return _states.TryGetValue(category, out CrowdControlDrState state)
            ? state.Level
            : 0;
    }

    internal CrowdControlDiminishingReturns Clone()
    {
        CrowdControlDiminishingReturns clone = new();
        clone.ReplaceWith(this);
        return clone;
    }

    internal void ReplaceWith(CrowdControlDiminishingReturns source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _states.Clear();
        foreach ((CrowdControlCategory category, CrowdControlDrState state) in source._states)
            _states[category] = state;
    }

    internal void RefreshResetWindow(
        CrowdControlCategory category,
        DateTimeOffset controlEndsAtUtc)
    {
        if (!_states.TryGetValue(category, out CrowdControlDrState state))
            return;
        _states[category] = state with { ResetAtUtc = controlEndsAtUtc + ResetWindow };
    }

    private void ResetIfExpired(CrowdControlCategory category, DateTimeOffset now)
    {
        if (_states.TryGetValue(category, out CrowdControlDrState state)
            && state.ResetAtUtc <= now)
        {
            _states.Remove(category);
        }
    }

    private readonly record struct CrowdControlDrState(
        int Level,
        DateTimeOffset ResetAtUtc);
}

public static class CrowdControlCategoryResolver
{
    public static bool TryResolve(EffectKind kind, out CrowdControlCategory category)
    {
        switch (kind)
        {
            case EffectKind.Stun:
                category = CrowdControlCategory.Stun;
                return true;
            case EffectKind.Root:
                category = CrowdControlCategory.Root;
                return true;
            case EffectKind.Fear:
                category = CrowdControlCategory.Fear;
                return true;
            case EffectKind.Silence:
                category = CrowdControlCategory.Silence;
                return true;
            default:
                category = default;
                return false;
        }
    }
}
