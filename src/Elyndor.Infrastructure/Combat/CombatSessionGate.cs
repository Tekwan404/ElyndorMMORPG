using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Elyndor.Infrastructure.Combat;

public static class CombatSessionMetrics
{
    public const string MeterName = "Elyndor.Combat";
    private static readonly Meter Meter = new(MeterName);
    internal static readonly Histogram<double> GateHold = Meter.CreateHistogram<double>(
        "elyndor.combat.gate.hold.duration", "ms", "Time holding a combat session single-writer gate.");
    internal static readonly Histogram<double> GateWait = Meter.CreateHistogram<double>(
        "elyndor.combat.gate.wait.duration", "ms", "Time waiting for a combat session single-writer gate.");
}

internal sealed class CombatSessionGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private long _acquiredAt;

    public void Wait()
    {
        long started = Stopwatch.GetTimestamp();
        _semaphore.Wait();
        Acquired(started);
    }

    public bool Wait(int millisecondsTimeout)
    {
        long started = Stopwatch.GetTimestamp();
        if (!_semaphore.Wait(millisecondsTimeout)) return false;
        Acquired(started);
        return true;
    }

    public async Task WaitAsync()
    {
        long started = Stopwatch.GetTimestamp();
        await _semaphore.WaitAsync();
        Acquired(started);
    }

    public async Task<bool> WaitAsync(int millisecondsTimeout) =>
        await WaitAsync(TimeSpan.FromMilliseconds(millisecondsTimeout), CancellationToken.None);

    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        bool acquired = await _semaphore.WaitAsync(timeout, cancellationToken);
        if (acquired) Acquired(started);
        else CombatSessionMetrics.GateWait.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return acquired;
    }

    private void Acquired(long started)
    {
        _acquiredAt = Stopwatch.GetTimestamp();
        CombatSessionMetrics.GateWait.Record(Stopwatch.GetElapsedTime(started, _acquiredAt).TotalMilliseconds);
    }

    public void Dispose() => _semaphore.Dispose();

    public void Release()
    {
        double duration = Stopwatch.GetElapsedTime(_acquiredAt).TotalMilliseconds;
        _semaphore.Release();
        CombatSessionMetrics.GateHold.Record(duration);
    }
}
