using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.Contracts.Curation;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Events;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// End-to-end certification of the full F01 → F02 pipeline against PostgreSQL Testcontainers:
/// onboarding materialises an IncorporatedProperty; the F02 flow opens the draft, builds policies,
/// accommodations and rates; a second operator validates; the offer is submitted idempotently
/// (single outbox); curation returns the offer via the in-process bus; the author corrects and
/// re-submits. Duplicate/out-of-order return events are idempotent.
/// </summary>
public sealed class CommercialOfferEndToEndTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferEndToEndTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task FullPipeline_OnboardSubmitReturnCorrectResubmit_ShouldPreserveHistoryAndSingleOutbox()
    {
        await ClearCommercialDataAsync();

        // 1. Create/submission F01 onboarding → materialise IncorporatedProperty via dbContext.
        var onboardingClient = CreateClientForSubject("logto|e2e-onboarding", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);
        var propertyId = await MaterialiseIncorporatedPropertyAsync(onboardingClient);

        // 2. Open F02 draft (lazy create on first read).
        var authorClient = CreateClientForSubject("logto|e2e-author", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewerClient = CreateClientForSubject("logto|e2e-reviewer", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);

        var draftResponse = await authorClient.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer");
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK, await draftResponse.Content.ReadAsStringAsync());
        var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
        draft.GetProperty("status").GetString().Should().Be("draft");

        // 3. Create policy + default + accommodation + rate.
        var policyResp = await authorClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });
        policyResp.StatusCode.Should().Be(HttpStatusCode.Created, await policyResp.Content.ReadAsStringAsync());
        var policyId = (await policyResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var accResp = await authorClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new
            {
                commercialName = "E2E Suite",
                maxAdults = 2,
                totalCapacity = 2,
                bedConfiguration = new[] { new { type = "queen", quantity = 2 } },
                mealPlan = "breakfast",
                policyId,
            });
        accResp.StatusCode.Should().Be(HttpStatusCode.Created, await accResp.Content.ReadAsStringAsync());
        var accommodationId = (await accResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var validFrom = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var validTo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)).ToString("yyyy-MM-dd");
        var rateResp = await authorClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new { name = "E2E rate", conditionCode = "standard", basePriceCents = 50_000L, includedGuests = 2, validFrom, validTo, minimumNights = 1, policyId, mealPlan = "breakfast" });
        rateResp.StatusCode.Should().Be(HttpStatusCode.Created, await rateResp.Content.ReadAsStringAsync());

        // 4. Validate with second operator.
        var expectedRevision = await GetRevisionAsync(authorClient, propertyId);
        var validateResp = await reviewerClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision, comment = "E2E validation." });
        validateResp.StatusCode.Should().Be(HttpStatusCode.Created, await validateResp.Content.ReadAsStringAsync());
        var validationId = (await validateResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // 5. Submit with idempotency key.
        expectedRevision = await GetRevisionAsync(authorClient, propertyId);
        var idempotencyKey = Guid.NewGuid();
        var submitResp = await SubmitAsync(authorClient, propertyId, expectedRevision, validationId, idempotencyKey);
        submitResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var submission = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var submissionId = submission.GetProperty("id").GetGuid();
        submission.GetProperty("eventName").GetString().Should().Be("oferta-inventario.oferta-estruturada");

        // 6. Confirm snapshot, audit and outbox.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var outboxCount = await dbContext.OutboxMessages.CountAsync(m => m.Type.Contains("InventoryCommercialOfferStructuredV1"));
            outboxCount.Should().Be(1, "a single submission must produce exactly one outbox message");
            var auditSubmittedCount = await dbContext.BusinessAuditEntries.CountAsync(e => e.AuditType == "OfferSubmitted");
            auditSubmittedCount.Should().Be(1);
        }

        // 7. Consume curation return event via in-process bus; verify it transitions to "returned".
        var eventId = Guid.NewGuid();
        await PublishReturnEventAsync(propertyId, submissionId, expectedRevision, eventId);

        var returnedOffer = await authorClient.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-offer");
        returnedOffer.GetProperty("status").GetString().Should().Be("returned");

        // 8. Publish the SAME return event again — idempotency must prevent duplicate returns.
        await PublishReturnEventAsync(propertyId, submissionId, expectedRevision, eventId);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var returnCount = await dbContext.OfferReturns.CountAsync(r => r.EventId == eventId);
            returnCount.Should().Be(1, "duplicate curation return events must be deduplicated by EventId");
        }

        // 9. Author corrects (mutation returns the offer to draft, bumps revision, invalidates prior validation).
        var patchResp = await authorClient.PatchAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}",
            new { commercialName = "E2E Suite Corrected", expectedRevision = await GetRevisionAsync(authorClient, propertyId) });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK, await patchResp.Content.ReadAsStringAsync());

        var correctedOffer = await authorClient.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-offer");
        correctedOffer.GetProperty("status").GetString().Should().Be("draft");
        correctedOffer.GetProperty("revision").GetInt32().Should().BeGreaterThan(expectedRevision);

        // 10. Re-validate (new validation) and re-submit.
        var newExpectedRevision = await GetRevisionAsync(authorClient, propertyId);
        var revalidateResp = await reviewerClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision = newExpectedRevision, comment = "E2E re-validation after correction." });
        revalidateResp.StatusCode.Should().Be(HttpStatusCode.Created, await revalidateResp.Content.ReadAsStringAsync());
        var newValidationId = (await revalidateResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var resubmitResp = await SubmitAsync(authorClient, propertyId, newExpectedRevision, newValidationId, Guid.NewGuid());
        resubmitResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var resubmission = await resubmitResp.Content.ReadFromJsonAsync<JsonElement>();
        resubmission.GetProperty("id").GetGuid().Should().NotBe(submissionId);

        // 11. A delayed return for the first submission must not undo the newer submission.
        // The handler ignores this out-of-order event without creating a return or audit entry.
        var staleEventId = Guid.NewGuid();
        await PublishReturnEventAsync(propertyId, submissionId, expectedRevision, staleEventId);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            (await dbContext.CommercialOffers.AsNoTracking().SingleAsync(o => o.PropertyId == propertyId)).State.Should().Be(OfferState.Submitted);
            (await dbContext.OfferReturns.CountAsync()).Should().Be(1);
            (await dbContext.BusinessAuditEntries.CountAsync(entry => entry.AuditType == "OfferReturned")).Should().Be(1);
        }

        // 12. History preserved both submissions and only the accepted return.
        var history = await authorClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/properties/{propertyId}/commercial-offer-history?_page=1&_size=50");
        var eventTypes = history.GetProperty("data").EnumerateArray()
            .Select(entry => entry.GetProperty("eventType").GetString())
            .ToList();
        eventTypes.Count(eventType => eventType == "submitted").Should().Be(2,
            "the history must preserve both the original and the re-submission events");
        eventTypes.Count(eventType => eventType == "returned").Should().Be(1);

        // 13. Metrics should reflect the completed flow.
        var metricsClient = CreateClientForSubject("logto|e2e-metrics", CommercialOfferPermissions.Metrics);
        var metricsResp = await metricsClient.GetAsync(
            $"/api/v1/commercial-offer-metrics?from={DateTimeOffset.UtcNow.AddDays(-7):O}&to={DateTimeOffset.UtcNow.AddDays(7):O}");
        metricsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var metrics = await metricsResp.Content.ReadFromJsonAsync<JsonElement>();
        metrics.GetProperty("totalOffers").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        metrics.GetProperty("returnedOfferCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    private HttpClient CreateClientForSubject(string subject, params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken(subject, permissions));
        return client;
    }

    private async Task<Guid> MaterialiseIncorporatedPropertyAsync(HttpClient onboardingClient)
    {
        var partnerResp = await onboardingClient.PostAsJsonAsync("/api/v1/partners", new
        {
            preselectionId = "pilot-preselection-001",
            legalName = "E2E Partner",
            legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "00.000.000/0001-00" },
            primaryContact = new { name = "E2E", email = "e2e@example.test", phone = "+5585999999999" },
        });
        partnerResp.StatusCode.Should().Be(HttpStatusCode.Created, await partnerResp.Content.ReadAsStringAsync());
        var partnerId = (await partnerResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var onboardingResp = await onboardingClient.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "E2E Property",
                destinationId = "recife-pe",
                address = new { street = "Rua E2E", number = "1", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" },
            },
        });
        onboardingResp.StatusCode.Should().Be(HttpStatusCode.Created, await onboardingResp.Content.ReadAsStringAsync());
        var propertyId = (await onboardingResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        dbContext.IncorporatedProperties.Add(LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties.IncorporatedProperty.Create(
            propertyId, partnerId, "E2E Property", "recife-pe", "logto|e2e-onboarding", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return propertyId;
    }

    private async Task PublishReturnEventAsync(Guid propertyId, Guid submissionId, int revision, Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        await eventBus.PublishAsync(new CurationOfferReturnedV1
        {
            EventId = eventId,
            PropertyId = propertyId,
            SubmissionId = submissionId,
            Revision = revision,
            ReasonCode = "incomplete_data",
            Reason = "Curation requires additional rate documentation.",
            ReturnedBy = "curator-e2e",
            ReturnedAt = DateTimeOffset.UtcNow,
            OccurredOnUtc = DateTimeOffset.UtcNow,
            CorrelationId = eventId.ToString(),
        }, CancellationToken.None);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid propertyId, int expectedRevision, Guid validationId, Guid idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/properties/{propertyId}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new { expectedRevision, validationId }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        return await client.SendAsync(request);
    }

    private static async Task<int> GetRevisionAsync(HttpClient client, Guid propertyId)
    {
        var offer = await client.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-offer");
        return offer.GetProperty("revision").GetInt32();
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
}
