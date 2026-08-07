using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pricing;
using Pricing.Service.Database;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("pricing")
    ?? Environment.GetEnvironmentVariable("PRICING_DB")
    ?? "Host=localhost;Port=5432;Database=trip;Username=postgres;Password=postgres";
var signingKey =
    builder.Configuration["QuoteSigningKey"]
    ?? Environment.GetEnvironmentVariable("QUOTE_SIGNING_KEY")
    ?? "azimuth-demo-signing-key";

builder.Services.AddDbContext<PricingDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<PricingDbContext>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<PricingDbContext>();
builder.Services.AddSingleton(
    new IdFactory(generatorId: 2, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
builder.Services.AddSingleton(Clock.System);
builder.Services.AddSingleton(new QuoteTokenCodec(signingKey));
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<PricingDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.MapEndpoints<PricingDbContext>();
app.Run();

public partial class Program;
