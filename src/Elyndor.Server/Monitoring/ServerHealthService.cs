using System.Diagnostics;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Monitoring;

public interface IServerHealthService
{
    Task<ElyndorHealthSnapshot> CheckAsync(CancellationToken cancellationToken);
}

public sealed class ServerHealthService(GameDbContext dbContext) : IServerHealthService
{
    public async Task<ElyndorHealthSnapshot> CheckAsync(CancellationToken cancellationToken)
    {
        var checks = new List<HealthCheckResult>();

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();
            checks.Add(new HealthCheckResult(
                "PostgreSQL",
                canConnect,
                stopwatch.Elapsed,
                canConnect ? "connection ok" : "connection failed"));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            checks.Add(new HealthCheckResult(
                "PostgreSQL",
                false,
                stopwatch.Elapsed,
                exception.GetType().Name));
        }

        checks.Add(new HealthCheckResult("Process", true, TimeSpan.Zero, "running"));
        return new ElyndorHealthSnapshot(DateTimeOffset.UtcNow, checks);
    }
}
