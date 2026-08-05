using Payments.Domain;

// The payments service. Slice 1 (D16.2): shares the Postgres instance with trip, with its own
// tables. Splitting the store is a slice-3 concern and changes no claim here.

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("payments")
    ?? Environment.GetEnvironmentVariable("PAYMENTS_DB")
    ?? "Host=localhost;Port=5432;Database=trip;Username=postgres;Password=postgres";

builder.Services.AddSingleton(new Database(connectionString));
builder.Services.AddSingleton<IPaymentProvider, AlwaysCaptures>();
builder.Services.AddSingleton<CaptureDispatcher>();

var app = builder.Build();
await app.Services.GetRequiredService<Database>().MigrateAsync();

app.MapPost("/dispatch", async (CaptureDispatcher dispatcher) =>
    Results.Ok(new { captured = await dispatcher.DispatchAsync(DateTimeOffset.UtcNow) }));

app.MapGet("/captures/{tripId:guid}", async (Guid tripId, CaptureDispatcher dispatcher) =>
    await dispatcher.FindAsync(tripId) is { } capture
        ? Results.Ok(new { capture.Id, capture.AmountMinor, capture.Currency, capture.AdjustmentReason })
        : Results.NotFound());

app.Run();

/// <summary>Stands in for a provider until there is one. The seam it occupies is the point.</summary>
internal sealed class AlwaysCaptures : IPaymentProvider
{
    public Task<ProviderOutcome> CaptureAsync(Guid tripId, long amountMinor, string currency) =>
        Task.FromResult(ProviderOutcome.Captured);
}
