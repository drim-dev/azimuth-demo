---
name: component-testing
description: Use when writing tests for vertical slices - provides harness-based component testing patterns that test features as a whole through HTTP endpoints with real dependencies using TestContainers
---

# Component Testing with Harnesses

## When to Use This Skill

Use this skill when:

- Writing tests for vertical slice features
- Need to test a feature as a whole (not individual classes)
- Working with external dependencies (DB, Redis, Kafka, file storage, etc.)
- Setting up integration tests that are easy to maintain during refactoring

**Announce at start:** "I'm using the component-testing skill to write tests for this feature."

## Philosophy

**Component testing** means testing a vertical slice feature as a complete unit, including all its internal classes and logic, while controlling external dependencies through harnesses.

**Benefits:**

- **Easier refactoring** - Internal changes don't break tests
- **Realistic behavior** - Test against real dependencies (PostgreSQL, Redis, Kafka)
- **Maintainability** - Tests focus on behavior, not implementation details
- **Flexibility** - Can swap implementations without changing tests
- **Fast feedback** - TestContainers provide quick setup/teardown

**Not unit tests:** We don't test individual classes. We test the entire feature through its HTTP endpoint.

## TestContainers Preference

**⚠️ IMPORTANT: Use TestContainers for real dependencies whenever possible.**

**Prefer (in order):**

1. **TestContainers with real dependency** (PostgreSQL, Redis, Kafka containers)
2. **Lightweight alternatives** (SQLite for PostgreSQL, in-memory cache)
3. **Mocks** (only when TestContainers are impractical)

**Why TestContainers?**

- **Real behavior** - Test against actual database, not in-memory simulation
- **Catch integration issues** - SQL dialect differences, connection pooling, etc.
- **Production-like** - Same database engine as production
- **Fast enough** - Container reuse across tests in same collection
- **No surprises** - What works in tests works in production

**When to use alternatives:**

- **Performance constraints** - If TestContainers are too slow (rare with proper fixture setup)
- **CI limitations** - If CI environment doesn't support Docker (rare)
- **External APIs** - Use WireMock or mocks for third-party APIs

## What is a Harness?

A **harness** is an abstraction for an external dependency that encapsulates:

- Starting the dependency (TestContainer, mock, etc.)
- Configuring the SUT to use it
- Seeding data for tests
- Asserting state after operations
- Cleaning up between tests

### Harness Responsibilities

1. **Start** - Launch TestContainer or initialize mock
2. **Configure** - Override connection strings, register in DI
3. **Seed** - Provide methods to setup test data
4. **Assert** - Provide methods to verify outcomes
5. **Stop** - Clean up resources

## Core Interfaces

### IHarness Interface

```csharp
public interface IHarness<T> where T : class
{
    // Configure the web host to use this harness's dependency
    void ConfigureWebHostBuilder(IWebHostBuilder builder);

    // Start the dependency (e.g., launch TestContainer)
    Task Start(WebApplicationFactory<T> factory, CancellationToken cancellationToken);

    // Stop the dependency (e.g., stop TestContainer)
    Task Stop(CancellationToken cancellationToken);
}
```

### Extension Method

```csharp
public static class HarnessExtensions
{
    public static WebApplicationFactory<T> AddHarness<T>(
        this WebApplicationFactory<T> factory,
        IHarness<T> harness)
        where T : class =>
        factory.WithWebHostBuilder(harness.ConfigureWebHostBuilder);
}
```

## Quick Start

### 1. Create Harness Implementations

**For complete implementations, see [harnesses.md](harnesses.md):**

- DatabaseHarness with PostgreSQL TestContainer
- HttpClientHarness for HTTP requests
- Other harness types (Redis, Kafka, etc.)

### 2. Create TestFixture

**For complete implementation, see [test-fixture.md](test-fixture.md).**

Quick overview:

```csharp
public class TestFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        Database = new DatabaseHarness<Program, AppDbContext>("DefaultConnection");
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(HttpClient);
    }

    public DatabaseHarness<Program, AppDbContext> Database { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task Reset(CancellationToken cancellationToken) =>
        await Database.Clear(cancellationToken);

    public async Task InitializeAsync()
    {
        await Database.Start(_factory, CreateCancellationToken(60));
        await HttpClient.Start(_factory, CreateCancellationToken());
        _ = _factory.Server; // Force server initialization
    }

    public async Task DisposeAsync()
    {
        await HttpClient.Stop(CreateCancellationToken());
        await Database.Stop(CreateCancellationToken());
    }
}
```

### 3. Create xUnit Collection (Domain-Specific)

**IMPORTANT: Create domain-specific collections, NOT generic collections.**

```csharp
// GOOD - Domain-specific collection
[CollectionDefinition(Name)]
public class SkillsTestsCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(SkillsTestsCollection);
}

// GOOD - Another domain-specific collection
[CollectionDefinition(Name)]
public class BlogTestsCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(BlogTestsCollection);
}

// BAD - Generic collection name
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<TestFixture> // ❌ Too generic
{
    public const string Name = nameof(DatabaseCollection);
}
```

**Collection Naming Rules:**

- ✅ Name collections after the **domain/feature area** being tested (Skills, Blog, Courses, Users, etc.)
- ✅ Use pattern: `{Domain}TestsCollection` (e.g., SkillsTestsCollection, BlogTestsCollection)
- ❌ DO NOT use generic names like DatabaseCollection, ApiCollection, TestCollection
- ❌ DO NOT create one collection for all tests

**Why domain-specific collections?**

- **Parallel execution** - Different domains can run in parallel (SkillsTests || BlogTests)
- **Isolation** - Domain-specific test data doesn't interfere across collections
- **Clear organization** - Tests grouped by domain, matching vertical slice architecture
- **Performance** - TestContainers shared within domain, but domains run concurrently
- **Flexibility** - Some domains might need different harness configurations

**Why collections exist:**

- Share one TestFixture (and TestContainers) across multiple test classes in same domain
- Control parallelism (tests in same collection run sequentially, different collections run in parallel)
- Amortize startup cost of TestContainers within a domain

### 4. Write Component Tests

```csharp
[Collection(UsersTestsCollection.Name)]
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
        // Arrange - Seed data using harness
        const string login = "Sam";
        var request = new CreateAccountRequestContract(login, "Qwer1234!");

        // Act - Call HTTP endpoint (tests full vertical slice)
        var client = new RestClient(_fixture.HttpClient.CreateClient());
        var response = await client.ExecutePostAsync<AccountContract>(
            "/auth/accounts", request, CreateCancellationToken());

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location().Should().Be($"/auth/accounts/{login.ToLower()}");
        response.Data.Login.Should().Be(login.ToLower());

        // Assert database state (via harness)
        var dbAccount = await _fixture.Database.SingleOrDefault<Account>(
            x => x.Login == login.ToLower(),
            CreateCancellationToken());

        dbAccount.Should().NotBeNull();
        dbAccount.PasswordHash.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_return_conflict_if_account_exists()
    {
        // Arrange - Seed existing account
        await _fixture.Database.Save(CreateAccount("sam"));

        var request = new CreateAccountRequestContract("Sam", "Qwer1234!");

        // Act
        var client = new RestClient(_fixture.HttpClient.CreateClient());
        var response = await client.ExecutePostAsync<ProblemDetailsContract>(
            "/auth/accounts", request, CreateCancellationToken());

        // Assert
        response.ShouldBeLogicConflictError(
            "Account already exists",
            "auth:logic:account_already_exists");
    }
}
```

**For more examples, see [examples.md](examples.md).**

## Component Test Patterns

### Test Structure (Arrange-Act-Assert)

1. **Arrange** - Setup using harness methods
   ```csharp
   await _fixture.Database.Save(account, post);
   ```

2. **Act** - Call HTTP endpoint (tests full vertical slice)
   ```csharp
   var response = await client.ExecutePostAsync<Result>("/endpoint", request);
   ```

3. **Assert HTTP response** - Status code, headers, body
   ```csharp
   response.StatusCode.Should().Be(HttpStatusCode.Created);
   response.Data.Should().NotBeNull();
   ```

4. **Assert side effects** - Database state, messages sent, etc.
   ```csharp
   var entity = await _fixture.Database.SingleOrDefault<Entity>(x => x.Id == id);
   entity.Should().NotBeNull();
   ```

### What NOT to Test in Component Tests

**⚠️ CRITICAL: NEVER test validation rules in component tests.**

**❌ DO NOT create component tests for validation scenarios.**

Validation rules are tested in isolated unit tests using FluentValidation.TestHelper. Component tests focus on business logic, authorization, and side effects - NOT validation.

**If a feature has a RequestValidator:**
1. Create `ValidatorTests` class nested inside the component tests file
2. Test ALL validation rules using FluentValidation.TestHelper
3. Component tests should NOT test validation errors (empty fields, max lengths, invalid formats, etc.)

**Example of what NOT to do:**

```csharp
// ❌ BAD - Validator is already tested in ValidatorTests
[Fact]
public async Task Should_return_validation_error_when_display_name_too_long()
{
    var request = new { DisplayName = new string('A', 101) };
    var response = await client.PatchAsJsonAsync("/api/users/me", request);
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

// ❌ BAD - Validator is already tested in ValidatorTests
[Fact]
public async Task Should_return_validation_error_when_email_empty()
{
    var request = new { Email = "" };
    var response = await client.PostAsJsonAsync("/api/users", request);
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

**What to test in component tests:**
- ✅ Business logic and feature behavior
- ✅ Authorization (403 Forbidden, 401 Unauthorized)
- ✅ Resource existence (404 Not Found)
- ✅ Conflicts (409 Conflict)
- ✅ Database side effects
- ✅ Success scenarios with valid data

### Seeding Data

```csharp
// Single entity
await _fixture.Database.Save(account);

// Multiple entities
await _fixture.Database.Save(account1, account2, post);

// Collections
await _fixture.Database.Save(accountsList, postsList);

// Custom seeding
await _fixture.Database.Execute(async db =>
{
    db.Accounts.AddRange(accounts);
    await db.SaveChangesAsync();
});
```

### Asserting State

```csharp
// Query single entity
var account = await _fixture.Database.SingleOrDefault<Account>(
    x => x.Login == "sam",
    cancellationToken);

// Custom query
var count = await _fixture.Database.Execute(async db =>
    await db.Accounts.CountAsync());
```

### Testing with Authentication

```csharp
[Fact]
public async Task Should_require_authentication()
{
    // Create authenticated client via fixture helper
    var (client, account) = await _fixture.CreateAuthedHttpClient();

    var response = await client.GetAsync("/protected-resource");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

## Common Scenarios

### Testing Authorization

```csharp
[Fact]
public async Task Should_require_admin_role()
{
    var (client, account) = await _fixture.CreateAuthedHttpClient(); // Regular user

    var response = await client.DeleteAsync("/admin/users/123");

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### Testing Business Logic Errors

```csharp
[Fact]
public async Task Should_return_conflict_for_duplicate()
{
    await _fixture.Database.Save(CreatePost("my-slug"));

    var request = new CreatePostRequest("My Post", "my-slug", "Content");

    var response = await Act<ProblemDetailsContract>(request);

    response.ShouldBeLogicConflictError("Post with this slug already exists");
}
```

## Harness Development Guidelines

### Creating a New Harness

1. **Implement IHarness<T>**
2. **Use TestContainers** for the real dependency (preferred)
3. **ConfigureWebHostBuilder** - Override connection strings or settings
4. **Start** - Launch TestContainer
5. **Stop** - Cleanup TestContainer
6. **Add seeding methods** - Setup test data
7. **Add assertion methods** - Query state for verification
8. **Add cleanup method** - Fast reset between tests (e.g., Respawn for DB)

### Example: Redis Harness with TestContainers

```csharp
public class RedisHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private RedisContainer? _redis;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:ConnectionString", _redis!.GetConnectionString());
    }

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken ct)
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redis.StartAsync(ct);
    }

    public async Task Stop(CancellationToken ct)
    {
        if (_redis is not null)
        {
            await _redis.StopAsync(ct);
            await _redis.DisposeAsync();
        }
    }

    // Redis-specific methods
    public async Task Set<T>(string key, T value) { /* ... */ }
    public async Task<T?> Get<T>(string key) { /* ... */ }
    public async Task Clear() { /* ... */ }
}
```

## Testing Workflow (TDD)

1. **Red** - Write failing component test
   - Define HTTP request/response contract
   - Define expected side effects (DB state, etc.)

2. **Green** - Implement vertical slice
   - Create Endpoint, Request, Validator, Handler
   - Run test until it passes

3. **Refactor** - Improve implementation
   - Tests remain green (they test behavior, not implementation)

## Reference Documentation

- **[harnesses.md](harnesses.md)** - Complete harness implementations (DatabaseHarness, HttpClientHarness, etc.)
- **[test-fixture.md](test-fixture.md)** - Complete TestFixture implementation with helpers
- **[examples.md](examples.md)** - Complete test examples for various scenarios

## Testing Checklist

When writing component tests:

- [ ] Create harnesses for external dependencies (prefer TestContainers)
- [ ] Setup TestFixture with all harnesses
- [ ] Create xUnit collection for fixture sharing
- [ ] Reset fixture in `IAsyncLifetime.InitializeAsync()`
- [ ] Test through HTTP endpoint (full vertical slice)
- [ ] Assert both HTTP response and side effects
- [ ] Use harness seeding methods for arrange
- [ ] Use harness assertion methods for verify
- [ ] Keep tests focused on feature behavior
- [ ] Add fixture helper methods for common scenarios

## Summary

**Component tests verify vertical slice features work correctly as a whole:**
- Test through HTTP endpoint (realistic)
- Use TestContainers for real dependencies (preferred)
- Use harnesses to abstract dependency setup
- Reset state between tests (fast with Respawn)
- Assert both response and side effects

**Remember:** TestContainers provide the most realistic testing environment. Only fall back to mocks when truly necessary.
