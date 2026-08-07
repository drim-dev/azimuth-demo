# Component Testing Examples

This document contains complete test examples for various scenarios.

## Complete Test Class Example

```csharp
using System.Net;
using FluentAssertions;
using FluentAssertions.Extensions;
using RestSharp;
using YourApp.Domain;
using YourApp.Features.Auth;
using YourApp.Tests.Contracts;
using YourApp.Tests.Extensions;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Auth;

[Collection(AuthTestsCollection.Name)]
public class CreateAccountTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public CreateAccountTests(TestFixture fixture) => _fixture = fixture;

    // Reset state before each test (fast with Respawn)
    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());

    public Task DisposeAsync() => Task.CompletedTask;

    // Helper method to execute the feature under test
    private async Task<RestResponse<T>> Act<T>(CreateAccountRequestContract request)
    {
        var client = new RestClient(_fixture.HttpClient.CreateClient());
        return await client.ExecutePostAsync<T>(
            "/auth/accounts",
            request,
            CreateCancellationToken());
    }

    [Fact]
    public async Task Should_create_account()
    {
        // Arrange
        const string login = "Sam";
        var request = new CreateAccountRequestContract(login, "Qwer1234!");

        // Act - Test through HTTP endpoint
        var restResponse = await Act<AccountContract>(request);

        // Assert HTTP response
        restResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseAccount = restResponse.Data;
        responseAccount.ShouldNotBeNull();

        restResponse.Headers.Location().Should().Be($"/auth/accounts/{responseAccount.Login}");

        responseAccount.Login.Should().Be(login.ToLower());
        responseAccount.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 1.Seconds());

        // Assert database state via harness
        var dbAccount = await _fixture.Database.SingleOrDefault<Account>(
            x => x.Login == responseAccount.Login,
            CreateCancellationToken());

        dbAccount.ShouldNotBeNull();
        dbAccount.Id.Should().BeGreaterOrEqualTo(0);
        dbAccount.Login.Should().Be(login.ToLower());
        dbAccount.CreatedAt.Should().BeCloseTo(responseAccount.CreatedAt, 100.Microseconds());
        dbAccount.PasswordHash.Should().NotBeEmpty();
        dbAccount.PasswordHash.Split('$').Should().HaveCount(6);
    }

    [Theory]
    [InlineData("sam")]
    [InlineData("Sam")]
    public async Task Should_return_conflict_if_account_exists_case_insensitive(string login)
    {
        // Arrange - Seed existing account via harness
        await _fixture.Database.Save(CreateAccount(login));

        var request = new CreateAccountRequestContract(login, "Qwer1234!");

        // Act
        var restResponse = await Act<ProblemDetailsContract>(request);

        // Assert
        restResponse.ShouldBeLogicConflictError(
            "Account already exists",
            "auth:logic:account_already_exists");
    }

    [Theory]
    [InlineData("ab", "Qwer1234!")] // Login too short
    [InlineData("Sam", "weak")] // Password too weak
    public async Task Should_return_validation_error_for_invalid_input(string login, string password)
    {
        // Arrange
        var request = new CreateAccountRequestContract(login, password);

        // Act
        var restResponse = await Act<ProblemDetailsContract>(request);

        // Assert
        restResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        restResponse.Data.Should().NotBeNull();
        restResponse.Data.Errors.Should().NotBeEmpty();
    }
}
```

## Testing with Authentication

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using YourApp.Domain;
using YourApp.Features.Posts;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Posts;

[Collection(PostsTestsCollection.Name)]
public class UpdatePostTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public UpdatePostTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_update_own_post()
    {
        // Arrange - Create authenticated user and their post
        var (client, account) = await _fixture.CreateAuthedHttpClient();
        var post = CreatePost(authorId: account.Id, slug: "my-post");
        await _fixture.Database.Save(post);

        var request = new UpdatePostRequestContract(
            Title: "Updated Title",
            Content: "Updated content");

        // Act
        var response = await client.PutAsJsonAsync(
            $"/posts/{post.Slug}",
            request,
            CreateCancellationToken());

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert database state
        var updatedPost = await _fixture.Database.SingleOrDefault<Post>(
            x => x.Slug == post.Slug,
            CreateCancellationToken());

        updatedPost.Should().NotBeNull();
        updatedPost.Title.Should().Be("Updated Title");
        updatedPost.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task Should_not_update_others_post()
    {
        // Arrange - Create two users, post belongs to user1
        var account1 = CreateAccount(login: "user1");
        var account2 = CreateAccount(login: "user2");
        await _fixture.Database.Save(account1, account2);

        var post = CreatePost(authorId: account1.Id, slug: "user1-post");
        await _fixture.Database.Save(post);

        // Authenticate as user2
        var jwt = await _fixture.WithService<JwtGenerator>(gen => gen.Generate(account2));
        var client = _fixture.HttpClient.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);

        var request = new UpdatePostRequestContract(
            Title: "Hacked Title",
            Content: "Hacked content");

        // Act
        var response = await client.PutAsJsonAsync(
            $"/posts/{post.Slug}",
            request,
            CreateCancellationToken());

        // Assert - Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert database unchanged
        var unchangedPost = await _fixture.Database.SingleOrDefault<Post>(
            x => x.Slug == post.Slug,
            CreateCancellationToken());

        unchangedPost.Should().NotBeNull();
        unchangedPost.Title.Should().Be(post.Title); // Unchanged
    }

    [Fact]
    public async Task Should_require_authentication()
    {
        // Arrange - Create post
        var post = CreatePost(slug: "test-post");
        await _fixture.Database.Save(post);

        var request = new UpdatePostRequestContract(
            Title: "Updated",
            Content: "Updated");

        // Act - No authentication
        var client = _fixture.HttpClient.CreateClient();
        var response = await client.PutAsJsonAsync(
            $"/posts/{post.Slug}",
            request,
            CreateCancellationToken());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_reject_expired_token()
    {
        // Arrange
        var (client, account) = await _fixture.CreateExpiredTokenHttpClient();
        var post = CreatePost(authorId: account.Id, slug: "my-post");
        await _fixture.Database.Save(post);

        var request = new UpdatePostRequestContract("Updated", "Updated");

        // Act
        var response = await client.PutAsJsonAsync(
            $"/posts/{post.Slug}",
            request,
            CreateCancellationToken());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

## Testing Role-Based Authorization

```csharp
using System.Net;
using FluentAssertions;
using YourApp.Domain;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Admin;

[Collection(AdminTestsCollection.Name)]
public class DeleteUserTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public DeleteUserTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Admin_should_delete_user()
    {
        // Arrange - Create admin and regular user
        var (adminClient, _) = await _fixture.CreateAdminHttpClient();

        var targetUser = CreateAccount(login: "targetuser");
        await _fixture.Database.Save(targetUser);

        // Act
        var response = await adminClient.DeleteAsync(
            $"/admin/users/{targetUser.Id}",
            CreateCancellationToken());

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert user deleted from database
        var deletedUser = await _fixture.Database.SingleOrDefault<Account>(
            x => x.Id == targetUser.Id,
            CreateCancellationToken());

        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task Regular_user_should_not_delete_user()
    {
        // Arrange - Regular user tries to delete another user
        var (client, _) = await _fixture.CreateAuthedHttpClient(); // Regular user

        var targetUser = CreateAccount(login: "targetuser");
        await _fixture.Database.Save(targetUser);

        // Act
        var response = await client.DeleteAsync(
            $"/admin/users/{targetUser.Id}",
            CreateCancellationToken());

        // Assert - Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert user still exists
        var existingUser = await _fixture.Database.SingleOrDefault<Account>(
            x => x.Id == targetUser.Id,
            CreateCancellationToken());

        existingUser.Should().NotBeNull();
    }
}
```

## Testing Pagination

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using YourApp.Domain;
using YourApp.Features.Posts;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Posts;

[Collection(PostsTestsCollection.Name)]
public class ListPostsTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public ListPostsTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_return_first_page()
    {
        // Arrange - Seed 25 posts
        var posts = Enumerable.Range(1, 25)
            .Select(i => CreatePost(slug: $"post-{i:D2}"))
            .ToList();
        await _fixture.Database.Save(posts);

        // Act
        var client = _fixture.HttpClient.CreateClient();
        var response = await client.GetFromJsonAsync<PostsListContract>(
            "/posts?page=1&pageSize=10",
            CreateCancellationToken());

        // Assert
        response.Should().NotBeNull();
        response.Posts.Should().HaveCount(10);
        response.TotalCount.Should().Be(25);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Should_return_second_page()
    {
        // Arrange
        var posts = Enumerable.Range(1, 25)
            .Select(i => CreatePost(slug: $"post-{i:D2}"))
            .ToList();
        await _fixture.Database.Save(posts);

        // Act
        var client = _fixture.HttpClient.CreateClient();
        var response = await client.GetFromJsonAsync<PostsListContract>(
            "/posts?page=2&pageSize=10",
            CreateCancellationToken());

        // Assert
        response.Should().NotBeNull();
        response.Posts.Should().HaveCount(10);
        response.Page.Should().Be(2);
    }

    [Fact]
    public async Task Should_return_empty_page_when_no_posts()
    {
        // Arrange - No posts seeded

        // Act
        var client = _fixture.HttpClient.CreateClient();
        var response = await client.GetFromJsonAsync<PostsListContract>(
            "/posts?page=1&pageSize=10",
            CreateCancellationToken());

        // Assert
        response.Should().NotBeNull();
        response.Posts.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }
}
```

## Testing Query Features

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using YourApp.Domain;
using YourApp.Features.Posts;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Posts;

[Collection(PostsTestsCollection.Name)]
public class GetPostTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public GetPostTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_return_post_by_slug()
    {
        // Arrange
        var post = CreatePost(
            slug: "my-first-post",
            title: "My First Post",
            content: "Post content");
        await _fixture.Database.Save(post);

        // Act
        var client = _fixture.HttpClient.CreateClient();
        var response = await client.GetFromJsonAsync<PostContract>(
            "/posts/my-first-post",
            CreateCancellationToken());

        // Assert
        response.Should().NotBeNull();
        response.Slug.Should().Be("my-first-post");
        response.Title.Should().Be("My First Post");
        response.Content.Should().Be("Post content");
    }

    [Fact]
    public async Task Should_return_not_found_for_nonexistent_post()
    {
        // Arrange - No posts seeded

        // Act
        var client = _fixture.HttpClient.CreateClient();
        var response = await client.GetAsync(
            "/posts/nonexistent-slug",
            CreateCancellationToken());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_return_not_found_for_unpublished_post()
    {
        // Arrange - Unpublished post
        var post = CreatePost(
            slug: "draft-post",
            isPublished: false);
        await _fixture.Database.Save(post);

        // Act
        var client = _fixture.HttpClient.CreateClient();
        var response = await client.GetAsync(
            "/posts/draft-post",
            CreateCancellationToken());

        // Assert - Should not expose unpublished posts
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

## Testing Complex Business Logic

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.EntityFrameworkCore;
using YourApp.Domain;
using YourApp.Tests.Contracts;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Courses;

[Collection(CourseTestsCollection.Name)]
public class EnrollInCourseTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public EnrollInCourseTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_enroll_user_in_course()
    {
        // Arrange
        var (client, account) = await _fixture.CreateAuthedHttpClient();
        var course = CreateCourse(title: "Test Course");
        await _fixture.Database.Save(course);

        // Act
        var response = await client.PostAsync(
            $"/courses/{course.Id}/enroll",
            null,
            CreateCancellationToken());

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert enrollment created
        var enrollment = await _fixture.Database.SingleOrDefault<Enrollment>(
            x => x.CourseId == course.Id && x.UserId == account.Id,
            CreateCancellationToken());

        enrollment.Should().NotBeNull();
        enrollment.EnrolledAt.Should().BeCloseTo(DateTime.UtcNow, 1.Seconds());
        enrollment.Progress.Should().Be(0);
    }

    [Fact]
    public async Task Should_not_enroll_twice_in_same_course()
    {
        // Arrange - User already enrolled
        var (client, account) = await _fixture.CreateAuthedHttpClient();
        var course = CreateCourse();
        var existingEnrollment = CreateEnrollment(account.Id, course.Id);
        await _fixture.Database.Save(course, existingEnrollment);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/courses/{course.Id}/enroll",
            null,
            CreateCancellationToken());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Assert only one enrollment exists
        var enrollmentCount = await _fixture.Database.Execute(async db =>
            await db.Enrollments.CountAsync(x =>
                x.CourseId == course.Id && x.UserId == account.Id));

        enrollmentCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_not_enroll_when_course_is_full()
    {
        // Arrange - Course at max capacity
        var (client, account) = await _fixture.CreateAuthedHttpClient();
        var course = CreateCourse(maxStudents: 2);

        var otherUsers = new[]
        {
            CreateAccount(login: "user1"),
            CreateAccount(login: "user2")
        };
        await _fixture.Database.Save(course);
        await _fixture.Database.Save(otherUsers);

        var enrollments = otherUsers.Select(u =>
            CreateEnrollment(u.Id, course.Id)).ToList();
        await _fixture.Database.Save(enrollments);

        // Act
        var response = await client.PostAsync(
            $"/courses/{course.Id}/enroll",
            null,
            CreateCancellationToken());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        problemDetails.Detail.Should().Contain("course is full");
    }
}
```

## Testing with Multiple Harnesses (Redis Cache)

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using YourApp.Domain;
using YourApp.Features.Posts;
using YourApp.Tests.Contracts;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Posts;

[Collection(PostsTestsCollection.Name)]
public class GetCachedPostTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public GetCachedPostTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_cache_post_after_first_request()
    {
        // Arrange
        var post = CreatePost(slug: "cached-post");
        await _fixture.Database.Save(post);

        var client = _fixture.HttpClient.CreateClient();

        // Act - First request (cache miss)
        var response1 = await client.GetFromJsonAsync<PostContract>(
            "/posts/cached-post",
            CreateCancellationToken());

        // Assert cached in Redis via harness
        var cachedPost = await _fixture.Redis.Get<PostContract>("post:cached-post");
        cachedPost.Should().NotBeNull();
        cachedPost.Slug.Should().Be("cached-post");

        // Act - Second request (cache hit)
        var response2 = await client.GetFromJsonAsync<PostContract>(
            "/posts/cached-post",
            CreateCancellationToken());

        // Assert same data returned
        response2.Should().BeEquivalentTo(response1);
    }

    [Fact]
    public async Task Should_invalidate_cache_on_update()
    {
        // Arrange - Cached post
        var (client, account) = await _fixture.CreateAuthedHttpClient();
        var post = CreatePost(authorId: account.Id, slug: "my-post");
        await _fixture.Database.Save(post);

        // Cache the post
        await _fixture.Redis.Set("post:my-post", new PostContract(post.Slug, post.Title));

        // Act - Update post
        var updateRequest = new UpdatePostRequestContract("New Title", "New content");
        await client.PutAsJsonAsync($"/posts/{post.Slug}", updateRequest);

        // Assert cache invalidated
        var cachedPost = await _fixture.Redis.Get<PostContract>("post:my-post");
        cachedPost.Should().BeNull();
    }
}
```

## Testing with Message Queue (Kafka)

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using YourApp.Features.Notifications;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Notifications;

[Collection(NotificationsTestsCollection.Name)]
public class SendNotificationTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public SendNotificationTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_publish_notification_to_kafka()
    {
        // Arrange
        var (client, account) = await _fixture.CreateAuthedHttpClient();
        var request = new SendNotificationRequestContract(
            RecipientId: 123,
            Message: "Hello!");

        // Act
        var response = await client.PostAsJsonAsync(
            "/notifications/send",
            request,
            CreateCancellationToken());

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert message published to Kafka via harness
        var messages = await _fixture.Kafka.ConsumeMessages<NotificationMessage>(
            topic: "notifications",
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(5));

        messages.Should().HaveCount(1);
        messages[0].RecipientId.Should().Be(123);
        messages[0].Message.Should().Be("Hello!");
    }
}
```

## Helper Methods in Test Classes

```csharp
using FluentAssertions;
using RestSharp;
using YourApp.Domain;
using YourApp.Features.Posts;
using YourApp.Tests.Fixtures;

namespace YourApp.Tests.Features.Posts;

[Collection(PostsTestsCollection.Name)]
public class PostTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public PostTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    // Reusable Act method
    private async Task<RestResponse<T>> CreatePost<T>(CreatePostRequestContract request)
    {
        var client = new RestClient(_fixture.HttpClient.CreateClient());
        return await client.ExecutePostAsync<T>("/posts", request, CreateCancellationToken());
    }

    // Reusable entity creation
    private Post CreatePost(
        string slug = "test-post",
        string title = "Test Post",
        string content = "Test content",
        long? authorId = null,
        bool isPublished = true)
    {
        return new Post(
            Id: 0,
            AuthorId: authorId ?? 1,
            Title: title,
            Slug: slug,
            Content: content,
            IsPublished: isPublished,
            CreatedAt: DateTime.UtcNow);
    }

    // Tests use helper methods...
}
```

## Summary

**Component test structure:**
1. **Arrange** - Seed data using harness methods
2. **Act** - Call HTTP endpoint
3. **Assert** - Verify HTTP response and side effects

**Common patterns:**
- Reset fixture in `IAsyncLifetime.InitializeAsync()`
- Create helper `Act<T>()` methods
- Use fixture helper methods for auth
- Assert both response and database state
- Test authorization and validation
- Use harnesses for all external dependencies
