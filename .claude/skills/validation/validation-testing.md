# Validation Testing

## Backend Validator Unit Tests

Test validators in isolation for comprehensive coverage of all validation rules.

**File Organization:**
- Place validator tests class INSIDE component tests file as a nested class
- File location: `DrimDev.Api.Tests/Features/{Domain}/{FeatureName}Tests.cs`
- Example: `HandleOAuthCallbackTests.cs` contains `HandleOAuthCallbackTests` class with nested `ValidatorTests` class

**Class Naming:**
- Component tests: `{FeatureName}Tests`
- Validator tests (nested): `ValidatorTests` (nested inside component tests class)

**Use FluentValidation.TestHelper** for concise validation testing:

```csharp
// Features/Blog/CreatePostValidatorTests.cs
using FluentValidation.TestHelper;

namespace DrimDev.Api.Tests.Features.Blog;

public class CreatePostValidatorTests
{
    private readonly CreatePost.RequestValidator _validator = new();

    [Fact]
    public void Should_not_have_errors_when_request_is_valid()
    {
        // Arrange
        var request = new CreatePost.Request(
            Title: "Test Post",
            Slug: "test-post",
            Content: "Test content"
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Should_have_error_when_title_empty(string title)
    {
        // Arrange
        var request = new CreatePost.Request(
            Title: title,
            Slug: "test",
            Content: "content"
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorCode("blog:post:title:required");
    }

    [Fact]
    public void Should_have_error_when_title_too_long()
    {
        // Arrange
        var request = new CreatePost.Request(
            Title: new string('a', 201),  // Exceeds 200 char limit
            Slug: "test",
            Content: "content"
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorCode("blog:post:title:too_long");
    }

    [Theory]
    [InlineData("Invalid Slug")]      // Spaces not allowed
    [InlineData("Invalid_Slug")]      // Underscores not allowed
    [InlineData("INVALID-SLUG")]      // Uppercase not allowed
    [InlineData("invalid--slug")]     // Multiple consecutive hyphens
    public void Should_have_error_when_slug_invalid_format(string slug)
    {
        // Arrange
        var request = new CreatePost.Request(
            Title: "Test",
            Slug: slug,
            Content: "content"
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorCode("blog:post:slug:invalid_format");
    }
}
```

## Async Validator Tests

For async validators (uniqueness checks requiring DbContext):

```csharp
// Features/Blog/CreatePostValidatorTests.cs
public class CreatePostValidatorTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;
    private CreatePost.RequestValidator _validator = null!;

    public CreatePostValidatorTests(TestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.Reset();
        _validator = new CreatePost.RequestValidator(_fixture.DbContext);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_have_error_when_slug_already_exists()
    {
        // Arrange
        var existingPost = CreatePost(slug: "existing-slug");
        await _fixture.Database.Save(existingPost);

        var request = new CreatePost.Request(
            Title: "New Post",
            Slug: "existing-slug",
            Content: "content"
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorCode("blog:post:slug:already_exists");
    }

    [Fact]
    public async Task Should_not_have_error_when_slug_unique()
    {
        // Arrange
        var request = new CreatePost.Request(
            Title: "New Post",
            Slug: "unique-slug",
            Content: "content"
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }
}
```

## Why Isolated Validator Tests Are Sufficient

- Validators are pure logic - no need for HTTP integration
- FluentValidation integration with MediatR is framework code (already tested)
- Fast feedback loop for all validation rules
- Easy to test edge cases and boundary conditions
- Component tests should focus on business logic, not validation rules
