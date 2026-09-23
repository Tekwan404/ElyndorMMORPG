using System.Diagnostics;
using System.Globalization;

namespace Elyndor.Server.Monitoring;

public interface IServerMetricsCollector
{
    Task<ServerMetricsSnapshot> CollectAsync(CancellationToken cancellationToken);

    void RecordPlayerSeen(Guid accountId);
}

public sealed class ServerMetricsCollector(
    IHostEnvironment environment,
    TimeProvider timeProvider) : IServerMetricsCollector
{
    private readonly DateTimeOffset _startedAt = timeProvider.GetUtcNow();
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly string _contentRoot = environment.ContentRootPath;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly OnlinePlayerPresence _playerPresence = new(timeProvider);
    private readonly object _gate = new();
    private TimeSpan _lastProcessCpu;
    private DateTimeOffset _lastCpuAt;
    private (long Idle, long Total)? _lastHostCpu;
    private long _lastReceived;
    private long _lastSent;
    private DateTimeOffset _lastNetworkAt;

    public void RecordPlayerSeen(Guid accountId) => _playerPresence.MarkSeen(accountId);

    public Task<ServerMetricsSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        double? hostCpu = null;
        double? processCpu = null;
        long received = 0;
        long sent = 0;

        try
        {
            _process.Refresh();
            lock (_gate)
            {
                TimeSpan currentProcessCpu = _process.TotalProcessorTime;
                if (_lastCpuAt != default)
                {
                    double elapsed = (now - _lastCpuAt).TotalSeconds;
                    if (elapsed > 0)
                    {
                        processCpu = Math.Clamp(
                            (currentProcessCpu - _lastProcessCpu).TotalSeconds / elapsed / Environment.ProcessorCount * 100d,
                            0,
                            100);
                    }
                }

                _lastProcessCpu = currentProcessCpu;
                _lastCpuAt = now;

                (long idle, long total) currentHostCpu = ReadHostCpuTotals();
                if (_lastHostCpu.HasValue)
                {
                    long idleDelta = Math.Max(0, currentHostCpu.idle - _lastHostCpu.Value.Idle);
                    long totalDelta = Math.Max(0, currentHostCpu.total - _lastHostCpu.Value.Total);
                    if (totalDelta > 0)
                        hostCpu = Math.Clamp((1d - idleDelta / (double)totalDelta) * 100d, 0, 100);
                }

                _lastHostCpu = currentHostCpu;

                (long rx, long tx) = ReadNetworkTotals();
                if (_lastNetworkAt != default)
                {
                    double elapsed = (now - _lastNetworkAt).TotalSeconds;
                    if (elapsed > 0)
                    {
                        received = Math.Max(0, (long)((rx - _lastReceived) / elapsed));
                        sent = Math.Max(0, (long)((tx - _lastSent) / elapsed));
                    }
                }

                _lastReceived = rx;
                _lastSent = tx;
                _lastNetworkAt = now;
            }
        }
        catch
        {
            // Monitoring must never bring down the game server.
        }

        (long memoryUsed, long memoryTotal) = ReadMemory();
        (long diskUsed, long diskTotal, long diskFree) = ReadDisk();

        var snapshot = new ServerMetricsSnapshot(
            now,
            hostCpu ?? processCpu,
            processCpu,
            memoryUsed,
            memoryTotal,
            diskUsed,
            diskTotal,
            diskFree,
            received,
            sent,
            now - _startedAt,
            Environment.ProcessorCount,
            _playerPresence.CountOnline());

        return Task.FromResult(snapshot);
    }

    private static (long Idle, long Total) ReadHostCpuTotals()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/stat"))
            return (0, 0);

        string? line = File.ReadLines("/proc/stat")
            .FirstOrDefault(value => value.StartsWith("cpu ", StringComparison.Ordinal));
        if (line is null)
            return (0, 0);

        string[] values = line[4..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 4)
            return (0, 0);

        long[] counters = values.Take(8)
            .Select(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0)
            .ToArray();
        long total = counters.Sum();
        long idle = counters[3] + (counters.Length > 4 ? counters[4] : 0);
        return (idle, total);
    }

    private (long Used, long Total) ReadMemory()
    {
        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
            {
                long totalKb = 0;
                long availableKb = 0;
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal)
                        && long.TryParse(line[9..].Trim().Split(' ')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long memoryTotalKb))
                        totalKb = memoryTotalKb;
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal)
                        && long.TryParse(line[13..].Trim().Split(' ')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long availableKbValue))
                        availableKb = availableKbValue;
                }

                if (totalKb > 0)
                    return (Math.Max(0, totalKb - availableKb) * 1024, totalKb * 1024);
            }

            long availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return (Math.Max(0, _process.WorkingSet64), availableMemory);
        }
        catch
        {
            return (0, 0);
        }
    }

    private (long Used, long Total, long Free) ReadDisk()
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(_contentRoot))
                ?? Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture);
            DriveInfo drive = new(root);
            return (drive.TotalSize - drive.AvailableFreeSpace, drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static (long Received, long Sent) ReadNetworkTotals()
    {
        try
        {
            long rx = 0;
            long tx = 0;
            const string proc = "/proc/net/dev";
            if (!File.Exists(proc))
                return (0, 0);

            foreach (string line in File.ReadLines(proc).Skip(2))
            {
                int separator = line.IndexOf(':');
                if (separator < 0)
                    continue;

                string[] values = line[(separator + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 9)
                    continue;

                if (long.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long interfaceRx))
                    rx += interfaceRx;
                if (long.TryParse(values[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out long interfaceTx))
                    tx += interfaceTx;
            }

            return (rx, tx);
        }
        catch
        {
            return (0, 0);
        }
    }
}
