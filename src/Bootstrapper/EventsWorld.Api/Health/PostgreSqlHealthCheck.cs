using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace EventsWorld.Api.Health;

/// <summary>
/// Readiness: the API can reach PostgreSQL with its own connection string
/// (<c>ConnectionStrings:Database</c>, the <c>ew_app</c> role).
/// </summary>
public sealed partial class PostgreSqlHealthCheck(
    IConfiguration configuration,
    ILogger<PostgreSqlHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringNames.Database);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy($"ConnectionStrings:{ConnectionStringNames.Database} is not configured.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            LogReadinessFailed(logger, exception);
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "PostgreSQL readiness check failed.")]
    private static partial void LogReadinessFailed(ILogger logger, Exception exception);
}

public static class HealthCheckExtensions
{
    private const string LiveTag = "live";
    private const string ReadyTag = "ready";

    /// <summary>
    /// <c>/health/live</c>: the process answers. <c>/health/ready</c>: the dependencies answer
    /// (PostgreSQL now, R2 in TS-007), with a time limit so a hung database fails fast.
    /// </summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag])
            .AddCheck<PostgreSqlHealthCheck>("postgres", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
        });

        return endpoints;
    }
}
