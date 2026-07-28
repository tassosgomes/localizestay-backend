using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LocalizeStay.UnitTests.ErrorHandling;

public sealed class BusinessRuleViolationMetadataTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task TryHandleAsync_WithMetadata_ShouldIncludeMetadataInProblemDetails()
    {
        // Arrange
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["conflictingDates"] = new[]
            {
                new { date = "2026-09-14", committedUnits = 3 },
            },
        };
        var exception = new BusinessRuleViolationException(
            "Redução abaixo do comprometido",
            "ALLOTMENT_BELOW_COMMITTED",
            metadata);

        var context = CreateHttpContext();
        var handler = CreateHandler(context);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.UnprocessableEntity);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body, _jsonOptions);
        problem.GetProperty("code").GetString().Should().Be("ALLOTMENT_BELOW_COMMITTED");
        problem.GetProperty("traceId").GetString().Should().Be("trace-test-001");
        var responseMetadata = problem.GetProperty("metadata");
        responseMetadata.GetProperty("conflictingDates")[0].GetProperty("date").GetString().Should().Be("2026-09-14");
        responseMetadata.GetProperty("conflictingDates")[0].GetProperty("committedUnits").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task TryHandleAsync_WithoutMetadata_ShouldOmitMetadataKey()
    {
        // Arrange
        var exception = new BusinessRuleViolationException("Regra violada", "ONBOARDING_NOT_READY");

        var context = CreateHttpContext();
        var handler = CreateHandler(context);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body, _jsonOptions);
        problem.TryGetProperty("metadata", out _).Should().BeFalse("metadata must be omitted when empty");
        problem.GetProperty("code").GetString().Should().Be("ONBOARDING_NOT_READY");
        problem.GetProperty("traceId").GetString().Should().Be("trace-test-001");
    }

    [Fact]
    public async Task TryHandleAsync_ConflictException_ShouldPreserveConflictingResourceId()
    {
        // Arrange
        var conflictingId = Guid.Parse("6b22179c-0143-4a70-97d3-c9648d77666a");
        var exception = new ConflictException("Duplicate", "DUPLICATE_LEGAL_IDENTIFIER", conflictingId);

        var context = CreateHttpContext();
        var handler = CreateHandler(context);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body, _jsonOptions);
        problem.GetProperty("metadata").GetProperty("conflictingResourceId").GetString().Should().Be("6b22179c-0143-4a70-97d3-c9648d77666a");
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-test-001";
        context.Request.Path = "/api/v1/test/scenarios/rule";
        return context;
    }

    private static GlobalExceptionHandler CreateHandler(HttpContext context)
    {
        var accessorMock = new Mock<ICorrelationIdAccessor>();
        accessorMock.Setup(a => a.CorrelationId).Returns(context.TraceIdentifier);
        return new GlobalExceptionHandler(accessorMock.Object, NullLogger<GlobalExceptionHandler>.Instance);
    }
}
