using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Domain;

namespace Payments.Database.Configurations;

public sealed class TripEventInboxConfiguration : IEntityTypeConfiguration<TripEventInbox>
{
    public void Configure(EntityTypeBuilder<TripEventInbox> builder)
    {
        builder.ToTable("payment_trip_event_inbox");
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(x => x.TripId).HasColumnName("trip_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.State).HasColumnName("state").IsRequired();
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
    }
}

public sealed class TripEventCursorConfiguration : IEntityTypeConfiguration<TripEventCursor>
{
    public void Configure(EntityTypeBuilder<TripEventCursor> builder)
    {
        builder.ToTable("payment_trip_event_cursors");
        builder.HasKey(x => x.TripId);
        builder.Property(x => x.TripId).HasColumnName("trip_id").ValueGeneratedNever();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
    }
}
