using System.Runtime.CompilerServices;

namespace Elyndor.Core.Combat;

/// <summary>Session-owned, single-writer proc admission. Never used for normal ability effects.</summary>
public sealed class ProcGuard
{
    private readonly Dictionary<(Guid Actor, string Hook), long> _sequences = new();
    private readonly Dictionary<(Guid Actor, string Hook), DateTimeOffset> _cooldowns = new();
    private ConditionalWeakTable<object, HashSet<(Guid Actor, string Hook)>> _observations = new();

    public static bool IsEligible(bool periodic, bool isProc, int depth) =>
        !periodic && !isProc && depth == 0;

    public static bool IsEligible(CombatEvent input) =>
        !input.IsReflected && IsEligible(input.IsPeriodic, input.IsProc, input.ProcDepth);

    public static bool IsEligible(CombatRuntimeEvent input) =>
        IsEligible(input.IsPeriodic, input.IsProc, input.ProcDepth);

    // Observe before RNG/counters, commit cooldown only after a successful proc.
    public bool TryObserve(Guid actor, string hook, object input, long sequence)
    {
        var key = (actor, hook);
        if (!_observations.GetOrCreateValue(input).Add(key))
            return false;
        if (sequence <= 0)
            return true;
        if (_sequences.TryGetValue(key, out long last) && sequence <= last)
            return false;
        _sequences[key] = sequence;
        return true;
    }

    public bool IsReady(Guid actor, string hook, DateTimeOffset now) =>
        !_cooldowns.TryGetValue((actor, hook), out DateTimeOffset readyAt) || now >= readyAt;

    public void StartCooldown(Guid actor, string hook, DateTimeOffset now, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        if (duration > TimeSpan.Zero)
            _cooldowns[(actor, hook)] = now + duration;
    }

    public void Reset()
    {
        _sequences.Clear();
        _cooldowns.Clear();
        _observations = new();
    }
}
