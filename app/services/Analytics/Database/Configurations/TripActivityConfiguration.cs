using Analytics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Database.Configurations;

public sealed class TripActivityConfiguration : IEntityTypeConfiguration<TripActivity>
{
    public void Configure(EntityTypeBuilder<TripActivity> builder)
    {
        builder.ToTable("trip_activity");
        builder.HasKey(x => x.TripId);
        builder.Property(x => x.TripId).HasColumnName("trip_id").ValueGeneratedNever();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.State).HasColumnName("state").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
    }
}

public sealed class TripEventInboxConfiguration : IEntityTypeConfiguration<TripEventInbox>
{
    public void Configure(EntityTypeBuilder<TripEventInbox> builder)
    {
        builder.ToTable("analytics_trip_event_inbox");
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(x => x.TripId).HasColumnName("trip_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
    }
}
