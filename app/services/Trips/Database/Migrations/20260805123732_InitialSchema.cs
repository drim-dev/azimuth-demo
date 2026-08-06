using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Trips.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false),
                    near = table.Column<string>(type: "text", nullable: false),
                    display = table.Column<string>(type: "text", nullable: false),
                    vehicle = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    pickup = table.Column<string>(type: "text", nullable: false),
                    dropoff = table.Column<string>(type: "text", nullable: false),
                    total_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_by_trip = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    rider_id = table.Column<string>(type: "text", nullable: false),
                    assigned_driver_id = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    fare_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    quote_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.id);
                    table.ForeignKey(
                        name: "FK_trips_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offers",
                columns: table => new
                {
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    driver_id = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offers", x => new { x.trip_id, x.driver_id });
                    table.ForeignKey(
                        name: "FK_offers_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trip_transitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    from_state = table.Column<string>(type: "text", nullable: false),
                    to_state = table.Column<string>(type: "text", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_transitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_trip_transitions_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_quote_consumer",
                table: "quotes",
                column: "consumed_by_trip",
                unique: true,
                filter: "consumed_by_trip IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trip_transitions_trip",
                table: "trip_transitions",
                columns: new[] { "trip_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_quote_id",
                table: "trips",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ux_trip_rider_active",
                table: "trips",
                column: "rider_id",
                unique: true,
                filter: "state NOT IN ('completed', 'cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "offers");

            migrationBuilder.DropTable(
                name: "trip_transitions");

            migrationBuilder.DropTable(
                name: "trips");

            migrationBuilder.DropTable(
                name: "quotes");
        }
    }
}
