using System.Collections;
using System.Linq.Expressions;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Common.Testing;

/// <summary>
/// A real Postgres, because that is what <c>component</c> means (D15): real persistence and real
/// serialization, with external services substituted.
/// </summary>
/// <remarks>
/// Defined that way the rung is partly machine-checkable rather than purely self-declared — a
/// harness knows whether it started a database, so a claim settled by a storage constraint cannot
/// be quietly satisfied by an in-memory fake. Against a fake, every claim that rests on a partial
/// unique index would pass against an implementation that has no index at all.
/// </remarks>
public sealed class DatabaseHarness<TProgram, TDbContext>(string databaseResourceName) : IHarness<TProgram>
    where TProgram : class
    where TDbContext : DbContext
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<TProgram>? _factory;
    private Respawner? _respawner;
    private bool _started;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder) =>
        builder.UseSetting($"ConnectionStrings:{databaseResourceName}", _postgres!.GetConnectionString());

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellation)
    {
        _factory = factory;

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await _postgres.StartAsync(cancellation);
        _started = true;
    }

    public async Task Stop(CancellationToken cancellation)
    {
        if (_postgres is not null)
        {
            await _postgres.StopAsync(cancellation);
            await _postgres.DisposeAsync();
        }

        _started = false;
    }

    public async Task Save(params object[] entities)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        foreach (var collection in entities.OfType<IEnumerable>())
        {
            db.AddRange(collection);
        }

        db.AddRange(entities.Where(e => e is not IEnumerable));
        await db.SaveChangesAsync();
    }

    public async Task Execute(Func<TDbContext, Task> action)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TDbContext>());
    }

    public async Task<T> Execute<T>(Func<TDbContext, Task<T>> action)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TDbContext>());
    }

    public async Task<T?> SingleOrDefault<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellation)
        where T : class
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(predicate, cancellation);
    }

    public async Task<int> Count<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellation)
        where T : class
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await db.Set<T>().AsNoTracking().CountAsync(predicate, cancellation);
    }

    /// <summary>Empties every table and leaves the schema, so a test starts from a known world.</summary>
    public async Task Clear(CancellationToken cancellation)
    {
        ThrowIfNotStarted();

        await using var connection = new NpgsqlConnection(_postgres!.GetConnectionString());
        await connection.OpenAsync(cancellation);

        // Built once: Respawn reads the schema to work out the delete order, which does not change.
        _respawner ??= await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                SchemasToInclude = ["public"],
                TablesToIgnore = ["__EFMigrationsHistory"],
                DbAdapter = DbAdapter.Postgres,
            });

        await _respawner.ResetAsync(connection);
    }

    private void ThrowIfNotStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException(
                $"Database harness is not started. Call {nameof(Start)} first.");
        }
    }
}
