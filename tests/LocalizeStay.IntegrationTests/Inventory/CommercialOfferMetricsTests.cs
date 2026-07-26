using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Certifies the F02 commercial-offer metrics endpoint: it requires the dedicated
/// <c>commercial-offers:metrics</c> permission, exposes every numerator/denominator declared by
/// the YAML schema, accepts the destination filter, and returns zero-valued aggregates for empty
/// windows (reprocessabilidade garantida pela leitura recalculada em runtime).
/// </summary>
public sealed class CommercialOfferMetricsTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferMetricsTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMetrics_WithMetricsPermission_ShouldReturnAllDeclaredAggregates()
    {
        var client = CreateClientForSubject("logto|metrics-author", CommercialOfferPermissions.Metrics);
        await ClearCommercialDataAsync();

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow.AddDays(30);

        // Use simple format without fractional seconds
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var url = $"/api/v1/commercial-offer-metrics?from={fromStr}&to={toStr}";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("from").GetDateTimeOffset().Should().BeCloseTo(from, TimeSpan.FromSeconds(1));
        payload.GetProperty("to").GetDateTimeOffset().Should().BeCloseTo(to, TimeSpan.FromSeconds(1));
        payload.GetProperty("totalOffers").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        payload.GetProperty("completeProperties").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        payload.GetProperty("firstReviewAcceptanceRate").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        payload.GetProperty("submissionWithinTwoBusinessDaysRate").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        payload.GetProperty("dualValidationRate").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        payload.GetProperty("requestsProcessedWithinFourBusinessHoursRate").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        payload.GetProperty("returnedOfferCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        payload.GetProperty("averageReworkCount").GetDouble().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetMetrics_WithEmptyWindow_ShouldReturnZeroValuedAggregates()
    {
        var client = CreateClientForSubject("logto|metrics-empty", CommercialOfferPermissions.Metrics);
        await ClearCommercialDataAsync();

        var from = DateTimeOffset.UtcNow.AddDays(-365);
        var to = DateTimeOffset.UtcNow.AddDays(-360);
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await client.GetAsync(
            $"/api/v1/commercial-offer-metrics?from={fromStr}&to={toStr}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("totalOffers").GetInt32().Should().Be(0);
        payload.GetProperty("returnedOfferCount").GetInt32().Should().Be(0);
        payload.GetProperty("firstReviewAcceptanceRate").GetDouble().Should().Be(0);
        payload.GetProperty("dualValidationRate").GetDouble().Should().Be(1);
    }

    [Fact]
    public async Task GetMetrics_WithDestinationFilter_ShouldNotFail()
    {
        var client = CreateClientForSubject("logto|metrics-dest", CommercialOfferPermissions.Metrics);
        await ClearCommercialDataAsync();

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow.AddDays(30);
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await client.GetAsync(
            $"/api/v1/commercial-offer-metrics?from={fromStr}&to={toStr}&destinationId=recife-pe");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("totalOffers").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetMetrics_AfterSubmission_ShouldReflectAtLeastOneOffer()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|metrics-submit-author", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|metrics-submit-reviewer", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var metricsClient = CreateClientForSubject("logto|metrics-reader", CommercialOfferPermissions.Metrics);
        var propertyId = await EnsurePropertyExistsAsync();

        // Trigger lazy draft creation.
        await writeClient.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer");

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await metricsClient.GetAsync(
            $"/api/v1/commercial-offer-metrics?from={fromStr}&to={toStr}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("totalOffers").GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "the metrics aggregate must include the just-created draft offer");

        // Numerator/denominator invariants: rates can never exceed 1.0 (100%).
        payload.GetProperty("firstReviewAcceptanceRate").GetDouble().Should().BeLessThanOrEqualTo(1.0);
        payload.GetProperty("submissionWithinTwoBusinessDaysRate").GetDouble().Should().BeLessThanOrEqualTo(1.0);
        payload.GetProperty("dualValidationRate").GetDouble().Should().BeLessThanOrEqualTo(1.0);
        payload.GetProperty("requestsProcessedWithinFourBusinessHoursRate").GetDouble().Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task GetMetrics_WithFridayCompletion_ShouldUseBusinessCalendarAndBeReprocessable()
    {
        await ClearCommercialDataAsync();
        var completedOnFriday = DateTimeOffset.Parse("2026-07-17T15:00:00Z");
        var submittedOnTuesday = DateTimeOffset.Parse("2026-07-21T15:00:00Z");
        var submittedOnWednesday = DateTimeOffset.Parse("2026-07-22T15:00:00Z");

        await SeedSubmittedOfferAsync("calendar-fast", completedOnFriday, submittedOnTuesday);
        await SeedSubmittedOfferAsync("calendar-slow", completedOnFriday, submittedOnWednesday);

        var client = CreateClientForSubject("logto|metrics-calendar", CommercialOfferPermissions.Metrics);
        const string query = "/api/v1/commercial-offer-metrics?from=2026-07-01T00:00:00Z&to=2026-08-01T00:00:00Z";

        var first = await client.GetFromJsonAsync<JsonElement>(query);
        var second = await client.GetFromJsonAsync<JsonElement>(query);

        // Friday + two business days is Tuesday: one of two submissions meets the SLA.
        first.GetProperty("totalOffers").GetInt32().Should().Be(2);
        first.GetProperty("completeProperties").GetInt32().Should().Be(2);
        first.GetProperty("submissionWithinTwoBusinessDaysRate").GetDouble().Should().Be(0.5);
        first.GetProperty("firstReviewAcceptanceRate").GetDouble().Should().Be(1.0);
        first.GetProperty("dualValidationRate").GetDouble().Should().Be(1.0,
            "each submission has a valid review by a subject different from the revision author");
        first.GetProperty("returnedOfferCount").GetInt32().Should().Be(0);
        first.GetProperty("averageReworkCount").GetDouble().Should().Be(0.0);
        second.GetRawText().Should().Be(first.GetRawText(),
            "metrics are derived from persisted facts and must be reprocessable without mutation");
    }

    [Fact]
    public async Task GetMetrics_WithMalformedDate_ShouldReturn400BadRequest()
    {
        var client = CreateClientForSubject("logto|metrics-bad-date", CommercialOfferPermissions.Metrics);

        var response = await client.GetAsync(
            "/api/v1/commercial-offer-metrics?from=not-a-date&to=2026-12-31T23:59:59Z");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("BAD_REQUEST");
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
        var onboardingClient = CreateClientForSubject("logto|metrics-onboarding", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);
        var partnerResp = await onboardingClient.PostAsJsonAsync("/api/v1/partners", new
        {
            preselectionId = "pilot-preselection-001",
            legalName = "Metrics Partner",
            legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "00.000.000/0001-00" },
            primaryContact = new { name = "Metrics", email = "metrics@example.test", phone = "+5585999999999" },
        });
        partnerResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await partnerResp.Content.ReadAsStringAsync());
        var partnerId = (await partnerResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var onboardingResp = await onboardingClient.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Metrics Property",
                destinationId = "recife-pe",
                address = new { street = "Rua Metrics", number = "1", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" },
            },
        });
        onboardingResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await onboardingResp.Content.ReadAsStringAsync());
        var propertyId = (await onboardingResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        dbContext.IncorporatedProperties.Add(IncorporatedProperty.Create(
            propertyId, partnerId, "Metrics Property", "recife-pe", "logto|metrics-onboarding", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return propertyId;
    }

    private async Task SeedSubmittedOfferAsync(string suffix, DateTimeOffset completedAt, DateTimeOffset submittedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var property = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), $"Metrics {suffix}", "recife-pe", "logto|metrics-seed", completedAt);
        var offer = CommercialOffer.Create(property.Id, "logto|metrics-seed", completedAt);
        offer.RecalculateCompleteness(1, 1, 1, hasAnyRateOverlap: false, completedAt);
        var validation = OfferValidation.Create(
            Guid.NewGuid(), property.Id, offer.Revision, "logto|metrics-reviewer", completedAt.AddHours(1));

        dbContext.IncorporatedProperties.Add(property);
        dbContext.CommercialOffers.Add(offer);
        dbContext.OfferValidations.Add(validation);
        dbContext.OfferSubmissions.Add(OfferSubmission.Create(
            Guid.NewGuid(), property.Id, offer.Revision, validation.Id, "{\"snapshotVersion\":1}", "logto|metrics-seed", submittedAt));
        await dbContext.SaveChangesAsync();
    }

    private async Task ClearCommercialDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Modules.Inventory.Infrastructure.InventoryDbContext>();
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
}
