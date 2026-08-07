using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Domain;

namespace Payments.Database.Configurations;

public sealed class CaptureIntentConfiguration : IEntityTypeConfiguration<CaptureIntent>
{
    public void Configure(EntityTypeBuilder<CaptureIntent> builder)
    {
        builder.ToTable("capture_intents");
        builder.HasKey(i => i.TripId);

        builder.Property(i => i.TripId).HasColumnName("trip_id").ValueGeneratedNever();
        builder.Property(i => i.QuoteToken).HasColumnName("quote_token").IsRequired();
        builder.Property(i => i.WrittenAt).HasColumnName("written_at").IsRequired();
        builder.Property(i => i.DispatchedAt).HasColumnName("dispatched_at");
    }
}

public sealed class CaptureConfiguration : IEntityTypeConfiguration<Capture>
{
    public void Configure(EntityTypeBuilder<Capture> builder)
    {
        builder.ToTable("captures");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.TripId).HasColumnName("trip_id").IsRequired();
        builder.Property(c => c.AmountMinor).HasColumnName("amount_minor").IsRequired();
        builder.Property(c => c.Currency).HasColumnName("currency").IsRequired();
        builder.Property(c => c.AdjustmentReason).HasColumnName("adjustment_reason");
        builder.Property(c => c.Voided).HasColumnName("voided").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.CapturedAt).HasColumnName("captured_at").IsRequired();

        // payments/capture#captured-once. Two workers processing the same completion both read "no
        // capture"; this is what actually holds the line, against paths that did not exist when it
        // was written. Partial, because a voided capture stays as a row so that a disputed trip's
        // history is legible — see the residue in design/payments/capture.md.
        builder.HasIndex(c => c.TripId)
            .IsUnique()
            .HasFilter("NOT voided")
            .HasDatabaseName("ux_capture_trip");
    }
}

public sealed class CaptureFailureConfiguration : IEntityTypeConfiguration<CaptureFailure>
{
    public void Configure(EntityTypeBuilder<CaptureFailure> builder)
    {
        builder.ToTable("capture_failures");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(f => f.TripId).HasColumnName("trip_id").IsRequired();
        builder.Property(f => f.Reason).HasColumnName("reason").IsRequired();
        builder.Property(f => f.OccurredAt).HasColumnName("occurred_at").IsRequired();
    }
}
