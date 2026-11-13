using System;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsWorld.Modules.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxAndEmailLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureExtension("citext");

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_log",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_email = table.Column<string>(type: "citext", nullable: false),
                    template = table.Column<string>(type: "text", nullable: false),
                    provider_message_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_log_outbox_outbox_id",
                        column: x => x.outbox_id,
                        principalSchema: "notifications",
                        principalTable: "outbox",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_created_at",
                schema: "notifications",
                table: "outbox",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_processed_at_next_attempt_at",
                schema: "notifications",
                table: "outbox",
                columns: ProcessedAtNextAttemptAtColumns);

            migrationBuilder.CreateIndex(
                name: "ix_email_log_outbox_id",
                schema: "notifications",
                table: "email_log",
                column: "outbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_log_to_email",
                schema: "notifications",
                table: "email_log",
                column: "to_email");

            migrationBuilder.CreateIndex(
                name: "ix_email_log_sent_at",
                schema: "notifications",
                table: "email_log",
                column: "sent_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_log",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "notifications");
        }

        private static readonly string[] ProcessedAtNextAttemptAtColumns = ["processed_at", "next_attempt_at"];
    }
}
