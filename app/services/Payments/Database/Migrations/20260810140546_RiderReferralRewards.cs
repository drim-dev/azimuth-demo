using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.Database.Migrations
{
    /// <inheritdoc />
    public partial class RiderReferralRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "original_fare_minor",
                table: "captures",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "referral_credit_id",
                table: "captures",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "referral_credit_minor",
                table: "captures",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("UPDATE captures SET original_fare_minor = amount_minor;");

            migrationBuilder.AddColumn<string>(
                name: "referral_credit_authority",
                table: "capture_intents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_id = table.Column<long>(type: "bigint", nullable: false),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    original_fare_minor = table.Column<long>(type: "bigint", nullable: false),
                    referral_credit_minor = table.Column<long>(type: "bigint", nullable: false),
                    captured_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    referral_credit_id = table.Column<long>(type: "bigint", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_events", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_capture_referral_credit",
                table: "captures",
                column: "referral_credit_id",
                unique: true,
                filter: "referral_credit_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_events_pending",
                table: "payment_events",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_event_capture",
                table: "payment_events",
                column: "capture_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_events");

            migrationBuilder.DropIndex(
                name: "ux_capture_referral_credit",
                table: "captures");

            migrationBuilder.DropColumn(
                name: "original_fare_minor",
                table: "captures");

            migrationBuilder.DropColumn(
                name: "referral_credit_id",
                table: "captures");

            migrationBuilder.DropColumn(
                name: "referral_credit_minor",
                table: "captures");

            migrationBuilder.DropColumn(
                name: "referral_credit_authority",
                table: "capture_intents");
        }
    }
}
