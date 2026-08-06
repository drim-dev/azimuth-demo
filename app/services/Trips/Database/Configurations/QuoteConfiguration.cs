using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trips.Domain;

namespace Trips.Database.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(q => q.Pickup).HasColumnName("pickup").IsRequired();
        builder.Property(q => q.Dropoff).HasColumnName("dropoff").IsRequired();
        builder.Property(q => q.TotalMinor).HasColumnName("total_minor").IsRequired();
        builder.Property(q => q.Currency).HasColumnName("currency").IsRequired();
        builder.Property(q => q.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(q => q.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(q => q.ConsumedByTripId).HasColumnName("consumed_by_trip");

        builder.Ignore(q => q.Total);

        // pricing/quote#quote-consumed-once, and trips/request#quote-consumed-once: a quote is spent
        // by at most one admitted request. The application-level check beside it is a courtesy that
        // makes the common path cheap and the error message good; this is the enforcement.
        builder.HasIndex(q => q.ConsumedByTripId)
            .IsUnique()
            .HasFilter("consumed_by_trip IS NOT NULL")
            .HasDatabaseName("ux_quote_consumer");
    }
}
