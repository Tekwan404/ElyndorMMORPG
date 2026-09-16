namespace Elyndor.Server.Monitoring;

public sealed record ServerMetricsSnapshot(
    DateTimeOffset Timestamp,
    double? CpuPercent,
    double? ProcessCpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    long DiskUsedBytes,
    long DiskTotalBytes,
    long DiskFreeBytes,
    long NetworkReceivedBytesPerSecond,
    long NetworkSentBytesPerSecond,
    TimeSpan ProcessUptime,
    int ProcessorCount)
{
    public double MemoryPercent =>
        MemoryTotalBytes <= 0 ? 0 : MemoryUsedBytes * 100d / MemoryTotalBytes;

    public double DiskPercent =>
        DiskTotalBytes <= 0 ? 0 : DiskUsedBytes * 100d / DiskTotalBytes;

    public double MemoryUsedGiB => MemoryUsedBytes / 1024d / 1024d / 1024d;

    public double MemoryTotalGiB => MemoryTotalBytes / 1024d / 1024d / 1024d;

    public double DiskUsedGiB => DiskUsedBytes / 1024d / 1024d / 1024d;

    public double DiskTotalGiB => DiskTotalBytes / 1024d / 1024d / 1024d;
}

public sealed record HealthCheckResult(
    string Name,
    bool IsHealthy,
    TimeSpan? Latency,
    string? Detail = null);

public sealed record ElyndorHealthSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyList<HealthCheckResult> Checks)
{
    public bool IsHealthy => Checks.All(check => check.IsHealthy);

    public bool IsDegraded => Checks.Any(check => check.IsHealthy) && !IsHealthy;
}

public sealed record ElyndorStatusSnapshot(
    ServerMetricsSnapshot Metrics,
    ElyndorHealthSnapshot Health,
    string Environment,
    string Version,
    string Commit);
