using Analytics.Domain;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Database;

public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<TripActivity> TripActivity => Set<TripActivity>();

    public DbSet<TripEventInbox> TripEventInbox => Set<TripEventInbox>();

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);
}
