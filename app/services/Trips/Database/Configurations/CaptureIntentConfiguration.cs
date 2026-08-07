using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trips.Domain;

namespace Trips.Database.Configurations;

public sealed class CaptureIntentConfiguration : IEntityTypeConfiguration<CaptureIntent>
{
    public void Configure(EntityTypeBuilder<CaptureIntent> builder)
    {
        builder.ToTable("capture_intents");
        builder.HasKey(x => x.TripId);
        builder.Property(x => x.TripId).HasColumnName("trip_id").ValueGeneratedNever();
        builder.Property(x => x.QuoteToken).HasColumnName("quote_token").IsRequired();
        builder.Property(x => x.WrittenAt).HasColumnName("written_at").IsRequired();
        builder.Property(x => x.DispatchedAt).HasColumnName("dispatched_at");
    }
}
