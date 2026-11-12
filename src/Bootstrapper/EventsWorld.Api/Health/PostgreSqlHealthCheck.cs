using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace EventsWorld.Api.Health;

public sealed partial class PostgreSqlHealthCheck(
    IConfiguration configuration,
    ILogger<PostgreSqlHealthCheck> logger) : IHealthCheck
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    [LoggerMessage(Level = LogLevel.Warning, Message = "PostgreSQL readiness check failed: {ErrorMessage}")]
    private static partial void LogReadinessFailed(ILogger logger, string? errorMessage);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration["POSTGRES_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connection string is not configured.");
        }

        var executionResult = await SafeExecuteDbCheckAsync(connectionString, cancellationToken);

        if (executionResult.IsHealthy)
        {
            return HealthCheckResult.Healthy("PostgreSQL database is reachable.");
        }

        LogReadinessFailed(logger, executionResult.ErrorMessage);
        return HealthCheckResult.Unhealthy($"PostgreSQL database is unreachable: {executionResult.ErrorMessage}");
    }

    private static async Task<(bool IsHealthy, string? ErrorMessage)> SafeExecuteDbCheckAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

public static class HealthCheckExtensions
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Live"), tags: [PostgreSqlHealthCheck.LiveTag])
            .AddCheck<PostgreSqlHealthCheck>("postgres", tags: [PostgreSqlHealthCheck.ReadyTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(PostgreSqlHealthCheck.LiveTag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(PostgreSqlHealthCheck.ReadyTag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });

        // Backward compatibility for existing /health check (liveness)
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(PostgreSqlHealthCheck.LiveTag)
        });

        return endpoints;
    }
}
