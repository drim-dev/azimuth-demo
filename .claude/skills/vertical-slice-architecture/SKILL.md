---
name: vertical-slice-architecture
description: Use when implementing backend features - provides vertical slice patterns with MediatR, FluentValidation, and IEndpoint (project)
---

# Vertical Slice Architecture Implementation

## When to Use This Skill

Use this skill when:

- Implementing ANY backend feature (command or query)
- Creating new vertical slices in `backend/src/Api/Features/`
- Need examples of validation, authorization, or error handling

**For testing vertical slices:** Use the `component-testing` skill instead. It provides harness-based testing patterns that test features as complete units through HTTP endpoints.

**Announce at start:** "I'm using the vertical-slice-architecture skill to implement this feature."

## Project Structure

### Features (Vertical Slices)

Each feature = one static class file with nested types:

```text
Features/
├── [Domain]/
│   ├── [Feature].cs          # Single file, all nested types
│   └── [Feature]Tests.cs     # Co-located test (or in tests/ project)
```

**Example:** `Features/Blog/CreatePost.cs`

### Common Infrastructure (Organized by Concern)

All shared infrastructure code is organized into concern-based folders. **Each file belongs to a specific concern, not a generic category like "Middleware".**

```text
Common/
├── Auth/                     # Authentication & authorization
│   └── UserContextMiddleware.cs  # BFF header → Claims conversion
├── Exceptions/               # Exception handling & domain exceptions
│   ├── Exceptions.cs        # NotFoundException, ForbiddenException, etc.
│   └── ExceptionHandlerMiddleware.cs  # Global exception handling
├── Http/                     # HTTP/endpoint infrastructure
│   ├── IEndpoint.cs         # Interface for endpoint registration
│   └── HttpContextExtensions.cs  # User context extraction helpers
├── Identity/                 # ID generation utilities
│   ├── IdFactory.cs         # IdGen wrapper for DI
│   └── Base32Encoder.cs     # Crockford Base32 encoding/decoding
└── Validation/               # Validation infrastructure
    └── ValidationBehavior.cs  # MediatR pipeline behavior
```

**Namespace Convention:** `DrimDev.Api.Common.{Concern}`
- Example: `DrimDev.Api.Common.Auth`, `DrimDev.Api.Common.Exceptions`, `DrimDev.Api.Common.Http`

**When creating new Common/ files:**
1. Identify the **specific concern** (Auth, Exceptions, Http, Identity, Validation, Caching, Storage, etc.)
2. Middleware belongs to the concern it serves (e.g., auth middleware → `Common/Auth/`, caching middleware → `Common/Caching/`)
3. Create a new concern folder if needed (e.g., `Common/Caching/`, `Common/Storage/`)
4. Place the file in the appropriate concern folder
5. Use the namespace `DrimDev.Api.Common.{Concern}`

**Anti-pattern:** Don't create generic folders like `Middleware/`, `Utilities/`, `Helpers/` - always use the specific concern.

## Core Pattern

Every vertical slice follows the same structure:

1. **Endpoint** - Maps HTTP route, extracts user, calls MediatR
2. **Request** - MediatR command/query with all input data
3. **Response** - Explicit DTO returned by handler
4. **RequestValidator** - FluentValidation rules
5. **RequestHandler** - Business logic, database operations

## Quick Reference

### Minimal Command Example

```csharp
namespace Api.Features.Blog;

public static class CreatePost
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app)
        {
            app.MapPost("/api/blog/posts", async (
                [FromBody] Body body,
                ISender sender,
                CancellationToken ct) =>
            {
                var request = new Request(body.Title, body.Slug);
                var response = await sender.Send(request, ct);
                return Results.Created($"/api/blog/posts/{response.PostId}", response);
            });
        }

        private record Body(string Title, string Slug);
    }

    public record Request(string Title, string Slug) : IRequest<Response>;
    public record Response(Guid PostId);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]+$");
        }
    }

    public class RequestHandler : IRequestHandler<Request, Response>
    {
        private readonly AppDbContext _db;

        public RequestHandler(AppDbContext db) => _db = db;

        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Slug = request.Slug.ToLower()
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync(ct);

            return new Response(post.Id);
        }
    }
}
```

### Minimal Query Example

```csharp
namespace Api.Features.Blog;

public static class GetPost
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app)
        {
            app.MapGet("/api/blog/posts/{slug}", async (
                string slug,
                ISender sender,
                CancellationToken ct) =>
            {
                var response = await sender.Send(new Request(slug), ct);
                return response != null ? Results.Ok(response) : Results.NotFound();
            });
        }
    }

    public record Request(string Slug) : IRequest<Response?>;

    public record Response(Guid Id, string Title, string Slug);

    public class RequestHandler : IRequestHandler<Request, Response?>
    {
        private readonly AppDbContext _db;

        public RequestHandler(AppDbContext db) => _db = db;

        public async Task<Response?> Handle(Request request, CancellationToken ct)
        {
            return await _db.Posts
                .Where(p => p.Slug == request.Slug && p.IsPublished)
                .Select(p => new Response(p.Id, p.Title, p.Slug))
                .FirstOrDefaultAsync(ct);
        }
    }
}
```

## Configuration Management

### Options Pattern

**Always use the Options pattern for configuration access** in vertical slices. Never inject `IConfiguration` directly into handlers.

**Structure:**
```text
Features/
└── [Domain]/
    ├── Options/
    │   └── [Domain]Options.cs    # Configuration class for this domain
    └── [Feature].cs               # Feature using IOptions<DomainOptions>
```

**Example:** `Features/Users/Options/UsersOptions.cs`

```csharp
namespace Api.Features.Users.Options;

/// <summary>
/// Configuration options for the Users domain.
/// </summary>
public class UsersOptions
{
    /// <summary>
    /// Email addresses that should be automatically promoted to Admin role on OAuth callback.
    /// </summary>
    public string[] AdminEmails { get; set; } = [];
}
```

**Usage in Handler:**

```csharp
using Microsoft.Extensions.Options;

public class RequestHandler : IRequestHandler<Request, Response>
{
    private readonly AppDbContext _db;
    private readonly UsersOptions _usersOptions;

    public RequestHandler(AppDbContext db, IOptions<UsersOptions> usersOptions)
    {
        _db = db;
        _usersOptions = usersOptions.Value;
    }

    public async Task<Response> Handle(Request request, CancellationToken ct)
    {
        // Access configuration
        var isAdmin = _usersOptions.AdminEmails.Contains(request.Email);
        // ...
    }
}
```

**Registration in Program.cs:**

```csharp
// Configure domain-specific options
builder.Services.Configure<UsersOptions>(builder.Configuration.GetSection("Users"));
```

**Configuration in appsettings.json:**

```json
{
  "Users": {
    "AdminEmails": ["admin@example.com"]
  }
}
```

**Benefits:**
- Strongly-typed configuration
- Testable (easy to mock options)
- Validation support (DataAnnotations or FluentValidation)
- Reloadable configuration (if using `IOptionsMonitor<T>`)

**Rules:**
- One options class per domain (e.g., `UsersOptions`, `CoursesOptions`)
- Place in `Features/[Domain]/Options/` folder
- Use `IOptions<T>` for static configuration
- Use `IOptionsMonitor<T>` for reloadable configuration
- Never inject `IConfiguration` directly into handlers

## Implementation Guide

For complete patterns, examples, and anti-patterns, see:

- **[Implementation Patterns](vsa-patterns.md)** - Complete templates, common patterns, authorization, pagination, testing, anti-patterns

## Checklist for Every Feature

When implementing a new vertical slice:

- [ ] Create feature file: `Features/[Domain]/[Feature].cs`
- [ ] Implement nested `Endpoint` class with `MapEndpoint` method
- [ ] Define `Request` record implementing `IRequest<TResponse>`
- [ ] Define `Response` record (or `IRequest` for commands with no return)
- [ ] Create `RequestValidator` inheriting `AbstractValidator<Request>`
- [ ] Implement validation rules in validator constructor
- [ ] Create `RequestHandler` implementing `IRequestHandler<Request, Response>`
- [ ] Inject dependencies in handler constructor (`AppDbContext`, `ILogger`, etc.)
- [ ] Implement business logic in `Handle` method
- [ ] Add authorization policy to endpoint if needed (`.RequireAuthorization("PolicyName")`)
- [ ] Extract user from `HttpContext.User` in endpoint if needed
- [ ] Add structured logging in handler (important operations, errors)
- [ ] Write component tests (see `component-testing` skill for patterns)
- [ ] Test happy path, validation errors, edge cases, authorization
- [ ] Verify both HTTP response and database state in tests
- [ ] Commit with descriptive message following conventional commits

## Key Rules

1. **One feature = one file** - All nested types in single static class file
2. **Keep nested types internal/private** - Only `Request` and `Response` need to be public
3. **Always use MediatR** - Validation runs automatically via pipeline
4. **Thin endpoints** - Delegate all logic to handler
5. **Use DTOs** - Never return EF entities directly
6. **Component tests** - Test through HTTP endpoint, not internal classes

## Summary

Vertical Slice Architecture = One Feature, One File

**Benefits:**

- Easy to find and modify features (everything in one place)
- Consistent structure across all features
- Automatic validation via MediatR pipeline
- Testable in isolation with component tests

**Use this skill for every backend feature to maintain consistency and quality.**
