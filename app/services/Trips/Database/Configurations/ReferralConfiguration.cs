using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trips.Domain;

namespace Trips.Database.Configurations;

public sealed class RiderAdmissionConfiguration : IEntityTypeConfiguration<RiderAdmission>
{
    public void Configure(EntityTypeBuilder<RiderAdmission> builder)
    {
        builder.ToTable("rider_admissions");
        builder.HasKey(x => x.RiderId);
        builder.Property(x => x.RiderId).HasColumnName("rider_id");
        builder.Property(x => x.FirstTripId).HasColumnName("first_trip_id");
        builder.HasIndex(x => x.FirstTripId).IsUnique().HasDatabaseName("ux_rider_admission_trip");
    }
}

public sealed class ReferralAccountConfiguration : IEntityTypeConfiguration<ReferralAccount>
{
    public void Configure(EntityTypeBuilder<ReferralAccount> builder)
    {
        builder.ToTable("referral_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.RiderId).HasColumnName("rider_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").IsRequired();
        builder.HasIndex(x => x.RiderId).IsUnique().HasDatabaseName("ux_referral_account_rider");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_referral_account_code");
    }
}

public sealed class ReferralAttributionConfiguration : IEntityTypeConfiguration<ReferralAttribution>
{
    public void Configure(EntityTypeBuilder<ReferralAttribution> builder)
    {
        builder.ToTable("referral_attributions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ReferredRiderId).HasColumnName("referred_rider_id").IsRequired();
        builder.Property(x => x.ReferrerRiderId).HasColumnName("referrer_rider_id").IsRequired();
        builder.Property(x => x.FirstTripId).HasColumnName("first_trip_id");
        builder.Property(x => x.QualificationCaptureId).HasColumnName("qualification_capture_id");
        builder.HasIndex(x => x.ReferredRiderId).IsUnique()
            .HasDatabaseName("ux_referral_attribution_referred_rider");
        builder.HasIndex(x => x.FirstTripId).IsUnique()
            .HasDatabaseName("ux_referral_attribution_first_trip");
    }
}

public sealed class ReferralCreditConfiguration : IEntityTypeConfiguration<ReferralCredit>
{
    public void Configure(EntityTypeBuilder<ReferralCredit> builder)
    {
        builder.ToTable("referral_credits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AttributionId).HasColumnName("attribution_id");
        builder.Property(x => x.BeneficiaryRiderId).HasColumnName("beneficiary_rider_id").IsRequired();
        builder.Property(x => x.AmountMinor).HasColumnName("amount_minor");
        builder.Property(x => x.Currency).HasColumnName("currency").IsRequired();
        builder.Property(x => x.ReservedTripId).HasColumnName("reserved_trip_id");
        builder.Property(x => x.UsedCaptureId).HasColumnName("used_capture_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.State).HasColumnName("state").HasConversion(
            state => StateName(state),
            name => ParseState(name));
        builder.HasIndex(x => new { x.AttributionId, x.BeneficiaryRiderId }).IsUnique()
            .HasDatabaseName("ux_referral_credit_source_beneficiary");
        builder.HasIndex(x => x.ReservedTripId).IsUnique()
            .HasFilter("reserved_trip_id IS NOT NULL")
            .HasDatabaseName("ux_referral_credit_reserved_trip");
        builder.HasOne(x => x.Attribution)
            .WithMany(x => x.Credits)
            .HasForeignKey(x => x.AttributionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string StateName(ReferralCreditState state) => state switch
    {
        ReferralCreditState.Available => "available",
        ReferralCreditState.Reserved => "reserved",
        ReferralCreditState.Used => "used",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static ReferralCreditState ParseState(string name) => name switch
    {
        "available" => ReferralCreditState.Available,
        "reserved" => ReferralCreditState.Reserved,
        "used" => ReferralCreditState.Used,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown referral credit state"),
    };
}

public sealed class PaymentEventInboxConfiguration : IEntityTypeConfiguration<PaymentEventInbox>
{
    public void Configure(EntityTypeBuilder<PaymentEventInbox> builder)
    {
        builder.ToTable("payment_event_inbox");
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(x => x.CaptureId).HasColumnName("capture_id");
        builder.Property(x => x.TripId).HasColumnName("trip_id");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.HasIndex(x => x.CaptureId).HasDatabaseName("ix_payment_event_inbox_capture");
    }
}
