using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trips.Domain;

namespace Trips.Database.Configurations;

public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.RiderId).HasColumnName("rider_id").IsRequired();
        builder.Property(t => t.AssignedDriverId).HasColumnName("assigned_driver_id");
        builder.Property(t => t.Version).HasColumnName("version").IsRequired();
        builder.Property(t => t.FareMinor).HasColumnName("fare_minor").IsRequired();
        builder.Property(t => t.Currency).HasColumnName("currency").IsRequired();
        builder.Property(t => t.QuoteId).HasColumnName("quote_id").IsRequired();
        builder.Property(t => t.QuoteToken).HasColumnName("quote_token").IsRequired();
        builder.Property(t => t.ReferralCreditId).HasColumnName("referral_credit_id");
        builder.Property(t => t.ReferralCreditAuthority).HasColumnName("referral_credit_authority");
        builder.Property(t => t.Pickup).HasColumnName("pickup").IsRequired();
        builder.Property(t => t.Dropoff).HasColumnName("dropoff").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        // Stored as the spec's own names rather than as an ordinal: the partial index below reads
        // the column, so a renumbering of the enum must not silently change which rows it covers.
        builder.Property(t => t.State)
            .HasColumnName("state")
            .IsRequired()
            .HasConversion(state => TripStateMachine.Name(state), name => TripStateMachine.Parse(name));

        builder.Ignore(t => t.Fare);

        builder.HasMany(t => t.Transitions)
            .WithOne()
            .HasForeignKey(t => t.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Offers)
            .WithOne()
            .HasForeignKey(o => o.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        // trips/request#second-request-rejected-while-active. Two requests arriving together both
        // read "no active trip"; this is what actually holds the line.
        //
        // Note the coupling flagged in design/trips/request.md: the predicate depends on the set of
        // terminal states, which trips/lifecycle owns. Adding a state and forgetting to classify it
        // here silently widens or narrows the rule, and nothing currently catches that.
        builder.HasIndex(t => t.RiderId)
            .IsUnique()
            .HasFilter("state NOT IN ('completed', 'cancelled')")
            .HasDatabaseName("ux_trip_rider_active");

        // A signed quote is spent at admission. The payload supplies stable identity; this index
        // settles concurrent consumers without requiring Trips to own Pricing's quote row.
        builder.HasIndex(t => t.QuoteId)
            .IsUnique()
            .HasDatabaseName("ux_trip_quote");
    }
}
