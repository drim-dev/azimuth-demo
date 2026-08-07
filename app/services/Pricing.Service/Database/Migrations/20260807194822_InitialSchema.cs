using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pricing.Service.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "market_pressure",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    market = table.Column<string>(type: "text", nullable: false),
                    open_requests = table.Column<int>(type: "integer", nullable: false),
                    available_drivers = table.Column<int>(type: "integer", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_pressure", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_quotes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    pickup = table.Column<string>(type: "text", nullable: false),
                    dropoff = table.Column<string>(type: "text", nullable: false),
                    total_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_quotes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_market_pressure_market_observed_at",
                table: "market_pressure",
                columns: new[] { "market", "observed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_pressure");

            migrationBuilder.DropTable(
                name: "pricing_quotes");
        }
    }
}
