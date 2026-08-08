using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Analytics.Database.Migrations;

[DbContext(typeof(AnalyticsDbContext))]
partial class AnalyticsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Analytics.Domain.TripActivity", b =>
        {
            b.Property<long>("TripId").HasColumnType("bigint").HasColumnName("trip_id");
            b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone")
                .HasColumnName("occurred_at");
            b.Property<string>("State").IsRequired().HasColumnType("text").HasColumnName("state");
            b.Property<long>("Version").HasColumnType("bigint").HasColumnName("version");
            b.HasKey("TripId");
            b.ToTable("trip_activity");
        });

        modelBuilder.Entity("Analytics.Domain.TripEventInbox", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("uuid").HasColumnName("event_id");
            b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone")
                .HasColumnName("received_at");
            b.Property<long>("TripId").HasColumnType("bigint").HasColumnName("trip_id");
            b.Property<long>("Version").HasColumnType("bigint").HasColumnName("version");
            b.HasKey("EventId");
            b.ToTable("analytics_trip_event_inbox");
        });
#pragma warning restore 612, 618
    }
}
