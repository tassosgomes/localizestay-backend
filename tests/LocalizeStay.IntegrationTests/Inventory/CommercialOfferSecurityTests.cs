using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Certifies the F02 security boundary: every commercial-offer endpoint requires the staff scope,
/// each of the four declared permissions (<c>read</c>/<c>write</c>/<c>review</c>/<c>metrics</c>) is
/// enforced on its operation set, the self-validation rule rejects the offer author as reviewer,
/// and the rate limiter emits RFC 9457 problem+json with <c>Retry-After</c> on overflow.
/// </summary>
public sealed class CommercialOfferSecurityTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferSecurityTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/v1/commercial-offers")]
    [InlineData("GET", "/api/v1/properties/00000000-0000-0000-0000-000000000001/commercial-offer")]
    [InlineData("GET", "/api/v1/properties/00000000-0000-0000-0000-000000000001/commercial-policies")]
    [InlineData("GET", "/api/v1/properties/00000000-0000-0000-0000-000000000001/accommodations")]
    [InlineData("GET", "/api/v1/properties/00000000-0000-0000-0000-000000000001/commercial-offer-history")]
    public async Task CommercialOfferEndpoints_AnonymousRequest_ShouldReturn401(string method, string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task CommercialOfferEndpoints_WithoutStaffPermissions_ShouldReturn403()
    {
        // Authenticated identity without any of the four commercial-offer permissions.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken($"logto|no-perms-{Guid.NewGuid()}"));

        var response = await client.GetAsync("/api/v1/commercial-offers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Theory]
    [InlineData(CommercialOfferPermissions.Write, "GET", "/api/v1/commercial-offer-metrics")]
    [InlineData(CommercialOfferPermissions.Read, "POST", "/api/v1/properties/00000000-0000-0000-0000-000000000001/commercial-policies")]
    [InlineData(CommercialOfferPermissions.Read, "POST", "/api/v1/properties/00000000-0000-0000-0000-000000000001/commercial-offer-validations")]
    public async Task CommercialOfferEndpoints_MissingOperationPermission_ShouldReturn403(
        string grantedPermission, string method, string path)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken($"logto|partial-{Guid.NewGuid()}", grantedPermission));
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" ? JsonContent.Create(new { type = "flexible", setAsDefault = false }) : null,
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CommercialOfferSecurity_AllFourPermissionsAreEnforcedOnDistinctOperations()
    {
        // The four F02 permissions map to distinct operation groups. This test certifies that each
        // permission unlocks its group and nothing else.
        await ClearCommercialDataAsync();
        var propertyId = await EnsurePropertyExistsAsync();

        // read: unlocks listCommercialOffers / getCommercialOffer / listCommercialPolicies / listAccommodations.
        var reader = CreateClientForSubject($"logto|sec-read-{Guid.NewGuid()}", CommercialOfferPermissions.Read);
        (await reader.GetAsync("/api/v1/commercial-offers?_page=1&_size=20")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await reader.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer")).StatusCode.Should().Be(HttpStatusCode.OK);

        // write: unlocks createCommercialPolicy (POST) — and the reader cannot do it.
        var writer = CreateClientForSubject($"logto|sec-write-{Guid.NewGuid()}", CommercialOfferPermissions.Write);
        var policyResponse = await writer.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });
        policyResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var readerPolicyResponse = await reader.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "nonRefundable", setAsDefault = false });
        readerPolicyResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // review: alone authorizes validation, while it cannot perform write operations.
        var accommodationResponse = await writer.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new
            {
                commercialName = "Permission Review Suite",
                maxAdults = 2,
                totalCapacity = 2,
                bedConfiguration = new[] { new { type = "queen", quantity = 2 } },
                mealPlan = "breakfast",
            });
        accommodationResponse.StatusCode.Should().Be(HttpStatusCode.Created, await accommodationResponse.Content.ReadAsStringAsync());
        var accommodationId = (await accommodationResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var rateResponse = await writer.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new
            {
                name = "Permission Review Rate",
                conditionCode = "standard",
                basePriceCents = 50_000L,
                validFrom = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                validTo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)).ToString("yyyy-MM-dd"),
                minimumNights = 1,
                mealPlan = "breakfast",
            });
        rateResponse.StatusCode.Should().Be(HttpStatusCode.Created, await rateResponse.Content.ReadAsStringAsync());

        var offer = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-offer");
        var reviewer = CreateClientForSubject($"logto|sec-review-{Guid.NewGuid()}", CommercialOfferPermissions.Review);
        var reviewResponse = await reviewer.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision = offer.GetProperty("revision").GetInt32(), comment = "Review permission alone." });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.Created, await reviewResponse.Content.ReadAsStringAsync());

        var reviewerWriteResponse = await reviewer.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "nonRefundable", setAsDefault = false });
        reviewerWriteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // metrics: unlocks getCommercialOfferMetrics — and the writer cannot do it.
        var metricsCaller = CreateClientForSubject($"logto|sec-metrics-{Guid.NewGuid()}", CommercialOfferPermissions.Metrics);
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow.AddDays(30);
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");
        (await metricsCaller.GetAsync($"/api/v1/commercial-offer-metrics?from={fromStr}&to={toStr}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await writer.GetAsync($"/api/v1/commercial-offer-metrics?from={fromStr}&to={toStr}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Validate_BySameAuthorAsRevision_ShouldBeRejectedAsSelfValidation()
    {
        await ClearCommercialDataAsync();
        var propertyId = await EnsurePropertyExistsAsync();
        var authorSubject = $"logto|self-val-{Guid.NewGuid()}";
        var authorClient = CreateClientForSubject(authorSubject,
            CommercialOfferPermissions.Read, CommercialOfferPermissions.Write, CommercialOfferPermissions.Review);

        // Trigger lazy draft creation + complete the offer as the same author.
        await authorClient.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer");
        await authorClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });

        var accResp = await authorClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Self-Val Suite", maxAdults = 2, totalCapacity = 2, bedConfiguration = new[] { new { type = "queen", quantity = 2 } }, mealPlan = "breakfast" });
        accResp.StatusCode.Should().Be(HttpStatusCode.Created, await accResp.Content.ReadAsStringAsync());
        var accommodationId = (await accResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var rateResp = await authorClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new { name = "Self-Val rate", conditionCode = "standard", basePriceCents = 50_000L, validFrom = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"), validTo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)).ToString("yyyy-MM-dd"), minimumNights = 1, mealPlan = "breakfast" });
        rateResp.StatusCode.Should().Be(HttpStatusCode.Created, await rateResp.Content.ReadAsStringAsync());

        var offer = await authorClient.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-offer");
        var expectedRevision = offer.GetProperty("revision").GetInt32();

        var validateResponse = await authorClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision, comment = "Self review must be rejected." });

        await AssertProblemAsync(validateResponse, HttpStatusCode.UnprocessableEntity, "SELF_VALIDATION_NOT_ALLOWED");
    }

    [Fact]
    public async Task RateLimit_WhenExceeded_ShouldReturn429WithRetryAfterHeader()
    {
        await ClearCommercialDataAsync();
        using var limitedFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = "1",
                ["RateLimit:TokensPerSecond"] = "1",
                ["RateLimit:QueueLimit"] = "0",
            })));
        using var client = limitedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken($"logto|sec-rate-{Guid.NewGuid()}", CommercialOfferPermissions.Read));

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.GetAsync("/api/v1/commercial-offers?_page=1&_size=1")));
        var rejected = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        rejected.Should().NotBeNull();
        rejected!.Headers.RetryAfter.Should().NotBeNull();
        await AssertProblemAsync(rejected, HttpStatusCode.TooManyRequests, "RATE_LIMIT_EXCEEDED");
        foreach (var response in responses) response.Dispose();
    }

    private HttpClient CreateClientForSubject(string subject, params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken(subject, permissions));
        return client;
    }

    private async Task<Guid> EnsurePropertyExistsAsync()
    {
        var onboardingClient = CreateClientForSubject("logto|sec-onboarding", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);
        var partnerResp = await onboardingClient.PostAsJsonAsync("/api/v1/partners", new
        {
            preselectionId = "pilot-preselection-001",
            legalName = "Security Partner",
            legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "00.000.000/0001-00" },
            primaryContact = new { name = "Security", email = "security@example.test", phone = "+5585999999999" },
        });
        partnerResp.StatusCode.Should().Be(HttpStatusCode.Created, await partnerResp.Content.ReadAsStringAsync());
        var partnerId = (await partnerResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var onboardingResp = await onboardingClient.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Security Property",
                destinationId = "recife-pe",
                address = new { street = "Rua Security", number = "1", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" },
            },
        });
        onboardingResp.StatusCode.Should().Be(HttpStatusCode.Created, await onboardingResp.Content.ReadAsStringAsync());
        var propertyId = (await onboardingResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        dbContext.IncorporatedProperties.Add(IncorporatedProperty.Create(
            propertyId, partnerId, "Security Property", "recife-pe", "logto|sec-onboarding", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return propertyId;
    }

    private async Task ClearCommercialDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        foreach (var table in new[]
        {
            "commercial_offer_idempotency_keys", "offer_returns", "offer_submissions", "offer_validations",
            "commercial_rates", "accommodations", "commercial_policies", "commercial_offers",
            "incorporated_properties", "readiness_gates", "communication_records", "duplicate_reviews",
            "pending_issues", "curation_returns", "idempotency_keys", "audit_entries", "outbox_messages",
            "property_onboardings", "partners",
        })
        {
            await dbContext.Database.ExecuteSqlRawAsync(string.Concat("DELETE FROM inventory.", table, ";"));
        }
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        using (response)
        {
            response.StatusCode.Should().Be(expectedStatus);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
            problem.GetProperty("code").GetString().Should().Be(expectedCode);
            problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }
}
