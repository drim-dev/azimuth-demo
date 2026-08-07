using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pricing.Service.Domain;

namespace Pricing.Service.Database.Configurations;

public sealed class MarketPressureConfiguration : IEntityTypeConfiguration<MarketPressure>
{
    public void Configure(EntityTypeBuilder<MarketPressure> builder)
    {
        builder.ToTable("market_pressure");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Market).HasColumnName("market").IsRequired();
        builder.Property(x => x.OpenRequests).HasColumnName("open_requests").IsRequired();
        builder.Property(x => x.AvailableDrivers).HasColumnName("available_drivers").IsRequired();
        builder.Property(x => x.ObservedAt).HasColumnName("observed_at").IsRequired();
        builder.HasIndex(x => new { x.Market, x.ObservedAt });
    }
}

public sealed class IssuedQuoteConfiguration : IEntityTypeConfiguration<IssuedQuote>
{
    public void Configure(EntityTypeBuilder<IssuedQuote> builder)
    {
        builder.ToTable("pricing_quotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Pickup).HasColumnName("pickup").IsRequired();
        builder.Property(x => x.Dropoff).HasColumnName("dropoff").IsRequired();
        builder.Property(x => x.TotalMinor).HasColumnName("total_minor").IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").IsRequired();
        builder.Property(x => x.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.Token).HasColumnName("token").IsRequired();
    }
}
