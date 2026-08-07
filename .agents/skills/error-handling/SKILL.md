---
name: error-handling
description: Use when implementing error handling for unexpected failures, authentication errors, not found errors, and business rule violations - provides full-stack error handling with ProblemDetails and proper HTTP status codes (project)
---

# Full-Stack Error Handling

All errors in drim-dev must be handled consistently using ProblemDetails format across backend, BFF, and frontend.

## When to Use

Use this skill when:

- Handling exceptions in backend (domain errors, infrastructure failures)
- Implementing authentication/authorization error responses
- Handling "not found" scenarios
- Managing business rule violations
- Creating error boundaries in frontend
- Implementing retry logic or error recovery

## Error Types and HTTP Status Codes

| Error Type | Status Code | When to Use |
|------------|-------------|-------------|
| Validation Error | 400 | Invalid user input (see validation skill) |
| Unauthorized | 401 | User not authenticated |
| Forbidden | 403 | User authenticated but lacks permission |
| Not Found | 404 | Resource doesn't exist |
| Conflict | 409 | State conflict (e.g., duplicate unique field) |
| Unprocessable Entity | 422 | Business rule violation |
| Internal Server Error | 500 | Unexpected server error |

## Error Code Convention

Pattern: `domain:entity:operation:error_type`

**Examples:**

- `blog:post:delete:not_found` - Post to delete doesn't exist
- `blog:post:publish:not_draft` - Cannot publish post that isn't draft
- `users:profile:update:forbidden` - User cannot update another user's profile
- `skills:skill:learn:already_learning` - User already learning this skill
- `courses:enrollment:create:course_full` - Course has reached max enrollments

## Implementation Guides

For detailed patterns and examples, see:

- **[Backend Error Handling](backend-errors.md)** - Domain exceptions, GlobalExceptionHandler, ProblemDetails integration, database errors
- **[Frontend Error Handling](frontend-errors.md)** - ApiError class, error boundaries, retry logic, component error handling
- **[Testing](error-testing.md)** - Testing error scenarios, status codes, error codes

## Quick Reference

### Backend Domain Exceptions

```csharp
// Define custom exceptions
public class NotFoundException : DomainException
{
    public NotFoundException(string message, string errorCode)
        : base(message, errorCode, StatusCodes.Status404NotFound)
    {
    }
}

// Use in handlers
if (post == null)
{
    throw new NotFoundException(
        $"Post with ID {request.PostId} not found",
        "blog:post:delete:not_found");
}
```

### Backend ProblemDetails Response

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

### BFF Passthrough

```typescript
// BFF passes ProblemDetails through unchanged
const response = await fetch(`${process.env.BACKEND_URL}/api/posts/${id}`, {
  method: 'DELETE',
  headers: {
    'Authorization': `Bearer ${session.accessToken}`,
    'X-User-Id': session.user.id
  }
});

// Return same status and body
return new Response(await response.text(), {
  status: response.status,
  headers: { 'Content-Type': 'application/json' }
});
```

### Frontend ApiError

```typescript
try {
  await api.delete(`/api/posts/${postId}`);
  toast.success('Post deleted successfully');
} catch (error) {
  if (error instanceof ApiError) {
    if (error.isNotFound) {
      toast.error('Post not found');
    } else if (error.isForbidden) {
      toast.error('You do not have permission to delete this post');
    } else {
      toast.error(error.message);
    }
  }
}
```

### Frontend Error Boundary

```typescript
<ErrorBoundary fallback={<ErrorPage />}>
  {children}
</ErrorBoundary>
```

## Summary Checklist

Before marking error handling implementation complete:

**Backend:**

- [ ] Domain exception classes created (NotFoundException, ForbiddenException, etc.)
- [ ] Error codes follow `domain:entity:operation:error_type` convention
- [ ] GlobalExceptionHandler registered and converts exceptions to ProblemDetails
- [ ] Not found errors throw NotFoundException with 404
- [ ] Authorization errors throw ForbiddenException with 403
- [ ] Business rule violations throw UnprocessableEntityException with 422
- [ ] Database errors handled (unique constraints, FK violations)
- [ ] Errors logged with appropriate level
- [ ] Component tests verify correct status codes and error codes

**BFF:**

- [ ] ProblemDetails passed through unchanged from backend
- [ ] BFF-specific errors (auth, backend unavailable) return ProblemDetails
- [ ] Network errors handled gracefully (503)

**Frontend:**

- [ ] ApiError class created for typed error handling
- [ ] API client throws ApiError with ProblemDetails
- [ ] Component error handling checks error types and codes
- [ ] Toast notifications show user-friendly messages
- [ ] Error boundary catches unexpected rendering errors
- [ ] Global error page (error.tsx) implemented
- [ ] Not found page (not-found.tsx) implemented
- [ ] Error codes mapped to i18n messages (if applicable)
- [ ] Frontend tests verify error display
