using Microsoft.EntityFrameworkCore;
using Pricing.Service.Domain;

namespace Pricing.Service.Database;

public sealed class PricingDbContext(DbContextOptions<PricingDbContext> options) : DbContext(options)
{
    public DbSet<MarketPressure> MarketPressures => Set<MarketPressure>();
    public DbSet<IssuedQuote> Quotes => Set<IssuedQuote>();

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.ApplyConfigurationsFromAssembly(typeof(PricingDbContext).Assembly);
}
