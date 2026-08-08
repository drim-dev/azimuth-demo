using Microsoft.EntityFrameworkCore;
using Trips.Domain;

namespace Trips.Database;

public sealed class TripDbContext(DbContextOptions<TripDbContext> options) : DbContext(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<TripEventOutbox> TripEvents => Set<TripEventOutbox>();

    public DbSet<TripTransition> TripTransitions => Set<TripTransition>();

    public DbSet<Offer> Offers => Set<Offer>();

    public DbSet<Driver> Drivers => Set<Driver>();

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.ApplyConfigurationsFromAssembly(typeof(TripDbContext).Assembly);
}
