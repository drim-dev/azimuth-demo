using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trips.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReferralRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "referral_credit_authority",
                table: "trips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "referral_credit_id",
                table: "trips",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referral_credit_authority",
                table: "trip_events",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_event_inbox",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_id = table.Column<long>(type: "bigint", nullable: false),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_event_inbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "referral_accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    rider_id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_attributions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    referred_rider_id = table.Column<string>(type: "text", nullable: false),
                    referrer_rider_id = table.Column<string>(type: "text", nullable: false),
                    first_trip_id = table.Column<long>(type: "bigint", nullable: false),
                    qualification_capture_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_attributions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rider_admissions",
                columns: table => new
                {
                    rider_id = table.Column<string>(type: "text", nullable: false),
                    first_trip_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rider_admissions", x => x.rider_id);
                });

            migrationBuilder.CreateTable(
                name: "referral_credits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    attribution_id = table.Column<long>(type: "bigint", nullable: false),
                    beneficiary_rider_id = table.Column<string>(type: "text", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    reserved_trip_id = table.Column<long>(type: "bigint", nullable: true),
                    used_capture_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_credits", x => x.id);
                    table.ForeignKey(
                        name: "FK_referral_credits_referral_attributions_attribution_id",
                        column: x => x.attribution_id,
                        principalTable: "referral_attributions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_event_inbox_capture",
                table: "payment_event_inbox",
                column: "capture_id");

            migrationBuilder.CreateIndex(
                name: "ux_referral_account_code",
                table: "referral_accounts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_referral_account_rider",
                table: "referral_accounts",
                column: "rider_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_referral_attribution_first_trip",
                table: "referral_attributions",
                column: "first_trip_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_referral_attribution_referred_rider",
                table: "referral_attributions",
                column: "referred_rider_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_referral_credit_reserved_trip",
                table: "referral_credits",
                column: "reserved_trip_id",
                unique: true,
                filter: "reserved_trip_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_referral_credit_source_beneficiary",
                table: "referral_credits",
                columns: new[] { "attribution_id", "beneficiary_rider_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rider_admission_trip",
                table: "rider_admissions",
                column: "first_trip_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_event_inbox");

            migrationBuilder.DropTable(
                name: "referral_accounts");

            migrationBuilder.DropTable(
                name: "referral_credits");

            migrationBuilder.DropTable(
                name: "rider_admissions");

            migrationBuilder.DropTable(
                name: "referral_attributions");

            migrationBuilder.DropColumn(
                name: "referral_credit_authority",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "referral_credit_id",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "referral_credit_authority",
                table: "trip_events");
        }
    }
}
