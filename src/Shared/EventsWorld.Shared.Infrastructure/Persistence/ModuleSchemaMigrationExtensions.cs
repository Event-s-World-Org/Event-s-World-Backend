using Microsoft.EntityFrameworkCore.Migrations;

namespace EventsWorld.Shared.Infrastructure.Persistence;

/// <summary>Raw SQL every module needs in its first migration (ADR-0002, database.md §5–6).</summary>
public static class ModuleSchemaMigrationExtensions
{
    /// <summary>
    /// Creates the module schema (owned by <c>ew_migrator</c>, who runs the migrations), gives
    /// <c>ew_app</c> data access to every future table and sequence in it, and removes any access
    /// from the Supabase Data API roles <c>anon</c> and <c>authenticated</c>.
    /// </summary>
    public static void SetUpModuleSchema(this MigrationBuilder migrationBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        SqlIdentifier.EnsureValid(schema);

        migrationBuilder.EnsureSchema(schema);
        migrationBuilder.Sql($"REVOKE ALL ON SCHEMA {schema} FROM PUBLIC;");
        migrationBuilder.Sql($"GRANT USAGE ON SCHEMA {schema} TO ew_app;");
        migrationBuilder.Sql(
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ew_app;");
        migrationBuilder.Sql(
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} GRANT USAGE, SELECT ON SEQUENCES TO ew_app;");

        // anon and authenticated only exist on Supabase (and in the local emulation).
        migrationBuilder.Sql($"""
            DO $$
            DECLARE
                api_role text;
            BEGIN
                FOREACH api_role IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = api_role) THEN
                        EXECUTE format('REVOKE ALL ON SCHEMA {schema} FROM %I', api_role);
                        EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA {schema} FROM %I', api_role);
                        EXECUTE format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA {schema} FROM %I', api_role);
                        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} REVOKE ALL ON TABLES FROM %I', api_role);
                    END IF;
                END LOOP;
            END
            $$;
            """);
    }

    /// <summary>
    /// Installs a trusted extension in the <c>extensions</c> schema, where Supabase keeps them
    /// (database.md §6: <c>citext</c>, <c>pg_trgm</c>).
    /// </summary>
    public static void EnsureExtension(this MigrationBuilder migrationBuilder, string extension)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        SqlIdentifier.EnsureValid(extension);

        migrationBuilder.Sql($"CREATE EXTENSION IF NOT EXISTS {extension} WITH SCHEMA extensions;");
    }
}
