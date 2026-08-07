using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pricing;
using Trips.Database;

// The trip service. Slice 1 (D16.2): pricing lives here as a module and is split out in slice 3.

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("trip")
    ?? Environment.GetEnvironmentVariable("TRIP_DB")
    ?? "Host=localhost;Port=5432;Database=trip;Username=postgres;Password=postgres";

builder.Services.AddDbContext<TripDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<TripDbContext>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<TripDbContext>();

builder.Services.AddSingleton(
    new IdFactory(generatorId: 0, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
builder.Services.AddSingleton(Clock.System);
builder.Services.AddSingleton(new QuoteTokenCodec(
    builder.Configuration["QuoteSigningKey"]
    ?? Environment.GetEnvironmentVariable("QUOTE_SIGNING_KEY")
    ?? "azimuth-demo-signing-key"));

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<TripDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.MapEndpoints<TripDbContext>();
app.Run();

/// <summary>Exposed so the component tests can host the real slices over a real store.</summary>
public partial class Program;
