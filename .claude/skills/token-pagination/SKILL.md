---
name: token-pagination
description: Use when implementing list endpoints that return paginated results - provides AIP-158 compliant token-based pagination with encrypted tokens and query validation (project)
---

# Token-Based Pagination (AIP-158)

All list endpoints in drim-dev MUST use token-based pagination following Google AIP-158.

## When to Use

Use this pattern for **every endpoint that returns a list of items**:

- List skills, courses, blog posts
- User activity feeds
- Search results
- Admin tables
- Any collection endpoint

## Request Parameters

```csharp
app.MapGet("/posts", async Task<Ok<PageResponse<PostModel>>> (
    [FromQuery] string? pageToken,      // Opaque continuation token
    [FromQuery] int? maxPageSize,       // Max items per page
    [FromQuery] bool? published,        // Your filter params...
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var response = await sender.Send(
        new Request(pageToken, maxPageSize, published),
        cancellationToken);
    return TypedResults.Ok(response);
});
```

**Parameters:**

- `pageToken` (string?, optional) - Encrypted token from previous response, null/empty for first page
- `maxPageSize` (int?, optional) - Max results per page, defaults to configured value (typically 10-50)
- **All filter/search params** - Must be included in request for hash validation

## Response Structure

```csharp
public record PageResponse<T>(T[] Items, string? NextPageToken);
```

**Rules:**

- `Items` - Array of results for current page
- `NextPageToken` - null if last page, otherwise encrypted token for next page

## Implementation Pattern

### 1. Handler Setup

```csharp
public class RequestHandler : IRequestHandler<Request, PageResponse<PostModel>>
{
    private readonly WebApiDbContext _db;
    private readonly LimitOffsetPaging _paging;

    public RequestHandler(WebApiDbContext db, LimitOffsetPaging paging)
    {
        _db = db;
        _paging = paging;
    }

    public async Task<PageResponse<PostModel>> Handle(Request request, CancellationToken ct)
    {
        // Step 1: Validate maxPageSize
        if (!_paging.TryGetMaxPageSize(request.MaxPageSize, out var maxPageSize))
        {
            throw PaginationExceptions.InvalidMaxPageSize();
        }

        // Step 2: Decode pageToken and validate query params
        if (!_paging.TryGetOffsetAndLimit(
            request.PageToken,
            maxPageSize,
            out var offset,
            out var limit,
            request.Published))  // ⚠️ CRITICAL: Pass ALL filter params
        {
            throw PaginationExceptions.InvalidPageToken();
        }

        // Step 3: Build query with filters
        var query = _db.Posts.AsNoTracking();

        if (request.Published is not null)
        {
            query = query.Where(x => x.Published == request.Published.Value);
        }

        // Step 4: Execute query with pagination
        // ⚠️ CRITICAL: Must OrderBy stable field (usually Id)
        var items = await query
            .OrderBy(p => p.Id)           // Stable ordering required
            .Skip(offset!.Value)
            .Take(limit!.Value)
            .Select(p => new PostModel(...))
            .ToArrayAsync(ct);

        // Step 5: Create next page token
        var nextPageToken = _paging.CreateNextPageToken(
            items.Length,
            offset.Value,
            limit.Value,
            request.Published);  // ⚠️ CRITICAL: Pass same filter params

        return new PageResponse<PostModel>(items, nextPageToken);
    }
}
```

## Critical Rules

### 1. Always Pass ALL Query Parameters to Hashing

The pagination system validates that tokens are used with the same query parameters via hash.

**✅ Correct:**

```csharp
// Decoding token
_paging.TryGetOffsetAndLimit(
    request.PageToken,
    maxPageSize,
    out var offset,
    out var limit,
    request.Published,    // All filter params
    request.Category,
    request.SearchQuery)

// Creating next token
_paging.CreateNextPageToken(
    items.Length,
    offset.Value,
    limit.Value,
    request.Published,    // Same filter params in same order
    request.Category,
    request.SearchQuery)
```

**❌ Wrong:**

```csharp
// Missing filter params - token will be invalid when filters change
_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit)
```

**Why:** Prevents users from reusing a page token with different filters, which would return wrong results.

### 2. Always Order By Stable Field

**✅ Correct:**

```csharp
var items = await query
    .OrderBy(p => p.Id)              // Stable, unique ordering
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);
```

**❌ Wrong:**

```csharp
// No ordering - results will be unpredictable
var items = await query
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);

// Ordering by non-unique field - pagination will skip/duplicate items
var items = await query
    .OrderBy(p => p.Category)
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);
```

**Why:** Without stable ordering, offset pagination returns inconsistent results when data changes.

### 3. Validate in Correct Order

**✅ Correct:**

```csharp
// 1. Validate maxPageSize first
if (!_paging.TryGetMaxPageSize(request.MaxPageSize, out var maxPageSize))
    throw PaginationExceptions.InvalidMaxPageSize();

// 2. Then validate and decode pageToken
if (!_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit))
    throw PaginationExceptions.InvalidPageToken();
```

**❌ Wrong:**

```csharp
// Validating pageToken before maxPageSize
if (!_paging.TryGetOffsetAndLimit(request.PageToken, request.MaxPageSize ?? 10, ...))
```

**Why:** maxPageSize validation must happen first to ensure we have a valid page size before decoding token.

### 4. Return Null Next Token on Last Page

The `CreateNextPageToken` method automatically returns `null` when `count < limit`, indicating the last page.

```csharp
var nextPageToken = _paging.CreateNextPageToken(
    items.Length,      // If this is less than limit, returns null
    offset.Value,
    limit.Value,
    request.Published);

// nextPageToken will be null if items.Length < limit.Value
```

**Never manually set `nextPageToken = null`** - let the helper method handle this logic.

## Common Mistakes

### Mistake 1: Forgetting Query Parameters in Token Methods

```csharp
// ❌ WRONG - Missing published filter in hash
if (!_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit))
    throw PaginationExceptions.InvalidPageToken();

var query = _db.Posts.Where(x => x.Published == request.Published);
// ... pagination ...

var nextPageToken = _paging.CreateNextPageToken(items.Length, offset.Value, limit.Value);

// Problem: User can reuse page 2 token with different filter, getting wrong results
```

**Fix:** Always pass all query parameters to both methods.

### Mistake 2: Ordering By Non-Unique or Unstable Fields

```csharp
// ❌ WRONG - Category is not unique
var items = await query
    .OrderBy(p => p.Category)
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);

// Problem: Multiple posts with same category will have unpredictable order
```

**Fix:** Order by unique, stable field (usually `Id`). For custom sorting, use compound ordering:

```csharp
// ✅ CORRECT - Custom sort with stable tiebreaker
var items = await query
    .OrderBy(p => p.Category)
    .ThenBy(p => p.Id)           // Stable tiebreaker
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);
```

### Mistake 3: Not Handling Null PageToken

```csharp
// ❌ WRONG - Assuming pageToken is always present
var offset = DecodeToken(request.PageToken);  // Crashes on first page
```

**Fix:** The `TryGetOffsetAndLimit` method handles null tokens automatically:

```csharp
// ✅ CORRECT
if (!_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit))
    throw PaginationExceptions.InvalidPageToken();

// If pageToken is null/empty, offset will be 0 (first page)
```

### Mistake 4: Using Different Parameter Order

```csharp
// ❌ WRONG - Different parameter order in decode vs create
_paging.TryGetOffsetAndLimit(
    request.PageToken, maxPageSize, out var offset, out var limit,
    request.Published, request.Category)

// Later...
var nextPageToken = _paging.CreateNextPageToken(
    items.Length, offset.Value, limit.Value,
    request.Category, request.Published)  // ⚠️ Swapped order!

// Problem: Hash will be different, token validation will fail
```

**Fix:** Keep query parameters in EXACT same order for both method calls.

## Configuration

Pagination behavior is configured in `appsettings.json`:

```json
{
  "Paging": {
    "TokenEncryptionKeyInBase64": "...",  // 32-byte AES key
    "TokenIvInBase64": "...",             // 16-byte IV
    "DefaultMaxPageSize": 10,             // Default if not specified
    "MaxMaxPageSize": 100                 // Upper limit
  }
}
```

**Rules:**

- Client can request any `maxPageSize` up to `MaxMaxPageSize`
- If client exceeds limit, request is rejected with `InvalidMaxPageSize` error
- If client omits `maxPageSize`, defaults to `DefaultMaxPageSize`

## Error Handling

```csharp
// Invalid maxPageSize (negative, zero, or exceeds max)
throw PaginationExceptions.InvalidMaxPageSize();
// Returns: 400 Bad Request with error code "paging:validation:max_page_size_invalid"

// Invalid pageToken (malformed, expired, wrong query params)
throw PaginationExceptions.InvalidPageToken();
// Returns: 400 Bad Request with error code "paging:validation:page_token_invalid"
```

## Security

**Token encryption:**

- Page tokens contain offset and query parameter hash
- Encrypted with AES-256 using configured key and IV
- Encoded with Crockford Base32 for URL safety
- Users cannot read or tamper with tokens

**Query validation:**

- Token includes SHA-256 hash of all query parameters
- Prevents token reuse with different filters
- If filters change, token is rejected

## Testing Pagination

When testing paginated endpoints:

```csharp
[Fact]
public async Task Should_paginate_posts()
{
    // Arrange - Create 25 posts
    var posts = Enumerable.Range(1, 25)
        .Select(i => CreatePost(name: $"Post {i}"))
        .ToArray();
    await _fixture.Database.Save(posts);

    var client = _fixture.CreateClient();

    // Act - Get first page
    var page1 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        "/posts?maxPageSize=10");

    // Assert - First page
    page1.ShouldNotBeNull();
    page1.Items.Should().HaveCount(10);
    page1.NextPageToken.Should().NotBeNullOrEmpty();

    // Act - Get second page
    var page2 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        $"/posts?maxPageSize=10&pageToken={page1.NextPageToken}");

    // Assert - Second page
    page2.ShouldNotBeNull();
    page2.Items.Should().HaveCount(10);
    page2.NextPageToken.Should().NotBeNullOrEmpty();

    // Act - Get third page (last)
    var page3 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        $"/posts?maxPageSize=10&pageToken={page2.NextPageToken}");

    // Assert - Last page
    page3.ShouldNotBeNull();
    page3.Items.Should().HaveCount(5);  // Only 5 items left
    page3.NextPageToken.Should().BeNullOrEmpty();  // No more pages

    // Assert - No duplicate items across pages
    var allIds = page1.Items
        .Concat(page2.Items)
        .Concat(page3.Items)
        .Select(p => p.Id)
        .ToArray();
    allIds.Should().OnlyHaveUniqueItems();
}

[Fact]
public async Task Should_reject_token_when_filters_change()
{
    // Arrange
    var client = _fixture.CreateClient();

    var page1 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        "/posts?published=true&maxPageSize=10");

    // Act - Reuse token with different filter
    var response = await client.GetAsync(
        $"/posts?published=false&maxPageSize=10&pageToken={page1.NextPageToken}");

    // Assert - Token rejected
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

## Summary Checklist

Before marking pagination implementation complete:

- [ ] Request has `pageToken` and `maxPageSize` parameters
- [ ] Response uses `PageResponse<T>` with `Items` and `NextPageToken`
- [ ] Handler validates `maxPageSize` first, then `pageToken`
- [ ] ALL query/filter parameters passed to `TryGetOffsetAndLimit`
- [ ] Same query parameters passed to `CreateNextPageToken` in same order
- [ ] Query uses `.OrderBy(x => x.Id)` or stable compound ordering
- [ ] Query uses `.Skip(offset.Value).Take(limit.Value)`
- [ ] Tests verify pagination works across multiple pages
- [ ] Tests verify no duplicate items across pages
- [ ] Tests verify `nextPageToken` is null on last page
- [ ] Tests verify token rejected when filters change
