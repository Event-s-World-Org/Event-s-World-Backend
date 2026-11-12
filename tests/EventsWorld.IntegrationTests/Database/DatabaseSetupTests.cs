using EventsWorld.IntegrationTests.Infrastructure;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EventsWorld.IntegrationTests.Database;

/// <summary>Acceptance criteria of TS-003 (ADR-0002, database.md §5–6).</summary>
[Collection(DatabaseTestGroup.Name)]
public sealed class DatabaseSetupTests(PostgresFixture database)
{
    private const string InsufficientPrivilege = "42501";

    [Fact]
    public async Task Migrations_CreateEveryModuleSchema_OwnedByTheMigrator()
    {
        var owners = await QueryAsync(
            database.AdminConnectionString,
            """
            SELECT n.nspname, r.rolname
            FROM pg_namespace n JOIN pg_roles r ON r.oid = n.nspowner
            WHERE n.nspname = ANY(@schemas)
            """,
            reader => (Schema: reader.GetString(0), Owner: reader.GetString(1)),
            new NpgsqlParameter("schemas", PostgresFixture.ModuleSchemas));

        Assert.Equal(PostgresFixture.ModuleSchemas.Order(), owners.Select(o => o.Schema).Order());
        Assert.All(owners, owner => Assert.Equal("ew_migrator", owner.Owner));
    }

    [Fact]
    public async Task EveryModule_KeepsItsMigrationHistoryInsideItsOwnSchema()
    {
        foreach (var schema in PostgresFixture.ModuleSchemas)
        {
            var applied = await ScalarAsync<long>(
                database.AdminConnectionString,
                $"SELECT count(*) FROM {schema}.\"__EFMigrationsHistory\"");

            Assert.True(applied >= 1, $"Schema '{schema}' has no applied migrations.");
        }
    }

    [Fact]
    public async Task Migrations_CanRunAgain_WithoutChanges()
    {
        var exception = await Record.ExceptionAsync(
            () => database.Api.Services.MigrateModuleDatabasesAsync(database.MigratorConnectionString));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublicSchema_StaysEmpty()
    {
        var tables = await ScalarAsync<long>(
            database.AdminConnectionString,
            "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public'");

        Assert.Equal(0, tables);
    }

    [Theory]
    [InlineData("citext")]
    [InlineData("pg_trgm")]
    public async Task Extensions_AreInstalledInTheExtensionsSchema(string extension)
    {
        var schema = await ScalarAsync<string>(
            database.AdminConnectionString,
            "SELECT n.nspname FROM pg_extension e JOIN pg_namespace n ON n.oid = e.extnamespace WHERE e.extname = @extension",
            new NpgsqlParameter("extension", extension));

        Assert.Equal("extensions", schema);
    }

    [Theory]
    [InlineData("anon")]
    [InlineData("authenticated")]
    public async Task SupabaseDataApiRoles_CannotReachAnyModuleTable(string apiRole)
    {
        await using var probes = await ProbeTables.CreateAsync(database);

        foreach (var table in probes.Names)
        {
            var schema = table.Split('.')[0];
            var canUseSchema = await ScalarAsync<bool>(
                database.AdminConnectionString,
                "SELECT has_schema_privilege(@role, @schema, 'USAGE')",
                new NpgsqlParameter("role", apiRole),
                new NpgsqlParameter("schema", schema));
            var canRead = await ScalarAsync<bool>(
                database.AdminConnectionString,
                "SELECT has_table_privilege(@role, @table, 'SELECT')",
                new NpgsqlParameter("role", apiRole),
                new NpgsqlParameter("table", table));

            Assert.False(canUseSchema, $"{apiRole} can use schema {schema}.");
            Assert.False(canRead, $"{apiRole} can read {table}.");
        }

        // And a real query fails, as it would through the Supabase Data API.
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            database.AdminConnectionString,
            $"SET ROLE {apiRole}; SELECT * FROM {probes.Names[0]};"));
        Assert.Equal(InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task AppRole_CanReadAndWriteModuleTables()
    {
        await using var probes = await ProbeTables.CreateAsync(database);

        foreach (var table in probes.Names)
        {
            await ExecuteAsync(database.AppConnectionString, $"INSERT INTO {table} (id) VALUES (1)");
            await ExecuteAsync(database.AppConnectionString, $"UPDATE {table} SET id = 2");
            Assert.Equal(1, await ScalarAsync<long>(database.AppConnectionString, $"SELECT count(*) FROM {table}"));
            await ExecuteAsync(database.AppConnectionString, $"DELETE FROM {table}");
        }
    }

    [Fact]
    public async Task AppRole_CannotRunDdl()
    {
        await using var probes = await ProbeTables.CreateAsync(database);
        var table = probes.Names[0];
        var schema = table.Split('.')[0];

        string[] statements =
        [
            $"CREATE TABLE {schema}.created_by_app (id int)",
            $"ALTER TABLE {table} ADD COLUMN extra int",
            $"DROP TABLE {table}",
            $"TRUNCATE {table}",
            "CREATE TABLE public.created_by_app (id int)",
            "CREATE SCHEMA created_by_app",
        ];

        foreach (var statement in statements)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(database.AppConnectionString, statement));
            Assert.Equal(InsufficientPrivilege, error.SqlState);
        }
    }

    [Fact]
    public void AppConnection_IsConfiguredForTheTransactionPooler()
    {
        using var scope = database.Api.Services.CreateScope();

        foreach (var module in scope.ServiceProvider.GetServices<ModuleDatabase>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);
            var connection = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());

            Assert.Equal("ew_app", connection.Username);
            Assert.True(connection.NoResetOnClose);
            Assert.Equal(0, connection.MaxAutoPrepare);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<List<T>> QueryAsync<T>(
        string connectionString,
        string sql,
        Func<NpgsqlDataReader, T> map,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<T>();
        while (await reader.ReadAsync())
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    /// <summary>
    /// One throwaway table per module schema, created by ew_migrator exactly as a migration would,
    /// so the default privileges are checked against real tables.
    /// </summary>
    private sealed class ProbeTables : IAsyncDisposable
    {
        private readonly string _migratorConnectionString;

        private ProbeTables(string migratorConnectionString, List<string> names)
        {
            _migratorConnectionString = migratorConnectionString;
            Names = names;
        }

        public List<string> Names { get; }

        public static async Task<ProbeTables> CreateAsync(PostgresFixture database)
        {
            var suffix = Guid.CreateVersion7().ToString("N")[^8..];
            var names = PostgresFixture.ModuleSchemas.Select(schema => $"{schema}.probe_{suffix}").ToList();

            foreach (var name in names)
            {
                await ExecuteAsync(database.MigratorConnectionString, $"CREATE TABLE {name} (id int)");
            }

            return new ProbeTables(database.MigratorConnectionString, names);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var name in Names)
            {
                await ExecuteAsync(_migratorConnectionString, $"DROP TABLE IF EXISTS {name}");
            }
        }
    }
}
