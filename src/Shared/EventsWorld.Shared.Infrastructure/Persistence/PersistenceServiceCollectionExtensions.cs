using EventsWorld.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace EventsWorld.Shared.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module DbContext on PostgreSQL (ADR-0002, database.md §1 and §6):
    /// snake_case names, the module's own schema and its own migrations history table,
    /// and Npgsql settings for the Supabase pooler in transaction mode.
    /// The <c>Database</c> connection string is read when the context is created, so the final
    /// configuration (user secrets, environment variables, test overrides) always applies.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string schema)
        where TContext : DbContext
    {
        SqlIdentifier.EnsureValid(schema);

        services.AddDbContext<TContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringNames.Database);

            void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
                npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema);

            // Without a connection string (design time: `dotnet ef migrations add`) the context
            // can still be built; any database call then fails with a clear Npgsql error.
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql(ConfigureNpgsql);
            }
            else
            {
                options.UseNpgsql(ForTransactionPooler(connectionString), ConfigureNpgsql);
            }

            options.UseSnakeCaseNamingConvention();
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddSingleton(new ModuleDatabase(typeof(TContext), schema));
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    /// <summary>
    /// The pooler in transaction mode hands each transaction to a different server connection:
    /// connection state must not be reset or kept, and server-side prepared statements are off.
    /// </summary>
    internal static string ForTransactionPooler(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            NoResetOnClose = true,
            MaxAutoPrepare = 0,
        }.ConnectionString;
}
