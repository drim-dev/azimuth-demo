using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analytics.Database.Migrations;

[DbContext(typeof(AnalyticsDbContext))]
[Migration("20260808040000_InitialSchema")]
public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "analytics_trip_event_inbox",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                trip_id = table.Column<long>(type: "bigint", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_analytics_trip_event_inbox", x => x.event_id));

        migrationBuilder.CreateTable(
            name: "trip_activity",
            columns: table => new
            {
                trip_id = table.Column<long>(type: "bigint", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                state = table.Column<string>(type: "text", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_trip_activity", x => x.trip_id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "analytics_trip_event_inbox");
        migrationBuilder.DropTable(name: "trip_activity");
    }
}
