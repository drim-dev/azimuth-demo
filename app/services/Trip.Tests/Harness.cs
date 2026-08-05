using Common.Identity;
using Common.Time;
using Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Trips.Database;
using Trips.Domain;
using Xunit;

namespace Trips.Tests;

/// <summary>
/// A real Postgres, because that is what <c>component</c> means (D15): real persistence and real
/// serialization, with external services substituted.
/// </summary>
/// <remarks>
/// Defined that way the rung is partly machine-checkable rather than purely self-declared — a
/// harness knows whether it started a database, so these claims cannot be quietly satisfied by an
/// in-memory fake. Every claim below is one whose truth is settled by a storage constraint, and
/// against a fake each of them would pass against an implementation that has no constraint at all.
/// </remarks>
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
        services.AddDbContext<TripDbContext>(o => o.UseNpgsql(ConnectionString));
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<TripDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>
/// The real slices over a real store, reached the way a request reaches them.
/// </summary>
/// <remarks>
/// Every send gets its own scope, so a test that sends concurrently exercises the same
/// context-per-request the service has. Sharing one <see cref="TripDbContext"/> across parallel
/// sends would fail on the change tracker before it ever reached the constraint the claim is about.
/// </remarks>
internal sealed class TripHarness : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public TripHarness(string connectionString, DateTimeOffset now)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TripDbContext>(o => o.UseNpgsql(connectionString));
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<TripDbContext>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<TripDbContext>();
        services.AddSingleton(new IdFactory(0, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        services.AddSingleton(Clock.Fixed(now));
        services.AddLogging();

        _services = services.BuildServiceProvider();
    }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Sends and reports the domain's refusal code rather than throwing, for the racing tests.</summary>
    public async Task<(bool Ok, string? ErrorCode)> TrySendAsync<TResponse>(IRequest<TResponse> request)
    {
        try
        {
            await SendAsync(request);
            return (true, null);
        }
        catch (Common.Exceptions.DomainException e)
        {
            return (false, e.ErrorCode);
        }
    }

    public async Task SeedDriversAsync(int available, int unavailable)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();

        for (var i = 0; i < available + unavailable; i++)
        {
            var id = $"driver-{i}";
            var existing = await db.Drivers.FirstOrDefaultAsync(d => d.Id == id);
            if (existing is null)
            {
                db.Drivers.Add(new Driver
                {
                    Id = id,
                    Available = i < available,
                    Near = "downtown",
                    Display = "Sam",
                    Vehicle = "blue hatchback",
                    Position = "52.37,4.89",
                });
            }
            else
            {
                existing.Available = i < available;
                existing.Near = "downtown";
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Withdraws every driver from supply, so a fan-out has nobody to reach.</summary>
    public async Task WithdrawAllDriversAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TripDbContext>()
            .Drivers
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.Available, false));
    }

    public async Task<TripState?> StateAsync(long tripId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
        var states = await db.Trips.AsNoTracking()
            .Where(t => t.Id == tripId)
            .Select(t => (TripState?)t.State)
            .ToListAsync();

        return states.FirstOrDefault();
    }

    public async Task<string?> AssignedDriverAsync(long tripId)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TripDbContext>()
            .Trips.AsNoTracking()
            .Where(t => t.Id == tripId)
            .Select(t => t.AssignedDriverId)
            .FirstOrDefaultAsync();
    }

    public async Task<long> FareAsync(long tripId)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TripDbContext>()
            .Trips.AsNoTracking()
            .Where(t => t.Id == tripId)
            .Select(t => t.FareMinor)
            .FirstAsync();
    }

    public async Task<IReadOnlyList<(string From, string To, string Actor)>> HistoryAsync(long tripId)
    {
        await using var scope = _services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<TripDbContext>()
            .TripTransitions.AsNoTracking()
            .Where(t => t.TripId == tripId)
            .OrderBy(t => t.Id)
            .Select(t => new { t.FromState, t.ToState, t.Actor })
            .ToListAsync();

        return rows
            .Select(r => (TripStateMachine.Name(r.FromState), TripStateMachine.Name(r.ToState), r.Actor))
            .ToList();
    }

    public async Task<DateTimeOffset> QuoteExpiryAsync(long quoteId)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TripDbContext>()
            .Quotes.AsNoTracking()
            .Where(q => q.Id == quoteId)
            .Select(q => q.ExpiresAt)
            .FirstAsync();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
