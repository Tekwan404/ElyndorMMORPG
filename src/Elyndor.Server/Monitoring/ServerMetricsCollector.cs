using System.Diagnostics;
using System.Globalization;

namespace Elyndor.Server.Monitoring;

public interface IServerMetricsCollector
{
    Task<ServerMetricsSnapshot> CollectAsync(CancellationToken cancellationToken);
}

public sealed class ServerMetricsCollector(IHostEnvironment environment) : IServerMetricsCollector
{
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly string _contentRoot = environment.ContentRootPath;
    private readonly object _gate = new();
    private TimeSpan _lastCpu;
    private DateTimeOffset _lastCpuAt;
    private long _lastReceived;
    private long _lastSent;
    private DateTimeOffset _lastNetworkAt;

    public Task<ServerMetricsSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        double? cpu = null;
        double? processCpu = null;
        long received = 0;
        long sent = 0;

        try
        {
            _process.Refresh();
            lock (_gate)
            {
                TimeSpan currentCpu = _process.TotalProcessorTime;
                if (_lastCpuAt != default)
                {
                    double elapsed = (now - _lastCpuAt).TotalSeconds;
                    if (elapsed > 0)
                    {
                        processCpu = Math.Clamp(
                            (currentCpu - _lastCpu).TotalSeconds / elapsed / Environment.ProcessorCount * 100d,
                            0,
                            100);
                        cpu = processCpu;
                    }
                }

                _lastCpu = currentCpu;
                _lastCpuAt = now;

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
            // Resource monitoring must never bring down the game server.
        }

        (long diskUsed, long diskTotal, long diskFree) = ReadDisk();
        long memoryTotal = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        long memoryUsed = Math.Max(0, memoryTotal - GC.GetGCMemoryInfo().MemoryLoadBytes);

        var snapshot = new ServerMetricsSnapshot(
            now,
            cpu,
            processCpu,
            memoryUsed,
            memoryTotal,
            diskUsed,
            diskTotal,
            diskFree,
            received,
            sent,
            now - _startedAt,
            Environment.ProcessorCount);

        return Task.FromResult(snapshot);
    }

    private (long Used, long Total, long Free) ReadDisk()
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(_contentRoot))
                ?? Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture);
            DriveInfo drive = new(root);
            return (
                drive.TotalSize - drive.AvailableFreeSpace,
                drive.TotalSize,
                drive.AvailableFreeSpace);
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
            string proc = "/proc/net/dev";
            if (!File.Exists(proc))
                return (0, 0);

            foreach (string line in File.ReadLines(proc).Skip(2))
            {
                int separator = line.IndexOf(':');
                if (separator < 0)
                    continue;

                string[] values = line[(separator + 1)..]
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 9)
                    continue;

                if (long.TryParse(values[0], out long interfaceRx))
                    rx += interfaceRx;
                if (long.TryParse(values[8], out long interfaceTx))
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
