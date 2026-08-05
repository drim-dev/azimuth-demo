---
name: validation
description: Use when implementing form validation or input validation - provides full-stack validation patterns with FluentValidation (backend), Zod (frontend), and ProblemDetails error handling (project)
---

# Full-Stack Validation

All user input in drim-dev must be validated on both frontend (UX) and backend (security/truth).

## When to Use

Use this skill when:

- Creating forms that accept user input
- Implementing endpoints that receive data
- Adding file upload functionality
- Validating query parameters or route params
- Implementing business rule validation

## Validation Flow

```text
User fills form
  ↓
Zod validates (client-side, instant feedback)
  ↓
❌ Invalid → Show errors immediately (no API call)
  ↓
✅ Valid → Submit to BFF
  ↓
BFF passes through to Backend (no validation)
  ↓
FluentValidation validates (server-side, authoritative)
  ↓
❌ Invalid → Return ProblemDetails (400)
  ↓
✅ Valid → Execute business logic
  ↓
Return success response
```

**Key principle:** Frontend validates for UX, backend validates for security. Backend is always source of truth.

## Error Code Convention

Pattern: `domain:entity:field:error_type`

**Examples:**

- `blog:post:title:required`
- `blog:post:slug:invalid_format`
- `blog:post:slug:already_exists`
- `skills:skill:name:too_long`
- `users:email:invalid_format`
- `users:avatar:file_too_large`
- `courses:lesson:video:invalid_mime_type`

**Usage:**

- Backend returns error codes in ProblemDetails extensions
- Frontend maps error codes to i18n keys
- Fallback to English message if no translation

## Implementation Guides

For detailed patterns and examples, see:

- **[Backend Validation](backend-validation.md)** - FluentValidation patterns, async validation, file uploads, ProblemDetails integration
- **[Frontend Validation](frontend-validation.md)** - Zod schemas, React Hook Form, error handling, i18n
- **[Common Patterns](validation-patterns.md)** - Slug, email, URL, password, date validation across stack
- **[Testing](validation-testing.md)** - Validator unit tests with FluentValidation.TestHelper

## Quick Reference

### Backend (FluentValidation)

```csharp
public class RequestValidator : AbstractValidator<Request>
{
    public RequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .WithErrorCode("blog:post:title:required");

        RuleFor(x => x.Slug)
            .Matches("^[a-z0-9-]+$")
            .WithErrorCode("blog:post:slug:invalid_format");
    }
}
```

### Frontend (Zod)

```typescript
const schema = z.object({
  title: z.string().min(1, 'Title is required'),
  slug: z.string().regex(/^[a-z0-9-]+$/, 'Invalid slug format')
});

const form = useForm({
  resolver: zodResolver(schema)
});
```

### BFF Layer

**Passthrough pattern** - No validation in BFF, pass ProblemDetails through unchanged.

```typescript
// BFF just forwards request/response
const response = await fetch(`${process.env.BACKEND_URL}/api/posts`, {
  method: 'POST',
  body: await request.text()
});

return new Response(await response.text(), {
  status: response.status
});
```

## Testing Validation Rules

**⚠️ CRITICAL: EVERY feature with a RequestValidator MUST have validator unit tests.**

**If you create a RequestValidator without validator unit tests, you have failed.**

Validation rules must be tested in isolation using FluentValidation.TestHelper. **NEVER** test validation through component tests - create dedicated validator test classes.

**File organization:**
- Place validator tests class INSIDE component tests file as a nested class
- File location: `DrimDev.Api.Tests/Features/{Domain}/{FeatureName}Tests.cs`
- Example: `CreateSkillTests.cs` contains `CreateSkillTests` class with nested `ValidatorTests` class
- Add required using directives:
  - `using DrimDev.Api.Features.{Domain};` (to access feature classes like CreateSkill)
  - `using FluentValidation.TestHelper;` (for TestValidate() and assertion methods)

**Class naming convention:**
- Component tests: `{FeatureName}Tests`
- Validator tests (nested): `ValidatorTests` (nested inside component tests class)

**Examples of validator tests in the codebase:**
- `CreateSkillTests.ValidatorTests` - Comprehensive example with all validation rules
- `UpdateSkillTests.ValidatorTests` - Tests for UpdateSkill validator
- `EndorseUserSkillTests.ValidatorTests` - Tests for EndorseUserSkill validator
- `HandleOAuthCallbackTests.ValidatorTests` - Tests for OAuth callback validator

**See [validation-testing.md](validation-testing.md) for complete patterns:**
- Review examples of testing all validation rules
- Use `TestValidate()` for synchronous validators
- Use `TestValidateAsync()` for async validators (database checks)
- Test required fields, length limits, format patterns, and business rules
- Test both error cases AND success cases (use `ShouldHaveValidationErrorFor()` and `ShouldNotHaveValidationErrorFor()`)

## Summary Checklist

Before marking validation implementation complete:

**Backend:**

- [ ] FluentValidation rules defined in `RequestValidator`
- [ ] Error codes follow `domain:entity:field:error_type` convention
- [ ] Async validation uses `MustAsync` for database checks
- [ ] ValidationBehavior registered in MediatR pipeline
- [ ] ValidationExceptionHandler converts to ProblemDetails
- [ ] **Isolated validator unit tests created** (see CreatePostValidatorTests in validation-testing.md)

**Frontend:**

- [ ] Zod schemas mirror backend FluentValidation rules
- [ ] React Hook Form uses `zodResolver` with Zod schema
- [ ] Client-side validation provides instant feedback
- [ ] Server errors mapped from ProblemDetails to form fields
- [ ] Error codes mapped to i18n messages (if applicable)
- [ ] Frontend tests verify client and server error display

**BFF:**

- [ ] API routes pass requests through unchanged
- [ ] API routes preserve status codes from backend
- [ ] API routes return ProblemDetails unchanged
- [ ] No validation logic in BFF layer
