using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

internal sealed class PostgresStabilityHealthCheck(
    Func<CancellationToken, ValueTask<string?>> connectionStringFactory)
    : IHealthCheck
{
    private const int RequiredSuccessfulProbes = 6;
    private static readonly TimeSpan ProbeDelay = TimeSpan.FromMilliseconds(500);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string? connectionString =
                await connectionStringFactory(cancellationToken);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return HealthCheckResult.Unhealthy(
                    "PostgreSQL connection string is not allocated yet.");
            }

            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using NpgsqlCommand command =
                new("SELECT 1", connection)
                {
                    CommandTimeout = 2
                };

            for (int probe = 0; probe < RequiredSuccessfulProbes; probe++)
            {
                object? result = await command.ExecuteScalarAsync(cancellationToken);
                if (!Equals(result, 1))
                {
                    return HealthCheckResult.Unhealthy(
                        $"PostgreSQL stability probe {probe + 1} returned an unexpected result.");
                }

                if (probe + 1 < RequiredSuccessfulProbes)
                {
                    await Task.Delay(ProbeDelay, cancellationToken);
                }
            }

            return HealthCheckResult.Healthy(
                $"PostgreSQL stayed reachable for {RequiredSuccessfulProbes} consecutive probes.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is not stable yet.",
                exception);
        }
    }
}
