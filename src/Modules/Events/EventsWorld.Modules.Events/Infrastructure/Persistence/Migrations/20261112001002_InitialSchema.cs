using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsWorld.Modules.Events.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates the <c>events</c> schema with its grants: data access for ew_app, nothing for the
    /// Supabase Data API roles (TS-003, database.md §5–6).
    /// </summary>
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureExtension("pg_trgm");
            migrationBuilder.SetUpModuleSchema(SchemaName);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schemas and extensions are never dropped by a migration: they may hold data.
        }

        private const string SchemaName = "events";
    }
}
