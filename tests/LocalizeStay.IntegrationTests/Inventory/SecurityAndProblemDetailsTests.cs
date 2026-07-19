using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Extensions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Contract-level coverage for the F01 cross-cutting host pipeline: JWT/policy enforcement, RFC 9457
/// Problem Details field by field, rate limiting and absence of sensitive data. These tests are the
/// evidence required by task 1.0's success criteria (dotnet-testing baseline: AAA + naming
/// MethodName_Condition_ExpectedBehavior).
/// </summary>
public sealed class SecurityAndProblemDetailsTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LocalizeStayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SecurityAndProblemDetailsTests(LocalizeStayWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutJwt_Returns401WithUnauthorizedProblemDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/ok");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
        problem.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("instance").GetString().Should().Be("/api/v1/test/scenarios/ok");
        problem.TryGetProperty("traceId", out _).Should().BeTrue("traceId is required on every Problem Details payload");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithJwtButWithoutPermission_Returns403WithForbiddenProblemDetails()
    {
        // Arrange: token carries staff scope but no portfolio-onboarding:read permission.
        var token = LocalizeStayWebApplicationFactory.CreateToken("logto|staff-0192");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/ok");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("code").GetString().Should().Be("FORBIDDEN");
        problem.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("instance").GetString().Should().Be("/api/v1/test/scenarios/ok");
        problem.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithJwtAndReadPermission_Returns200()
    {
        // Arrange
        var token = LocalizeStayWebApplicationFactory.CreateToken(
            "logto|staff-0192",
            PortfolioOnboardingPermissions.Read);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/ok");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NotFoundScenario_Returns404WithContractCodeAndInstance()
    {
        // Arrange
        await AuthorizeAsReaderAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/notfound");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("code").GetString().Should().Be("PROPERTY_ONBOARDING_NOT_FOUND");
        problem.GetProperty("instance").GetString().Should().Be("/api/v1/test/scenarios/notfound");
        problem.GetProperty("type").GetString().Should().StartWith("https://api.localizestay.com/problems/");
        problem.GetProperty("errors").GetArrayLength().Should().Be(0);
        problem.GetProperty("metadata").GetobjectLength().Should().Be(0);
    }

    [Fact]
    public async Task ConflictScenario_Returns409WithConflictingResourceIdInMetadata()
    {
        // Arrange
        await AuthorizeAsReaderAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/conflict");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("code").GetString().Should().Be("DUPLICATE_LEGAL_IDENTIFIER");
        problem.GetProperty("metadata").GetProperty("conflictingResourceId").GetString().Should()
            .Be("6b22179c-0143-4a70-97d3-c9648d77666a");
        problem.GetProperty("errors").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task BusinessRuleScenario_Returns422WithContractCode()
    {
        // Arrange
        await AuthorizeAsReaderAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/rule");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(422);
        problem.GetProperty("code").GetString().Should().Be("ONBOARDING_NOT_READY");
        problem.GetProperty("instance").GetString().Should().Be("/api/v1/test/scenarios/rule");
        problem.GetProperty("errors").GetArrayLength().Should().Be(0);
        problem.GetProperty("metadata").GetobjectLength().Should().Be(0);
    }

    [Fact]
    public async Task ValidationScenario_Returns400WithStructuredErrors()
    {
        // Arrange
        await AuthorizeAsReaderAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/validation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("code").GetString().Should().Be("BAD_REQUEST");

        var errors = problem.GetProperty("errors");
        errors.GetArrayLength().Should().BeGreaterThan(0);
        var firstError = errors.EnumerateArray().First();
        firstError.GetProperty("field").GetString().Should().Be("legalIdentifier.value");
        firstError.GetProperty("code").GetString().Should().Be("INVALID_LEGAL_IDENTIFIER");
        firstError.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CrashScenario_Returns500WithoutSensitiveData()
    {
        // Arrange
        await AuthorizeAsReaderAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/crash");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("hunter2", "secrets must never leak through Problem Details");
        raw.Should().NotContain("111.222.333-44", "PII such as CPF must never leak through Problem Details");
        raw.Should().NotContain("InvalidOperationException", "exception type must not leak through Problem Details");
        raw.Should().NotContain("at LocalizeStay", "stack traces must not leak through Problem Details");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(500);
        problem.GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
        problem.GetProperty("detail").GetString().Should().NotContain("hunter2");
        problem.GetProperty("detail").GetString().Should().NotContain("111");
    }

    [Fact]
    public async Task RateLimit_Exceeded_Returns429WithProblemDetailsBody()
    {
        // Arrange: build a dedicated host with a one-token bucket so the second request fails before
        // the limiter can refill. The original factory's Testcontainer is reused via WithWebHostBuilder.
        var limitedClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:PermitLimit"] = "1",
                    ["RateLimit:TokensPerSecond"] = "1",
                    ["RateLimit:QueueLimit"] = "0",
                });
            });
        }).CreateClient();

        var token = LocalizeStayWebApplicationFactory.CreateToken(
            "logto|ratelimit-001",
            PortfolioOnboardingPermissions.Read);
        limitedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act: fire requests in parallel to maximize the chance of bucket exhaustion.
        var tasks = Enumerable.Range(0, 6)
            .Select(_ => limitedClient.GetAsync("/api/v1/test/scenarios/ok"))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests,
            "with PermitLimit=1 and QueueLimit=0 at least one parallel request must be rejected");

        var limited = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        limited.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        limited.Headers.RetryAfter.Should().NotBeNull();

        var problem = await limited.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(429);
        problem.GetProperty("code").GetString().Should().Be("RATE_LIMIT_EXCEEDED");
        problem.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("instance").GetString().Should().Be("/api/v1/test/scenarios/ok");
    }

    [Fact]
    public async Task ProblemDetails_PropagatesInboundCorrelationIdAsTraceId()
    {
        // Arrange
        await AuthorizeAsReaderAsync();
        _client.DefaultRequestHeaders.Remove("X-Correlation-Id");
        var correlationId = "trace-id-from-test-001";
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        // Act
        var response = await _client.GetAsync("/api/v1/test/scenarios/notfound");

        // Assert: the inbound X-Correlation-Id must surface as the traceId extension on the Problem
        // Details body (architecture baseline: trace id is propagated end to end). The header itself
        // is reset by ExceptionHandlerMiddleware.Clear() before the body is written, so it cannot be
        // re-checked here — the contract-level guarantee is the traceId field.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("traceId").GetString().Should().Be(correlationId);
    }

    private async Task AuthorizeAsReaderAsync()
    {
        var token = LocalizeStayWebApplicationFactory.CreateToken(
            "logto|staff-0192",
            PortfolioOnboardingPermissions.Read);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await Task.CompletedTask;
    }
}

internal static class JsonElementExtensions
{
    public static int GetobjectLength(this JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return element.EnumerateObject().Count();
    }
}
