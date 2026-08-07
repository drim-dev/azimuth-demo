# Harness Implementations Reference

This document contains complete harness implementations. These are created once and reused across all tests.

## DatabaseHarness (PostgreSQL with TestContainers)

Complete implementation with seeding, assertion, and cleanup methods:

```csharp
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

namespace YourApp.Tests.Harnesses;

public class DatabaseHarness<TProgram, TDbContext> : IHarness<TProgram>
    where TProgram : class
    where TDbContext : DbContext
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<TProgram>? _factory;
    private bool _started;
    private readonly string _databaseResourceName;

    public DatabaseHarness(string databaseResourceName)
    {
        _databaseResourceName = databaseResourceName;
    }

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        // Override connection string to use TestContainer
        builder.UseSetting(
            $"ConnectionStrings:{_databaseResourceName}",
            _postgres!.GetConnectionString());
    }

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellationToken)
    {
        _factory = factory;

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:15.4-alpine3.18")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await _postgres.StartAsync(cancellationToken);
        _started = true;
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (_postgres is not null)
        {
            await _postgres.StopAsync(cancellationToken);
            await _postgres.DisposeAsync();
        }

        _started = false;
    }

    // === SEEDING METHODS ===

    /// <summary>
    /// Run EF Core migrations on the test database
    /// </summary>
    public async Task Migrate(CancellationToken cancellationToken)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Save entities to the database. Accepts single entities or collections.
    /// </summary>
    public async Task Save(params object[] entities)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Handle collections (arrays, lists)
        var collections = entities.OfType<IEnumerable>();
        foreach (var collection in collections)
        {
            dbContext.AddRange(collection);
        }

        // Handle single entities
        var singleEntities = entities.Where(e => e is not IEnumerable);
        dbContext.AddRange(singleEntities);

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Execute custom action with DbContext for complex seeding
    /// </summary>
    public async Task Execute(Func<TDbContext, Task> action)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(db);
    }

    /// <summary>
    /// Execute custom query with DbContext and return result
    /// </summary>
    public async Task<T> Execute<T>(Func<TDbContext, Task<T>> action)
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await action(db);
    }

    // === ASSERTION METHODS ===

    /// <summary>
    /// Query single entity matching predicate (for assertions)
    /// </summary>
    public async Task<T?> SingleOrDefault<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken)
        where T : class
    {
        ThrowIfNotStarted();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        return await db.Set<T>().SingleOrDefaultAsync(predicate, cancellationToken);
    }

    // === CLEANUP METHODS ===

    /// <summary>
    /// Clear all data from the database using Respawn (fast, preserves schema)
    /// </summary>
    public async Task Clear(CancellationToken cancellationToken)
    {
        ThrowIfNotStarted();

        await using var connection = new NpgsqlConnection(_postgres!.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            SchemasToInclude = ["public"],
            DbAdapter = DbAdapter.Postgres,
        });

        await respawner.ResetAsync(connection);
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
```

## HttpClientHarness

Simple harness for creating HTTP clients to call your API:

```csharp
namespace YourApp.Tests.Harnesses;

public class HttpClientHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private WebApplicationFactory<TProgram>? _factory;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        // No special configuration needed
    }

    public Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellationToken)
    {
        _factory = factory;
        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public HttpClient CreateClient()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException(
                $"HttpClient harness is not started. Call {nameof(Start)} first.");
        }

        return _factory.CreateClient();
    }
}
```

## RedisHarness (with TestContainers)

Example harness for Redis using TestContainers:

```csharp
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace YourApp.Tests.Harnesses;

public class RedisHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private RedisContainer? _redis;
    private IConnectionMultiplexer? _connection;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:ConnectionString", _redis!.GetConnectionString());
    }

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellationToken)
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redis.StartAsync(cancellationToken);

        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        if (_redis is not null)
        {
            await _redis.StopAsync(cancellationToken);
            await _redis.DisposeAsync();
        }
    }

    // === SEEDING METHODS ===

    public async Task Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        var db = _connection!.GetDatabase();
        var json = JsonSerializer.Serialize(value);
        await db.StringSetAsync(key, json, expiry);
    }

    // === ASSERTION METHODS ===

    public async Task<T?> Get<T>(string key)
    {
        var db = _connection!.GetDatabase();
        var value = await db.StringGetAsync(key);

        return value.HasValue
            ? JsonSerializer.Deserialize<T>(value!)
            : default;
    }

    public async Task<bool> Exists(string key)
    {
        var db = _connection!.GetDatabase();
        return await db.KeyExistsAsync(key);
    }

    // === CLEANUP METHODS ===

    public async Task Clear()
    {
        var endpoints = _connection!.GetEndPoints();
        var server = _connection.GetServer(endpoints.First());
        await server.FlushDatabaseAsync();
    }
}
```

## KafkaHarness (with TestContainers)

Example harness for Kafka using TestContainers:

```csharp
using System.Text.Json;
using Confluent.Kafka;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Kafka;

namespace YourApp.Tests.Harnesses;

public class KafkaHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private KafkaContainer? _kafka;
    private IProducer<string, string>? _producer;
    private IConsumer<string, string>? _consumer;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.UseSetting("Kafka:BootstrapServers", _kafka!.GetBootstrapAddress());
    }

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellationToken)
    {
        _kafka = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.4.0")
            .Build();

        await _kafka.StartAsync(cancellationToken);

        var config = new ProducerConfig { BootstrapServers = _kafka.GetBootstrapAddress() };
        _producer = new ProducerBuilder<string, string>(config).Build();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafka.GetBootstrapAddress(),
            GroupId = "test-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        _producer?.Dispose();
        _consumer?.Dispose();

        if (_kafka is not null)
        {
            await _kafka.StopAsync(cancellationToken);
            await _kafka.DisposeAsync();
        }
    }

    // === SEEDING METHODS ===

    public async Task Publish<T>(string topic, string key, T message)
    {
        var json = JsonSerializer.Serialize(message);
        await _producer!.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = json
        });
    }

    // === ASSERTION METHODS ===

    public async Task<List<T>> ConsumeMessages<T>(string topic, int expectedCount, TimeSpan timeout)
    {
        _consumer!.Subscribe(topic);

        var messages = new List<T>();
        var cts = new CancellationTokenSource(timeout);

        try
        {
            while (messages.Count < expectedCount && !cts.Token.IsCancellationRequested)
            {
                var result = _consumer.Consume(cts.Token);
                if (result != null)
                {
                    var message = JsonSerializer.Deserialize<T>(result.Message.Value);
                    if (message != null)
                    {
                        messages.Add(message);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached
        }

        return messages;
    }
}
```

## Harness Guidelines

### When Creating a New Harness

1. **Use TestContainers** - Always prefer TestContainers for real dependencies
2. **Implement IHarness<T>** - Follow the standard interface
3. **Override configuration** - Use `ConfigureWebHostBuilder` to point to TestContainer
4. **Provide seeding methods** - Make it easy to setup test data
5. **Provide assertion methods** - Make it easy to verify outcomes
6. **Provide cleanup method** - Fast reset between tests (e.g., Respawn, FlushDatabase)

### TestContainers Best Practices

**Use specific image tags:**
```csharp
// Good - Reproducible
.WithImage("postgres:15.4-alpine3.18")

// Bad - Unpredictable
.WithImage("postgres:latest")
```

**Use wait strategies:**
```csharp
.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
```

**Reuse containers:**
- Containers are started once per xUnit collection
- Use `Reset()` or `Clear()` methods between tests
- Don't start/stop containers per test (too slow)

### Alternative: In-Memory for Non-Critical Dependencies

If TestContainers are too slow or unavailable, use in-memory alternatives:

```csharp
public class InMemoryCacheHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private readonly Dictionary<string, object> _cache = new();

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real cache with in-memory
            services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
                Options.Create(new MemoryDistributedCacheOptions())));
        });
    }

    public Task Start(WebApplicationFactory<TProgram> factory, CancellationToken ct) =>
        Task.CompletedTask;

    public Task Stop(CancellationToken ct) => Task.CompletedTask;

    // In-memory methods...
}
```

**Only use when:**
- TestContainers performance is prohibitive
- CI environment doesn't support Docker
- Testing non-critical caching logic
