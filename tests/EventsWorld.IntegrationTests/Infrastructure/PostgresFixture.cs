using System.Security.Cryptography;
using EventsWorld.Shared.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventsWorld.IntegrationTests.Infrastructure;

/// <summary>
/// A real PostgreSQL 17 in Docker, prepared like the Supabase database: the local Supabase
/// emulation, the bootstrap script from <c>db/bootstrap</c> and every module migration applied
/// as <c>ew_migrator</c>. Docker must be running.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("eventsworld")
        .Build();

    public string AdminConnectionString { get; private set; } = string.Empty;
    public string MigratorConnectionString { get; private set; } = string.Empty;
    public string AppConnectionString { get; private set; } = string.Empty;
    public ApiFactory Api { get; private set; } = null!;

    public static readonly string[] ModuleSchemas =
    [
        "accounts", "events", "registrations", "payments", "files", "notifications", "audit", "legal",
    ];

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        AdminConnectionString = _container.GetConnectionString();

        await ExecuteScriptAsync(SolutionPaths.File("db", "local", "00-supabase-emulation.sql"));
        await ExecuteScriptAsync(SolutionPaths.File("db", "bootstrap", "roles-and-privileges.sql"));

        MigratorConnectionString = await CreateLoginAsync("ew_migrator");
        AppConnectionString = await CreateLoginAsync("ew_app");

        Api = new ApiFactory()
            .WithSetting($"ConnectionStrings:{ConnectionStringNames.Database}", AppConnectionString)
            .WithSetting($"ConnectionStrings:{ConnectionStringNames.Migrations}", MigratorConnectionString);

        await Api.Services.MigrateModuleDatabasesAsync(MigratorConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (Api is not null)
        {
            await Api.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task ExecuteScriptAsync(string path)
    {
        await using var connection = await OpenAsync(AdminConnectionString);
        await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(path), connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Gives the role a random password (hex, safe to inline) and returns its connection string.</summary>
    private async Task<string> CreateLoginAsync(string role)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        await using (var connection = await OpenAsync(AdminConnectionString))
        await using (var command = new NpgsqlCommand($"ALTER ROLE {role} PASSWORD '{password}'", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = role,
            Password = password,
        }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseTestGroup : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Database";
}
