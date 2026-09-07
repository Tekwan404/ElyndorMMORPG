using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Elyndor.Server;

public static class ServerRateLimitPolicies
{
    public const string Authentication = "authentication";
}

public sealed class ServerRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int ApiPermitLimit { get; init; } = 240;
    public int AuthenticationPermitLimit { get; init; } = 20;
    public int WindowSeconds { get; init; } = 60;
    public int CombatCommandPermitLimit { get; init; } = 20;
    public int CombatCommandWindowSeconds { get; init; } = 1;

    public bool IsValid() =>
        ApiPermitLimit > 0
        && AuthenticationPermitLimit > 0
        && WindowSeconds > 0
        && CombatCommandPermitLimit > 0
        && CombatCommandWindowSeconds > 0;
}

public sealed class CombatCommandRateLimiter(
    ServerRateLimitingOptions settings) : IDisposable
{
    private readonly PartitionedRateLimiter<Guid> _limiter =
        PartitionedRateLimiter.Create<Guid, Guid>(
            accountId => RateLimitPartition.GetFixedWindowLimiter(
                accountId,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.CombatCommandPermitLimit,
                    Window = TimeSpan.FromSeconds(
                        settings.CombatCommandWindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public bool TryAcquire(Guid accountId)
    {
        if (accountId == Guid.Empty)
            return false;

        using RateLimitLease lease = _limiter.AttemptAcquire(accountId, 1);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}

public static class ServerRateLimiting
{
    public static IServiceCollection AddElyndorRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ServerRateLimitingOptions settings =
            configuration.GetSection(ServerRateLimitingOptions.SectionName)
                .Get<ServerRateLimitingOptions>()
            ?? new ServerRateLimitingOptions();

        if (!settings.IsValid())
        {
            throw new InvalidOperationException(
                "Rate limiting requires positive permit limits and windows.");
        }

        TimeSpan window = TimeSpan.FromSeconds(settings.WindowSeconds);

        services.AddSingleton(settings);
        services.AddSingleton<CombatCommandRateLimiter>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                HttpResponse response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(
                        MetadataName.RetryAfter,
                        out TimeSpan retryAfter))
                {
                    response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                            .ToString(CultureInfo.InvariantCulture);
                }

                ProblemDetails problem = new()
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests"
                };
                problem.Extensions["code"] = "rate_limited";
                problem.Extensions["correlationId"] =
                    context.HttpContext.TraceIdentifier;

                await response.WriteAsJsonAsync(
                    problem,
                    cancellationToken);
            };

            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(
                    httpContext =>
                    {
                        if (!IsProtectedTransport(httpContext.Request.Path))
                            return RateLimitPartition.GetNoLimiter("unlimited");

                        return RateLimitPartition.GetFixedWindowLimiter(
                            $"api:{PartitionKey(httpContext)}",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = settings.ApiPermitLimit,
                                Window = window,
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                    });

            options.AddPolicy(
                ServerRateLimitPolicies.Authentication,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        $"auth:{ClientAddress(httpContext)}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.AuthenticationPermitLimit,
                            Window = window,
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
        });

        return services;
    }

    private static bool IsProtectedTransport(PathString path) =>
        path.StartsWithSegments("/api")
        || path.StartsWithSegments("/hubs");

    private static string PartitionKey(HttpContext context)
    {
        string? accountId = context.User.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(accountId)
            ? $"ip:{ClientAddress(context)}"
            : $"account:{accountId}";
    }

    private static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
