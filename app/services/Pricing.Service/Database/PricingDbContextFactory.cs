using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pricing.Service.Database;

public sealed class PricingDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
{
    public PricingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseNpgsql("Host=localhost;Database=trip;Username=postgres;Password=postgres")
            .Options;
        return new PricingDbContext(options);
    }
}
