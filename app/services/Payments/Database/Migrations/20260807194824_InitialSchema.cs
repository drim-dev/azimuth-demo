using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Payments.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capture_failures",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capture_failures", x => x.id);
                });

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS capture_intents (
                    trip_id bigint PRIMARY KEY,
                    quote_token text NOT NULL,
                    payment_method text NOT NULL,
                    written_at timestamp with time zone NOT NULL,
                    dispatched_at timestamp with time zone NULL
                );
                """);

            migrationBuilder.CreateTable(
                name: "payment_trip_event_cursors",
                columns: table => new
                {
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_payment_trip_event_cursors", x => x.trip_id));

            migrationBuilder.CreateTable(
                name: "payment_trip_event_inbox",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_payment_trip_event_inbox", x => x.event_id));

            migrationBuilder.CreateTable(
                name: "captures",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    adjustment_reason = table.Column<string>(type: "text", nullable: true),
                    voided = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_captures", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_capture_trip",
                table: "captures",
                column: "trip_id",
                unique: true,
                filter: "NOT voided");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capture_failures");

            migrationBuilder.DropTable(
                name: "captures");

            migrationBuilder.DropTable(
                name: "payment_trip_event_cursors");

            migrationBuilder.DropTable(
                name: "payment_trip_event_inbox");
        }
    }
}
