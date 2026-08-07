# TestFixture Implementation Reference

This document contains a complete TestFixture implementation with helper methods. TestFixtures are created once per xUnit collection and shared across multiple test classes.

## Complete TestFixture Example

```csharp
using System.Net.Http.Headers;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YourApp.Database;
using YourApp.Domain;
using YourApp.Features.Auth.Services;
using YourApp.Tests.Harnesses;

namespace YourApp.Tests.Fixtures;

public class TestFixture : IAsyncLifetime
{
    static TestFixture()
    {
        SetupFluentAssertions();
    }

    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        // Initialize harnesses
        Database = new DatabaseHarness<Program, AppDbContext>("DefaultConnection");
        HttpClient = new HttpClientHarness<Program>();

        // Add all harnesses to factory
        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(HttpClient);
    }

    public WebApplicationFactory<Program> Factory => _factory;
    public DatabaseHarness<Program, AppDbContext> Database { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    /// <summary>
    /// Reset fixture state between tests (fast with Respawn)
    /// </summary>
    public async Task Reset(CancellationToken cancellationToken)
    {
        await Database.Clear(cancellationToken);
    }

    /// <summary>
    /// Helper: Create authenticated HTTP client with account
    /// </summary>
    public async Task<(HttpClient, Account)> CreateAuthedHttpClient()
    {
        var account = CreateAccount();
        await Database.Save(account);

        await using var scope = _factory.Services.CreateAsyncScope();
        var jwtGenerator = scope.ServiceProvider.GetRequiredService<JwtGenerator>();
        var jwt = jwtGenerator.Generate(account);

        var client = HttpClient.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);

        return (client, account);
    }

    /// <summary>
    /// Helper: Create HTTP client with invalid authentication
    /// </summary>
    public async Task<(HttpClient, Account)> CreateWronglyAuthedHttpClient()
    {
        var account = CreateAccount();
        await Database.Save(account);

        await using var scope = _factory.Services.CreateAsyncScope();
        var jwtGenerator = scope.ServiceProvider.GetRequiredService<JwtGenerator>();
        var incorrectJwt = jwtGenerator.Generate(account) + "123"; // Make it invalid

        var client = HttpClient.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", incorrectJwt);

        return (client, account);
    }

    // IAsyncLifetime implementation
    public async Task InitializeAsync()
    {
        // Start all harnesses
        await Database.Start(_factory, CreateCancellationToken(60));
        await HttpClient.Start(_factory, CreateCancellationToken());

        // Force lazy initialization of the server
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        // Stop all harnesses
        await HttpClient.Stop(CreateCancellationToken());
        await Database.Stop(CreateCancellationToken());
    }

    // Workaround to fix FluentAssertion concurrency issue
    // https://github.com/fluentassertions/fluentassertions/issues/1932#issuecomment-1137366562
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void SetupFluentAssertions()
    {
        AssertionOptions.AssertEquivalencyUsing(options => options
            .Using<DateTimeOffset>(ctx => ctx.Subject.Should().BeSameDateAs(ctx.Expectation))
            .WhenTypeIs<DateTimeOffset>()
            .Using<DateTime>(ctx => ctx.Subject.Should().BeSameDateAs(ctx.Expectation))
            .WhenTypeIs<DateTime>()
        );
    }
}
```

## TestFixture with Multiple Databases

If your application uses multiple databases:

```csharp
public class TestFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        // Multiple database harnesses
        AuthDb = new DatabaseHarness<Program, AuthDbContext>("AuthConnection");
        ContentDb = new DatabaseHarness<Program, ContentDbContext>("ContentConnection");
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(AuthDb)
            .AddHarness(ContentDb)
            .AddHarness(HttpClient);
    }

    public DatabaseHarness<Program, AuthDbContext> AuthDb { get; }
    public DatabaseHarness<Program, ContentDbContext> ContentDb { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task Reset(CancellationToken cancellationToken)
    {
        // Clear all databases
        await AuthDb.Clear(cancellationToken);
        await ContentDb.Clear(cancellationToken);
    }

    public async Task InitializeAsync()
    {
        await AuthDb.Start(_factory, CreateCancellationToken(60));
        await ContentDb.Start(_factory, CreateCancellationToken(60));
        await HttpClient.Start(_factory, CreateCancellationToken());
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await HttpClient.Stop(CreateCancellationToken());
        await AuthDb.Stop(CreateCancellationToken());
        await ContentDb.Stop(CreateCancellationToken());
    }
}
```

## TestFixture with Additional Harnesses

Example with Redis and Kafka:

```csharp
public class TestFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        Database = new DatabaseHarness<Program, AppDbContext>("DefaultConnection");
        Redis = new RedisHarness<Program>();
        Kafka = new KafkaHarness<Program>();
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(Redis)
            .AddHarness(Kafka)
            .AddHarness(HttpClient);
    }

    public DatabaseHarness<Program, AppDbContext> Database { get; }
    public RedisHarness<Program> Redis { get; }
    public KafkaHarness<Program> Kafka { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task Reset(CancellationToken cancellationToken)
    {
        await Database.Clear(cancellationToken);
        await Redis.Clear();
        // Kafka topics are typically not cleared between tests
    }

    public async Task InitializeAsync()
    {
        await Database.Start(_factory, CreateCancellationToken(60));
        await Redis.Start(_factory, CreateCancellationToken(30));
        await Kafka.Start(_factory, CreateCancellationToken(60));
        await HttpClient.Start(_factory, CreateCancellationToken());
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await HttpClient.Stop(CreateCancellationToken());
        await Database.Stop(CreateCancellationToken());
        await Redis.Stop(CreateCancellationToken());
        await Kafka.Stop(CreateCancellationToken());
    }
}
```

## Common Helper Methods

### Authentication Helpers

```csharp
/// <summary>
/// Create authenticated HTTP client for a regular user
/// </summary>
public async Task<(HttpClient, Account)> CreateAuthedHttpClient()
{
    var account = CreateAccount();
    await Database.Save(account);

    var jwt = GenerateJwt(account);

    var client = HttpClient.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);

    return (client, account);
}

/// <summary>
/// Create authenticated HTTP client for an admin user
/// </summary>
public async Task<(HttpClient, Account)> CreateAdminHttpClient()
{
    var account = CreateAccount(role: "Admin");
    await Database.Save(account);

    var jwt = GenerateJwt(account);

    var client = HttpClient.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);

    return (client, account);
}

/// <summary>
/// Create HTTP client with expired token
/// </summary>
public async Task<(HttpClient, Account)> CreateExpiredTokenHttpClient()
{
    var account = CreateAccount();
    await Database.Save(account);

    var jwt = GenerateExpiredJwt(account);

    var client = HttpClient.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);

    return (client, account);
}

private string GenerateJwt(Account account)
{
    await using var scope = _factory.Services.CreateAsyncScope();
    var jwtGenerator = scope.ServiceProvider.GetRequiredService<JwtGenerator>();
    return jwtGenerator.Generate(account);
}
```

### Entity Creation Helpers

```csharp
/// <summary>
/// Create a test account with default values
/// </summary>
private Account CreateAccount(
    string login = "testuser",
    string role = "User")
{
    return new Account(
        Id: 0,
        Login: login,
        PasswordHash: HashPassword("Password123!"),
        CreatedAt: DateTime.UtcNow);
}

/// <summary>
/// Create a test post with default values
/// </summary>
private Post CreatePost(
    string slug = "test-post",
    Account? author = null)
{
    return new Post(
        Id: 0,
        AuthorId: author?.Id ?? 1,
        Title: "Test Post",
        Slug: slug,
        Content: "Test content",
        CreatedAt: DateTime.UtcNow);
}
```

### Service Access Helpers

```csharp
/// <summary>
/// Get a service from the DI container
/// </summary>
public T GetService<T>() where T : notnull
{
    var scope = _factory.Services.CreateScope();
    return scope.ServiceProvider.GetRequiredService<T>();
}

/// <summary>
/// Execute action with a scoped service
/// </summary>
public async Task WithService<T>(Func<T, Task> action) where T : notnull
{
    await using var scope = _factory.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<T>();
    await action(service);
}
```

## xUnit Collection Setup

Every TestFixture needs a corresponding xUnit collection:

```csharp
using DrimCity.WebApi.Tests.Fixtures;

namespace DrimCity.WebApi.Tests.Features.Auth;

[CollectionDefinition(Name)]
public class AuthTestsCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(AuthTestsCollection);
}
```

**Why collections?**
- **Fixture sharing** - One TestFixture instance shared across all test classes in the collection
- **Parallelism control** - Tests in the same collection run sequentially
- **Performance** - TestContainers are started once, not per test class
- **Resource management** - Containers are cleaned up once when collection completes

## Using TestFixture in Tests

```csharp
using YourApp.Tests.Fixtures;

[Collection(AuthTestsCollection.Name)]
public class CreateAccountTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public CreateAccountTests(TestFixture fixture) => _fixture = fixture;

    // Reset state before each test (fast with Respawn)
    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_create_account()
    {
        // Use fixture harnesses and helpers
        var (client, account) = await _fixture.CreateAuthedHttpClient();

        // Test logic...
    }
}
```

## Fixture Lifecycle

1. **Collection start** - TestFixture constructor runs once
2. **InitializeAsync** - Start all harnesses (TestContainers launch)
3. **Test class 1** - Uses fixture
   - **IAsyncLifetime.InitializeAsync** - Reset fixture state
   - **Test 1** - Runs
   - **Test 2** - Runs
   - **IAsyncLifetime.DisposeAsync** - No-op
4. **Test class 2** - Uses same fixture
   - **IAsyncLifetime.InitializeAsync** - Reset fixture state
   - **Test 3** - Runs
   - **IAsyncLifetime.DisposeAsync** - No-op
5. **Collection end** - TestFixture.DisposeAsync runs (stop containers)

## Best Practices

### DO: Use Reset() between tests
```csharp
public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
```

**Why:** Fast cleanup with Respawn (preserves schema, clears data)

### DO: Create helper methods for common scenarios
```csharp
public async Task<(HttpClient, Account)> CreateAuthedHttpClient() { }
public async Task<(HttpClient, Account)> CreateAdminHttpClient() { }
```

**Why:** Reduces duplication, makes tests more readable

### DO: Expose harnesses as properties
```csharp
public DatabaseHarness<Program, WebApiDbContext> Database { get; }
public HttpClientHarness<Program> HttpClient { get; }
```

**Why:** Clear, type-safe access to harness methods

### DON'T: Start/stop containers per test
```csharp
// BAD - Too slow
public async Task InitializeAsync()
{
    await _fixture.Database.Start(...);
}

public async Task DisposeAsync()
{
    await _fixture.Database.Stop(...);
}
```

**Why:** Starting TestContainers is slow. Start once per collection, reset between tests.

### DON'T: Share mutable state across tests
```csharp
// BAD - State leaks between tests
private readonly Account _sharedAccount = CreateAccount();
```

**Why:** Tests should be independent. Use `Reset()` to clear state, create fresh entities in each test.

## Troubleshooting

### TestContainers are slow

**Solution:** Ensure you're reusing containers across tests:
- Use xUnit collections to share TestFixture
- Call `Reset()` between tests instead of restart
- Use fast cleanup methods (Respawn for DB, FlushDatabase for Redis)

### Tests fail intermittently

**Solution:** Ensure proper cleanup:
- Call `Reset()` in `IAsyncLifetime.InitializeAsync()`
- Verify Respawn configuration includes all schemas
- Check for background jobs or async operations completing

### Out of memory

**Solution:** Limit number of parallel test collections:
- xUnit runs collections in parallel by default
- Set `maxParallelThreads` in xunit.runner.json if needed
- Ensure TestContainers are properly disposed

## Summary

**TestFixture responsibilities:**
1. Initialize harnesses once per collection
2. Expose harnesses for test access
3. Provide helper methods for common scenarios
4. Fast reset between tests
5. Cleanup on collection disposal

**Key patterns:**
- Use `IAsyncLifetime` for fixture lifecycle
- Create xUnit collection for sharing
- Reset in test's `IAsyncLifetime.InitializeAsync()`
- Expose harnesses as properties
- Add domain-specific helper methods
