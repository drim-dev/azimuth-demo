using Analytics.Database;
using Analytics.Features.TripActivity;
using Common.Exceptions;
using Common.Http;
using Common.Messaging;
using Common.Time;
using Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("analytics")
    ?? Environment.GetEnvironmentVariable("ANALYTICS_DB")
    ?? "Host=localhost;Port=5432;Database=analytics;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<AnalyticsDbContext>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<AnalyticsDbContext>();
builder.Services.AddSingleton(Clock.System);
builder.Services.AddSingleton(new RabbitMqAddress(
    builder.Configuration["RabbitMq:Uri"]
    ?? Environment.GetEnvironmentVariable("RABBITMQ_URI")
    ?? string.Empty));
builder.Services.AddHostedService<TripLifecycleConsumer>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.MapEndpoints<AnalyticsDbContext>();
app.Run();

public partial class Program;
