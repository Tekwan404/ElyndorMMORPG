using System.Collections.Concurrent;

namespace Elyndor.Server.Monitoring;

public sealed class ServerErrorMetrics
{
    private readonly ConcurrentQueue<ErrorEvent> _events = new();

    public void Record(Exception exception, string? path)
    {
        _events.Enqueue(new(DateTimeOffset.UtcNow, exception.GetType().Name, path ?? string.Empty));
        Trim(DateTimeOffset.UtcNow - TimeSpan.FromHours(24));
    }

    public int GetCount(TimeSpan window)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - window;
        Trim(cutoff);
        return _events.Count(item => item.Timestamp >= cutoff);
    }

    public IReadOnlyList<ErrorEvent> GetRecent(int max = 10)
    {
        Trim(DateTimeOffset.UtcNow - TimeSpan.FromHours(24));
        return _events.Reverse().Take(Math.Max(1, max)).ToArray();
    }

    private void Trim(DateTimeOffset cutoff)
    {
        while (_events.TryPeek(out ErrorEvent? item) && item.Timestamp < cutoff)
            _events.TryDequeue(out _);
    }

    public sealed record ErrorEvent(DateTimeOffset Timestamp, string Type, string Path);
}
