using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trips.Domain;

namespace Trips.Database.Configurations;

public sealed class TripTransitionConfiguration : IEntityTypeConfiguration<TripTransition>
{
    public void Configure(EntityTypeBuilder<TripTransition> builder)
    {
        builder.ToTable("trip_transitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.TripId).HasColumnName("trip_id").IsRequired();
        builder.Property(t => t.Actor).HasColumnName("actor").IsRequired();
        builder.Property(t => t.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.Property(t => t.FromState)
            .HasColumnName("from_state")
            .IsRequired()
            .HasConversion(state => TripStateMachine.Name(state), name => TripStateMachine.Parse(name));

        builder.Property(t => t.ToState)
            .HasColumnName("to_state")
            .IsRequired()
            .HasConversion(state => TripStateMachine.Name(state), name => TripStateMachine.Parse(name));

        builder.HasIndex(t => new { t.TripId, t.Id }).HasDatabaseName("ix_trip_transitions_trip");
    }
}

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers");
        builder.HasKey(o => new { o.TripId, o.DriverId });

        builder.Property(o => o.TripId).HasColumnName("trip_id");
        builder.Property(o => o.DriverId).HasColumnName("driver_id");
        builder.Property(o => o.OfferedAt).HasColumnName("offered_at").IsRequired();
        builder.Property(o => o.ExpiresAt).HasColumnName("expires_at").IsRequired();

        builder.Property(o => o.State)
            .HasColumnName("state")
            .IsRequired()
            .HasConversion(state => OfferStateNames.Of(state), name => OfferStateNames.Parse(name));
    }
}

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.Available).HasColumnName("available").IsRequired();
        builder.Property(d => d.Near).HasColumnName("near").IsRequired();
        builder.Property(d => d.Display).HasColumnName("display").IsRequired();
        builder.Property(d => d.Vehicle).HasColumnName("vehicle").IsRequired();
        builder.Property(d => d.Position).HasColumnName("position");
    }
}
