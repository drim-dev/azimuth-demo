# Backend Validation with FluentValidation

Complete guide to implementing backend validation using FluentValidation in drim-dev.

## Basic Validation Rules

```csharp
using FluentValidation;

namespace DrimDev.Api.Features.Blog;

public static class CreatePost
{
    public record Request(string Title, string Slug, string Content) : IRequest<Response>;

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            // Required field
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .WithErrorCode("blog:post:title:required");

            // Length constraint
            RuleFor(x => x.Title)
                .MaximumLength(200)
                .WithMessage("Title must be 200 characters or less")
                .WithErrorCode("blog:post:title:too_long");

            // Regex pattern
            RuleFor(x => x.Slug)
                .NotEmpty()
                .WithErrorCode("blog:post:slug:required")
                .Matches("^[a-z0-9-]+$")
                .WithMessage("Slug must be lowercase letters, numbers, and hyphens only")
                .WithErrorCode("blog:post:slug:invalid_format");

            // Email validation
            RuleFor(x => x.AuthorEmail)
                .NotEmpty()
                .WithErrorCode("blog:post:author_email:required")
                .EmailAddress()
                .WithMessage("Invalid email format")
                .WithErrorCode("blog:post:author_email:invalid_format");

            // URL validation
            RuleFor(x => x.Website)
                .Must(BeValidUrl)
                .When(x => !string.IsNullOrEmpty(x.Website))
                .WithMessage("Invalid URL format")
                .WithErrorCode("blog:post:website:invalid_format");
        }

        private bool BeValidUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
```

## Async Validation (Uniqueness Checks)

```csharp
public class RequestValidator : AbstractValidator<Request>
{
    private readonly AppDbContext _db;

    public RequestValidator(AppDbContext db)
    {
        _db = db;

        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithErrorCode("blog:post:slug:required")
            .MustAsync(BeUniqueSlug)
            .WithMessage("A post with this slug already exists")
            .WithErrorCode("blog:post:slug:already_exists");
    }

    private async Task<bool> BeUniqueSlug(string slug, CancellationToken ct)
    {
        var exists = await _db.Posts
            .AnyAsync(p => p.Slug == slug.ToLower(), ct);
        return !exists;
    }
}
```

**⚠️ IMPORTANT:** Async validators require DbContext injection. Register validator with DI:

```csharp
// Program.cs
services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);
```

## File Upload Validation

```csharp
public record Request(IFormFile Avatar) : IRequest<Response>;

public class RequestValidator : AbstractValidator<Request>
{
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public RequestValidator()
    {
        RuleFor(x => x.Avatar)
            .NotNull()
            .WithMessage("Avatar is required")
            .WithErrorCode("users:avatar:required");

        RuleFor(x => x.Avatar)
            .Must(HaveValidSize)
            .WithMessage($"File size must not exceed {MaxFileSize / 1024 / 1024} MB")
            .WithErrorCode("users:avatar:file_too_large");

        RuleFor(x => x.Avatar)
            .Must(HaveValidMimeType)
            .WithMessage($"File must be one of: {string.Join(", ", AllowedMimeTypes)}")
            .WithErrorCode("users:avatar:invalid_mime_type");

        RuleFor(x => x.Avatar)
            .Must(HaveValidExtension)
            .WithMessage($"File extension must be one of: {string.Join(", ", AllowedExtensions)}")
            .WithErrorCode("users:avatar:invalid_extension");
    }

    private bool HaveValidSize(IFormFile? file)
    {
        return file != null && file.Length > 0 && file.Length <= MaxFileSize;
    }

    private bool HaveValidMimeType(IFormFile? file)
    {
        return file != null && AllowedMimeTypes.Contains(file.ContentType.ToLower());
    }

    private bool HaveValidExtension(IFormFile? file)
    {
        if (file == null) return false;
        var extension = Path.GetExtension(file.FileName).ToLower();
        return AllowedExtensions.Contains(extension);
    }
}
```

## Cross-Field Validation

```csharp
public record Request(string Password, string PasswordConfirmation) : IRequest<Response>;

public class RequestValidator : AbstractValidator<Request>
{
    public RequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode("users:password:required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .WithErrorCode("users:password:too_short")
            .Matches(@"[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .WithErrorCode("users:password:missing_uppercase")
            .Matches(@"[a-z]")
            .WithMessage("Password must contain at least one lowercase letter")
            .WithErrorCode("users:password:missing_lowercase")
            .Matches(@"[0-9]")
            .WithMessage("Password must contain at least one number")
            .WithErrorCode("users:password:missing_number");

        RuleFor(x => x.PasswordConfirmation)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match")
            .WithErrorCode("users:password_confirmation:mismatch");
    }
}
```

## Conditional Validation

```csharp
public record Request(bool IsPublished, DateTime? PublishedAt) : IRequest<Response>;

public class RequestValidator : AbstractValidator<Request>
{
    public RequestValidator()
    {
        // PublishedAt is required only if IsPublished is true
        RuleFor(x => x.PublishedAt)
            .NotNull()
            .WithMessage("Published date is required when publishing")
            .WithErrorCode("blog:post:published_at:required")
            .When(x => x.IsPublished);

        // PublishedAt must be in the past or present
        RuleFor(x => x.PublishedAt)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Published date cannot be in the future")
            .WithErrorCode("blog:post:published_at:future_date")
            .When(x => x.PublishedAt.HasValue);
    }
}
```

## When to Validate in Validator vs Handler

**Validator (FluentValidation):**

- ✅ Format validation (regex, length, email)
- ✅ Required fields
- ✅ Simple business rules (password strength)
- ✅ Uniqueness checks (async)
- ✅ Cross-field validation
- ✅ File validation (size, type, extension)

**Handler (Business Logic):**

- ✅ Complex business rules requiring multiple entities
- ✅ Authorization checks (user owns resource)
- ✅ Domain-specific constraints
- ✅ State transitions (can only publish if draft)

**Example:**

```csharp
public class RequestValidator : AbstractValidator<Request>
{
    public RequestValidator(AppDbContext db)
    {
        // Simple uniqueness check in validator
        RuleFor(x => x.Slug)
            .MustAsync(async (slug, ct) => !await db.Posts.AnyAsync(p => p.Slug == slug, ct))
            .WithErrorCode("blog:post:slug:already_exists");
    }
}

public class RequestHandler : IRequestHandler<Request, Response>
{
    public async Task<Response> Handle(Request request, CancellationToken ct)
    {
        // Complex business rule in handler
        var user = await _db.Users.FindAsync(request.UserId);

        if (request.IsPublished && !user.IsVerified)
        {
            throw new DomainException(
                "Only verified users can publish posts",
                "blog:post:publish:user_not_verified");
        }

        // ... rest of logic
    }
}
```

## ProblemDetails Integration

FluentValidation errors automatically convert to ProblemDetails via MediatR pipeline behavior.

**Setup (Program.cs):**

```csharp
services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);
services.AddFluentValidationAutoValidation();

// Add MediatR pipeline behavior for automatic validation
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

services.AddProblemDetails();
```

**ValidationBehavior (MediatR Pipeline):**

```csharp
using FluentValidation;
using MediatR;

namespace DrimDev.Api.Common;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Any())
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

**Exception Handler (converts to ProblemDetails):**

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace DrimDev.Api.Common;

public class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "One or more validation errors occurred",
            Status = StatusCodes.Status400BadRequest,
            Extensions =
            {
                ["errors"] = errors,
                ["errorCodes"] = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorCode).ToArray())
            }
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

// Register in Program.cs
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
```

**ProblemDetails Response:**

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "One or more validation errors occurred",
  "status": 400,
  "errors": {
    "title": ["Title is required"],
    "slug": ["Slug must be lowercase letters, numbers, and hyphens only"]
  },
  "errorCodes": {
    "title": ["blog:post:title:required"],
    "slug": ["blog:post:slug:invalid_format"]
  }
}
```
