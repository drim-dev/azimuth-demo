using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trips.Domain;

namespace Trips.Database.Configurations;

public sealed class TripEventOutboxConfiguration : IEntityTypeConfiguration<TripEventOutbox>
{
    public void Configure(EntityTypeBuilder<TripEventOutbox> builder)
    {
        builder.ToTable("trip_events");
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(x => x.TripId).HasColumnName("trip_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.QuoteToken).HasColumnName("quote_token").IsRequired();
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.PublishedAt).HasColumnName("published_at");
        builder.Property(x => x.State)
            .HasColumnName("state")
            .IsRequired()
            .HasConversion(state => TripStateMachine.Name(state), name => TripStateMachine.Parse(name));

        builder.HasIndex(x => new { x.TripId, x.Version })
            .IsUnique()
            .HasDatabaseName("ux_trip_event_version");
        builder.HasIndex(x => new { x.PublishedAt, x.OccurredAt })
            .HasDatabaseName("ix_trip_events_pending");
    }
}
