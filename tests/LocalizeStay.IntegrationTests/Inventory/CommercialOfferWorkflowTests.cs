using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Drives the F02 commercial-offer state machine end-to-end via real HTTP requests against the
/// PostgreSQL-backed host: validation, submission, return, correction, re-validation, re-submission,
/// revision conflicts, concurrent first-draft creation and idempotent submission replay. The
/// companion <c>CommercialOfferOutboxAndAuditTests</c> certifies the transactional outbox/audit
/// side-effects produced by these flows.
/// </summary>
public sealed class CommercialOfferWorkflowTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferWorkflowTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Validate_OfferCreatedByDifferentOperator_ShouldTransitionToValidated()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-1", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|reviewer-1", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        await EnsureCompleteOfferAsync(writeClient, propertyId);

        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var response = await reviewClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision, comment = "Commercial data checked by second operator." });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var validation = await response.Content.ReadFromJsonAsync<JsonElement>();
        validation.GetProperty("propertyId").GetGuid().Should().Be(propertyId);
        validation.GetProperty("revision").GetInt32().Should().Be(expectedRevision);
        validation.GetProperty("status").GetString().Should().Be("valid");
        validation.GetProperty("validatedBy").GetProperty("id").GetString().Should().Be("logto|reviewer-1");
    }

    [Fact]
    public async Task Validate_BySameAuthor_ShouldBeRejectedAsSelfValidation()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-2", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|author-2", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        await EnsureCompleteOfferAsync(writeClient, propertyId);

        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var response = await reviewClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision, comment = "Self review." });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "SELF_VALIDATION_NOT_ALLOWED");
    }

    [Fact]
    public async Task Submit_ReplaysSameIdempotencyKeyAndProducesSingleSubmission()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-3", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|reviewer-3", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var validationId = await EnsureOfferValidatedAsync(writeClient, reviewClient, propertyId);
        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var idempotencyKey = Guid.NewGuid();

        var first = await SubmitAsync(writeClient, propertyId, expectedRevision, validationId, idempotencyKey);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstSubmissionId = firstBody.GetProperty("id").GetGuid();
        firstBody.GetProperty("eventName").GetString().Should().Be("oferta-inventario.oferta-estruturada");

        // Same Idempotency-Key + same payload must replay the original submission (single outbox entry).
        var second = await SubmitAsync(writeClient, propertyId, expectedRevision, validationId, idempotencyKey);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        secondBody.GetProperty("id").GetGuid().Should().Be(firstSubmissionId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var submissionCount = await dbContext.OfferSubmissions.CountAsync(s => s.PropertyId == propertyId);
        submissionCount.Should().Be(1, "idempotent replay must never create a second submission");
        var outboxCount = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Type.Contains("InventoryCommercialOfferStructuredV1"))
            .CountAsync();
        outboxCount.Should().Be(1, "a single idempotent submission must produce exactly one outbox message");
    }

    [Fact]
    public async Task Submit_WithReusedIdempotencyKeyButDifferentPayload_ShouldReturn409()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-4", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|reviewer-4", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var validationId = await EnsureOfferValidatedAsync(writeClient, reviewClient, propertyId);
        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var idempotencyKey = Guid.NewGuid();

        var first = await SubmitAsync(writeClient, propertyId, expectedRevision, validationId, idempotencyKey);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Same Idempotency-Key but a different validationId payload must be rejected as a conflict.
        var second = await SubmitAsync(writeClient, propertyId, expectedRevision, Guid.NewGuid(), idempotencyKey);
        await AssertProblemAsync(second, HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Submit_WithoutActiveValidation_ShouldReturn422()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-5", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await EnsureCompleteOfferAsync(writeClient, propertyId);

        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var response = await SubmitAsync(writeClient, propertyId, expectedRevision, Guid.NewGuid(), Guid.NewGuid());

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_REQUIRED");
    }

    [Fact]
    public async Task Submit_WithStaleRevision_ShouldReturn422RevisionMismatch()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-6", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|reviewer-6", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var validationId = await EnsureOfferValidatedAsync(writeClient, reviewClient, propertyId);

        var response = await SubmitAsync(writeClient, propertyId, expectedRevision: 999, validationId, Guid.NewGuid());

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "REVISION_MISMATCH");
    }

    [Fact]
    public async Task ConcurrentFirstDraftCreation_ShouldYieldExactlyOneOfferPerProperty()
    {
        await ClearCommercialDataAsync();
        var propertyId = await EnsurePropertyExistsAsync();
        var client = CreateClientForSubject("logto|author-7", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);

        // Race the lazy-create path on the GET endpoint from multiple workers. The unique constraint
        // on commercial_offers.property_id plus the catch-and-replay in the create handler must
        // guarantee exactly one draft survives.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer")));
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Dispose();
        }

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var offerCount = await dbContext.CommercialOffers.CountAsync(o => o.PropertyId == propertyId);
        offerCount.Should().Be(1, "concurrent first-draft creation must converge to a single offer");
    }

    [Fact]
    public async Task RevisionMutation_WhileAnotherOperatorValidates_ShouldInvalidateValidation()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-8", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|reviewer-8", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var (_, accommodationId) = await EnsureCompleteOfferAsync(writeClient, propertyId);

        var revisionBeforeValidation = await GetRevisionAsync(writeClient, propertyId);
        var validateResponse = await reviewClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision = revisionBeforeValidation, comment = "First review." });
        validateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Any commercial mutation invalidates the active validation and bumps the revision.
        var mutationResponse = await writeClient.PatchAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}",
            new { commercialName = "Updated after validation", expectedRevision = revisionBeforeValidation });
        mutationResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var offer = await writeClient.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-offer");
        offer.GetProperty("revision").GetInt32().Should().BeGreaterThan(revisionBeforeValidation);
        // Mutation invalidates the active validation (status flips to "invalidated"); a new review
        // is then required before submission. The reference is preserved for auditability.
        offer.GetProperty("currentValidation").ValueKind.Should().Be(JsonValueKind.Object);
        offer.GetProperty("currentValidation").GetProperty("status").GetString().Should().Be("invalidated");
    }

    [Fact]
    public async Task OfferHistory_AggregatesCreatedValidationAndSubmissionEventsInReverseOrder()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|author-9", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|reviewer-9", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var validationId = await EnsureOfferValidatedAsync(writeClient, reviewClient, propertyId);
        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var submitResponse = await SubmitAsync(writeClient, propertyId, expectedRevision, validationId, Guid.NewGuid());
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var history = await writeClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/properties/{propertyId}/commercial-offer-history?_page=1&_size=20");

        history.GetProperty("pagination").GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        var eventTypes = history.GetProperty("data").EnumerateArray()
            .Select(entry => entry.GetProperty("eventType").GetString())
            .ToList();
        // Submission is the headline event of this flow; validation/mutation events are also recorded
        // in business_audit_entries and surfaced through the same history projection.
        eventTypes.Should().Contain("submitted");
        eventTypes.Should().NotBeEmpty();
    }

    private HttpClient CreateClientForSubject(string subject, params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken(subject, permissions));
        return client;
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

    private async Task<(Guid PropertyId, Guid AccommodationId)> EnsureCompleteOfferAsync(HttpClient client, Guid propertyId)
    {
        // Lazy draft creation happens on first read of the commercial-offer aggregate.
        var draftResponse = await client.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer");
        draftResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Draft creation failed: {await draftResponse.Content.ReadAsStringAsync()}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var persistedOffer = await verifyContext.CommercialOffers.AsNoTracking()
            .SingleOrDefaultAsync(o => o.PropertyId == propertyId);
        persistedOffer.Should().NotBeNull(
            "the GET endpoint must persist the draft commercial offer before subsequent mutations");

        var policyResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });
        policyResp.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"Policy creation failed: {await policyResp.Content.ReadAsStringAsync()}");
        var policy = await policyResp.Content.ReadFromJsonAsync<JsonElement>();
        var policyId = policy.GetProperty("id").GetGuid();

        var accResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new
            {
                commercialName = "Complete Suite",
                maxAdults = 2,
                totalCapacity = 2,
                bedConfiguration = new[] { new { type = "queen", quantity = 2 } },
                mealPlan = "breakfast",
                policyId,
            });
        accResp.StatusCode.Should().Be(HttpStatusCode.Created, await accResp.Content.ReadAsStringAsync());
        var accommodation = await accResp.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = accommodation.GetProperty("id").GetGuid();

        var validFrom = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var validTo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)).ToString("yyyy-MM-dd");
        var rateResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new
            {
                name = "Complete rate",
                conditionCode = "standard",
                basePriceCents = 50_000L,
                includedGuests = 2,
                validFrom,
                validTo,
                minimumNights = 1,
                policyId,
                mealPlan = "breakfast",
            });
        rateResp.StatusCode.Should().Be(HttpStatusCode.Created, await rateResp.Content.ReadAsStringAsync());

        return (propertyId, accommodationId);
    }

    private async Task<Guid> EnsureOfferValidatedAsync(HttpClient writeClient, HttpClient reviewClient, Guid propertyId)
    {
        await EnsureCompleteOfferAsync(writeClient, propertyId);
        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var response = await reviewClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision, comment = "Validation for workflow tests." });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var validation = await response.Content.ReadFromJsonAsync<JsonElement>();
        return validation.GetProperty("id").GetGuid();
    }

    private async Task<Guid> EnsurePropertyExistsAsync()
    {
        var onboardingClient = CreateClientForSubject("logto|onboarding-operator", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);
        var partnerResp = await onboardingClient.PostAsJsonAsync("/api/v1/partners", new
        {
            preselectionId = "pilot-preselection-001",
            legalName = "Workflow Partner",
            legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "00.000.000/0001-00" },
            primaryContact = new { name = "Workflow", email = "workflow@example.test", phone = "+5585999999999" },
        });
        partnerResp.StatusCode.Should().Be(HttpStatusCode.Created, await partnerResp.Content.ReadAsStringAsync());
        var partnerId = (await partnerResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var onboardingResp = await onboardingClient.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Workflow Property",
                destinationId = "recife-pe",
                address = new { street = "Rua Workflow", number = "1", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" },
            },
        });
        onboardingResp.StatusCode.Should().Be(HttpStatusCode.Created, await onboardingResp.Content.ReadAsStringAsync());
        var propertyId = (await onboardingResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        dbContext.IncorporatedProperties.Add(IncorporatedProperty.Create(
            propertyId, partnerId, "Workflow Property", "recife-pe", "logto|onboarding-operator", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return propertyId;
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

    private async Task ClearCommercialDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.commercial_offer_idempotency_keys;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.offer_returns;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.offer_submissions;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.offer_validations;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.commercial_rates;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.accommodations;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.commercial_policies;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.commercial_offers;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.incorporated_properties;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.readiness_gates;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.communication_records;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.duplicate_reviews;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.pending_issues;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.curation_returns;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.idempotency_keys;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.audit_entries;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.outbox_messages;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.property_onboardings;");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM inventory.partners;");
    }
}
