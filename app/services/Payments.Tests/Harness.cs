using Common.Identity;
using Common.Time;
using Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Database;
using Payments.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace Payments.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var services = new ServiceCollection();
        services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(ConnectionString));
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>Records what the provider was asked, and answers as the test dictates.</summary>
internal sealed class ScriptedProvider(params ProviderOutcome[] outcomes) : IPaymentProvider
{
    private int _calls;

    public int Calls => _calls;

    public Task<ProviderOutcome> CaptureAsync(long tripId, long amountMinor, string currency)
    {
        var index = Interlocked.Increment(ref _calls) - 1;
        return Task.FromResult(index < outcomes.Length ? outcomes[index] : outcomes[^1]);
    }
}

/// <summary>
/// The real slices over a real store, reached the way a request reaches them.
/// </summary>
/// <remarks>
/// Every send gets its own scope, so a test that sends concurrently exercises the same
/// context-per-request the service has. Sharing one <see cref="PaymentsDbContext"/> across parallel
/// sends would fail on the change tracker before it ever reached the index the claim is about.
/// </remarks>
internal sealed class PaymentsHarness : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public PaymentsHarness(string connectionString, IPaymentProvider provider, DateTimeOffset now)
    {
        var services = new ServiceCollection();
        services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(connectionString));
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<PaymentsDbContext>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<PaymentsDbContext>();
        services.AddSingleton(new IdFactory(1, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        services.AddSingleton(Clock.Fixed(now));
        services.AddSingleton(provider);
        services.AddLogging();

        _services = services.BuildServiceProvider();
    }

    public IdFactory Ids => _services.GetRequiredService<IdFactory>();

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task SendAsync(IRequest request)
    {
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<int> CountCapturesAsync(long tripId)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Captures
            .AsNoTracking()
            .CountAsync(c => c.TripId == tripId && !c.Voided);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
