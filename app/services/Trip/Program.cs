using Trip;
using Trip.Domain;
using Trip.Storage;

// The trip service. Slice 1 (D16.2): pricing lives here as a module and is split out in slice 3.

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("trip")
    ?? Environment.GetEnvironmentVariable("TRIP_DB")
    ?? "Host=localhost;Port=5432;Database=trip;Username=postgres;Password=postgres";

builder.Services.AddSingleton(new Database(connectionString));
builder.Services.AddSingleton<QuoteStore>();
builder.Services.AddSingleton<TripStore>();
builder.Services.AddSingleton<OfferStore>();
builder.Services.AddSingleton<Clock>(_ => new Clock(() => DateTimeOffset.UtcNow));

var app = builder.Build();
await app.Services.GetRequiredService<Database>().MigrateAsync();
Routes.Map(app);
app.Run();

/// <summary>Injected so tests can settle time without waiting for it.</summary>
public sealed class Clock(Func<DateTimeOffset> now)
{
    public DateTimeOffset Now => now();
}

/// <summary>Exposed so the component tests can host the real routes over a real store.</summary>
public partial class Program;
