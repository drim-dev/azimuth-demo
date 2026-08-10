using Microsoft.EntityFrameworkCore;
using Payments.Domain;

namespace Payments.Database;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<CaptureIntent> CaptureIntents => Set<CaptureIntent>();

    public DbSet<Capture> Captures => Set<Capture>();

    public DbSet<CaptureFailure> CaptureFailures => Set<CaptureFailure>();

    public DbSet<PaymentEventOutbox> PaymentEvents => Set<PaymentEventOutbox>();

    public DbSet<TripEventInbox> TripEventInbox => Set<TripEventInbox>();

    public DbSet<TripEventCursor> TripEventCursors => Set<TripEventCursor>();

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
}
