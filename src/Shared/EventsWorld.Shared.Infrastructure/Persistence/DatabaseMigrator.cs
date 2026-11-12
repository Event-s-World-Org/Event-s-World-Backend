using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventsWorld.Shared.Infrastructure.Persistence;

/// <summary>
/// Applies the pending migrations of every module, in registration order, as <c>ew_migrator</c>
/// through the <c>Migrations</c> connection (direct or session mode). Used by the API's
/// <c>migrate</c> command in the deployment pipeline and by the integration tests.
/// </summary>
public static partial class DatabaseMigrator
{
    public static async Task MigrateModuleDatabasesAsync(
        this IServiceProvider services,
        string migrationsConnectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsConnectionString);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator));

        foreach (var module in services.GetServices<ModuleDatabase>())
        {
            await using var scope = services.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);

            context.Database.SetConnectionString(migrationsConnectionString);

            // EF Core reads the history table optimistically and logs an error when it does not
            // exist yet (first deployment). Creating it first keeps the deployment log clean.
            await context.GetService<IHistoryRepository>().CreateIfNotExistsAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);

            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Count();
            LogMigrated(logger, module.Schema, applied);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Schema {Schema} is up to date ({Count} migration(s) applied).")]
    private static partial void LogMigrated(ILogger logger, string schema, int count);
}
