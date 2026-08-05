using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;
using Payments.Domain;

// The payments service. Slice 1 (D16.2): shares the Postgres instance with trip, with its own
// tables. Splitting the store is a slice-3 concern and changes no claim here.

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("payments")
    ?? Environment.GetEnvironmentVariable("PAYMENTS_DB")
    ?? "Host=localhost;Port=5432;Database=trip;Username=postgres;Password=postgres";

builder.Services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<PaymentsDbContext>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<PaymentsDbContext>();

builder.Services.AddSingleton(
    new IdFactory(generatorId: 1, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
builder.Services.AddSingleton(Clock.System);
builder.Services.AddSingleton<IPaymentProvider, AlwaysCaptures>();

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.MapEndpoints<PaymentsDbContext>();
app.Run();

/// <summary>Stands in for a provider until there is one. The seam it occupies is the point.</summary>
internal sealed class AlwaysCaptures : IPaymentProvider
{
    public Task<ProviderOutcome> CaptureAsync(long tripId, long amountMinor, string currency) =>
        Task.FromResult(ProviderOutcome.Captured);
}

/// <summary>Exposed so the component tests can host the real slices over a real store.</summary>
public partial class Program;
