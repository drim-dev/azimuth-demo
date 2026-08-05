# Backend Error Handling

Complete guide to backend error handling with domain exceptions and ProblemDetails in drim-dev.

## Domain Exceptions

Create custom exception types for different error scenarios.

```csharp
// Common/Exceptions/DomainException.cs
namespace DrimDev.Api.Common.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    public DomainException(string message, string errorCode, int statusCode = 422)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message, string errorCode)
        : base(message, errorCode, StatusCodes.Status404NotFound)
    {
    }
}

public class ForbiddenException : DomainException
{
    public ForbiddenException(string message, string errorCode)
        : base(message, errorCode, StatusCodes.Status403Forbidden)
    {
    }
}

public class ConflictException : DomainException
{
    public ConflictException(string message, string errorCode)
        : base(message, errorCode, StatusCodes.Status409Conflict)
    {
    }
}

public class UnprocessableEntityException : DomainException
{
    public UnprocessableEntityException(string message, string errorCode)
        : base(message, errorCode, StatusCodes.Status422UnprocessableEntity)
    {
    }
}
```

## Using Domain Exceptions

```csharp
// Features/Blog/DeletePost.cs
public class RequestHandler : IRequestHandler<Request>
{
    private readonly AppDbContext _db;
    private readonly ILogger<RequestHandler> _logger;

    public RequestHandler(AppDbContext db, ILogger<RequestHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(Request request, CancellationToken ct)
    {
        var post = await _db.Posts.FindAsync(request.PostId, ct);

        // Not found
        if (post == null)
        {
            throw new NotFoundException(
                $"Post with ID {request.PostId} not found",
                "blog:post:delete:not_found");
        }

        // Authorization check
        if (post.AuthorId != request.UserId)
        {
            throw new ForbiddenException(
                "You do not have permission to delete this post",
                "blog:post:delete:forbidden");
        }

        // Business rule: cannot delete published posts
        if (post.IsPublished)
        {
            throw new UnprocessableEntityException(
                "Cannot delete a published post. Unpublish it first.",
                "blog:post:delete:is_published");
        }

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted post {PostId} by user {UserId}", request.PostId, request.UserId);
    }
}
```

## Global Exception Handler

Convert all exceptions to ProblemDetails.

```csharp
// Common/Exceptions/GlobalExceptionHandler.cs
using DrimDev.Api.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DrimDev.Api.Common.Exceptions;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            DomainException domainEx => CreateProblemDetails(
                httpContext,
                domainEx.StatusCode,
                GetTitle(domainEx.StatusCode),
                domainEx.Message,
                domainEx.ErrorCode),

            UnauthorizedAccessException => CreateProblemDetails(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required to access this resource",
                "auth:unauthorized"),

            _ => CreateProblemDetails(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                _env.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred. Please try again later.",
                "server:internal_error")
        };

        // Log error
        if (exception is DomainException)
        {
            _logger.LogWarning(exception, "Domain exception occurred: {ErrorCode}",
                (exception as DomainException)!.ErrorCode);
        }
        else
        {
            _logger.LogError(exception, "Unhandled exception occurred");
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode)
    {
        return new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions =
            {
                ["errorCode"] = errorCode,
                ["traceId"] = context.TraceIdentifier
            }
        };
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        500 => "Internal Server Error",
        _ => "Error"
    };
}
```

**Register in Program.cs:**

```csharp
// Add exception handlers
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();  // From validation skill
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Use exception handler middleware
app.UseExceptionHandler();
```

## ProblemDetails Response Examples

**404 Not Found:**

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Not Found",
  "status": 404,
  "detail": "Post with ID 12345 not found",
  "instance": "/api/posts/12345",
  "errorCode": "blog:post:delete:not_found",
  "traceId": "00-abc123..."
}
```

**403 Forbidden:**

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to delete this post",
  "instance": "/api/posts/12345",
  "errorCode": "blog:post:delete:forbidden",
  "traceId": "00-abc123..."
}
```

**422 Unprocessable Entity:**

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Cannot delete a published post. Unpublish it first.",
  "instance": "/api/posts/12345",
  "errorCode": "blog:post:delete:is_published",
  "traceId": "00-abc123..."
}
```

**500 Internal Server Error (Production):**

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred. Please try again later.",
  "instance": "/api/posts",
  "errorCode": "server:internal_error",
  "traceId": "00-abc123..."
}
```

**500 Internal Server Error (Development):**

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Object reference not set to an instance of an object.",
  "instance": "/api/posts",
  "errorCode": "server:internal_error",
  "traceId": "00-abc123..."
}
```

## Database Errors

Handle database-specific errors (constraints, deadlocks, etc.).

```csharp
public async Task Handle(Request request, CancellationToken ct)
{
    try
    {
        var skill = new Skill
        {
            Id = _idFactory.Create(),
            Slug = request.Slug,
            Name = request.Name
        };

        await _db.Skills.AddAsync(skill, ct);
        await _db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
    {
        // Unique constraint violation (duplicate slug)
        if (pgEx.SqlState == "23505")
        {
            throw new ConflictException(
                "A skill with this slug already exists",
                "skills:skill:create:slug_exists");
        }

        // Foreign key violation
        if (pgEx.SqlState == "23503")
        {
            throw new UnprocessableEntityException(
                "Referenced entity does not exist",
                "skills:skill:create:invalid_reference");
        }

        // Re-throw if not handled
        throw;
    }
}
```

## Authorization Errors

```csharp
// Features/Users/UpdateProfile.cs
public class RequestHandler : IRequestHandler<Request, Response>
{
    public async Task<Response> Handle(Request request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(request.UserId, ct);

        if (user == null)
        {
            throw new NotFoundException(
                $"User with ID {request.UserId} not found",
                "users:profile:update:not_found");
        }

        // Users can only update their own profile (unless admin)
        if (user.Id != request.CurrentUserId && !request.IsAdmin)
        {
            throw new ForbiddenException(
                "You do not have permission to update this profile",
                "users:profile:update:forbidden");
        }

        user.Name = request.Name;
        user.Bio = request.Bio;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new Response(user.Id);
    }
}
```

## BFF Error Handling

### Passthrough Pattern

BFF passes through ProblemDetails from backend unchanged (same as validation).

```typescript
// app/api/posts/[id]/route.ts
export async function DELETE(
  request: Request,
  { params }: { params: { id: string } }
) {
  const session = await getServerSession(authOptions);

  if (!session) {
    // BFF-specific 401 error
    return Response.json({
      type: "https://tools.ietf.org/html/rfc7807",
      title: "Unauthorized",
      status: 401,
      detail: "Authentication is required",
      errorCode: "auth:unauthorized"
    }, { status: 401 });
  }

  const response = await fetch(
    `${process.env.BACKEND_URL}/api/posts/${params.id}`,
    {
      method: 'DELETE',
      headers: {
        'Authorization': `Bearer ${session.accessToken}`,
        'X-User-Id': session.user.id
      }
    }
  );

  // Pass through status code and ProblemDetails
  return new Response(await response.text(), {
    status: response.status,
    headers: { 'Content-Type': 'application/json' }
  });
}
```

### BFF-Specific Errors

```typescript
// app/api/posts/route.ts
export async function POST(request: Request) {
  const session = await getServerSession(authOptions);

  if (!session) {
    return Response.json({
      type: "https://tools.ietf.org/html/rfc7807",
      title: "Unauthorized",
      status: 401,
      detail: "Authentication is required",
      errorCode: "auth:unauthorized"
    }, { status: 401 });
  }

  try {
    const response = await fetch(`${process.env.BACKEND_URL}/api/posts`, {
      method: 'POST',
      body: await request.text(),
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${session.accessToken}`,
        'X-User-Id': session.user.id
      }
    });

    return new Response(await response.text(), {
      status: response.status,
      headers: { 'Content-Type': 'application/json' }
    });
  } catch (error) {
    // Network error or backend unreachable
    console.error('Backend error:', error);

    return Response.json({
      type: "https://tools.ietf.org/html/rfc7807",
      title: "Service Unavailable",
      status: 503,
      detail: "Backend service is temporarily unavailable. Please try again later.",
      errorCode: "bff:backend:unavailable"
    }, { status: 503 });
  }
}
```
