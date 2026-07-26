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
/// Certifies the transactional outbox + business audit boundary for the F02 submission flow:
/// state, snapshot, audit entry and outbox message must commit atomically; replay must not
/// duplicate the outbox; a forced outbox failure must roll back every side-effect.
/// </summary>
public sealed class CommercialOfferOutboxAndAuditTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferOutboxAndAuditTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Submit_ShouldPersistStateSnapshotAuditAndOutboxInTheSameTransaction()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|outbox-author", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|outbox-reviewer", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var validationId = await EnsureOfferValidatedAsync(writeClient, reviewClient, propertyId);
        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/properties/{propertyId}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new { expectedRevision, validationId }),
        };
        submitRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var submitResponse = await writeClient.SendAsync(submitRequest);
        submitResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var offer = await dbContext.CommercialOffers.AsNoTracking().SingleAsync(o => o.PropertyId == propertyId);
        offer.State.Should().Be(OfferState.Submitted);

        var submission = await dbContext.OfferSubmissions.AsNoTracking().SingleAsync(s => s.PropertyId == propertyId);
        submission.ValidationId.Should().Be(validationId);
        submission.SnapshotJson.Should().NotBeNullOrWhiteSpace();
        var snapshot = JsonSerializer.Deserialize<JsonElement>(submission.SnapshotJson);
        snapshot.GetProperty("snapshotVersion").GetInt32().Should().Be(1);
        snapshot.GetProperty("state").GetString().Should().NotBeNullOrWhiteSpace();
        snapshot.GetProperty("validationId").GetGuid().Should().Be(validationId);

        var outbox = await dbContext.OutboxMessages.AsNoTracking()
            .Where(m => m.Type.Contains("InventoryCommercialOfferStructuredV1"))
            .ToListAsync();
        outbox.Should().ContainSingle();
        var outboxMessage = outbox[0];
        outboxMessage.CorrelationId.Should().NotBeNullOrWhiteSpace();
        var payload = JsonSerializer.Deserialize<JsonElement>(outboxMessage.Content);
        payload.GetProperty("propertyId").GetGuid().Should().Be(propertyId);
        payload.GetProperty("submissionId").GetGuid().Should().Be(submission.Id);
        payload.GetProperty("revisionAtSubmission").GetInt32().Should().Be(submission.Revision);

        var auditCount = await dbContext.BusinessAuditEntries.AsNoTracking()
            .CountAsync(entry => entry.AggregateId == offer.Id.ToString() && entry.AuditType == "OfferSubmitted");
        auditCount.Should().Be(1, "the submission handler must record exactly one OfferSubmitted audit entry in the same transaction");
    }

    [Fact]
    public async Task Submit_WithForcedOutboxFailure_ShouldRollbackStateSnapshotAndAudit()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|outbox-fail-author", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|outbox-fail-reviewer", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var validationId = await EnsureOfferValidatedAsync(writeClient, reviewClient, propertyId);
        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("CREATE OR REPLACE FUNCTION inventory.reject_f02_outbox() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced F02 outbox failure'; END; $$;");
        await dbContext.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_f02_outbox BEFORE INSERT ON inventory.outbox_messages FOR EACH ROW EXECUTE FUNCTION inventory.reject_f02_outbox();");

        System.Net.HttpStatusCode responseStatus;
        try
        {
            using var submitRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/properties/{propertyId}/commercial-offer-submissions")
            {
                Content = JsonContent.Create(new { expectedRevision, validationId }),
            };
            submitRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var response = await writeClient.SendAsync(submitRequest);
            responseStatus = response.StatusCode;
            response.Dispose();
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS reject_f02_outbox ON inventory.outbox_messages;");
            await dbContext.Database.ExecuteSqlRawAsync("DROP FUNCTION IF EXISTS inventory.reject_f02_outbox();");
        }

        responseStatus.Should().Be(System.Net.HttpStatusCode.InternalServerError,
            "an outbox insert failure must surface as 500 INTERNAL_ERROR after rollback");

        dbContext.ChangeTracker.Clear();
        var offer = await dbContext.CommercialOffers.AsNoTracking().SingleAsync(o => o.PropertyId == propertyId);
        offer.State.Should().Be(OfferState.Validated,
            "the state transition to Submitted must roll back when the outbox insert fails");

        (await dbContext.OfferSubmissions.CountAsync(s => s.PropertyId == propertyId)).Should().Be(0,
            "the submission row must not persist when the outbox insert fails");
        (await dbContext.OutboxMessages.CountAsync()).Should().Be(0,
            "no outbox message may remain when the insert fails");
        (await dbContext.BusinessAuditEntries.CountAsync(e => e.AuditType == "OfferSubmitted")).Should().Be(0,
            "the audit entry must roll back together with the outbox message");
    }

    [Fact]
    public async Task Submit_IdempotentReplay_ShouldNotDuplicateOutboxOrAudit()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateClientForSubject("logto|outbox-replay-author", CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateClientForSubject("logto|outbox-replay-reviewer", CommercialOfferPermissions.Read, CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        var validationId = await EnsureOfferValidatedAsync(writeClient, reviewClient, propertyId);
        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var idempotencyKey = Guid.NewGuid();

        await SendSubmissionAsync(writeClient, propertyId, expectedRevision, validationId, idempotencyKey);
        await SendSubmissionAsync(writeClient, propertyId, expectedRevision, validationId, idempotencyKey);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await dbContext.OfferSubmissions.CountAsync(s => s.PropertyId == propertyId)).Should().Be(1);
        (await dbContext.OutboxMessages.CountAsync(m => m.Type.Contains("InventoryCommercialOfferStructuredV1"))).Should().Be(1);
        (await dbContext.BusinessAuditEntries.CountAsync(e => e.AuditType == "OfferSubmitted")).Should().Be(1);
    }

    private static async Task SendSubmissionAsync(HttpClient client, Guid propertyId, int expectedRevision, Guid validationId, Guid idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/properties/{propertyId}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new { expectedRevision, validationId }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        response.Dispose();
    }

    private HttpClient CreateClientForSubject(string subject, params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken(subject, permissions));
        return client;
    }

    private static async Task<int> GetRevisionAsync(HttpClient client, Guid propertyId)
    {
        var offer = await client.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-offer");
        return offer.GetProperty("revision").GetInt32();
    }

    private async Task<Guid> EnsureOfferValidatedAsync(HttpClient writeClient, HttpClient reviewClient, Guid propertyId)
    {
        // Trigger lazy draft creation.
        await writeClient.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer");

        await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });
        var policy = await writeClient.GetFromJsonAsync<JsonElement>($"/api/v1/properties/{propertyId}/commercial-policies");
        var policyId = policy.GetProperty("data").EnumerateArray().First().GetProperty("id").GetGuid();

        var accResp = await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new
            {
                commercialName = "Outbox Suite",
                maxAdults = 2,
                totalCapacity = 2,
                bedConfiguration = new[] { new { type = "queen", quantity = 2 } },
                mealPlan = "breakfast",
                policyId,
            });
        accResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await accResp.Content.ReadAsStringAsync());
        var accommodationId = (await accResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var validFrom = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var validTo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)).ToString("yyyy-MM-dd");
        var rateResp = await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new { name = "Outbox rate", conditionCode = "standard", basePriceCents = 50_000L, includedGuests = 2, validFrom, validTo, minimumNights = 1, policyId, mealPlan = "breakfast" });
        rateResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await rateResp.Content.ReadAsStringAsync());

        var expectedRevision = await GetRevisionAsync(writeClient, propertyId);
        var validateResp = await reviewClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision, comment = "Validated for outbox certification." });
        validateResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await validateResp.Content.ReadAsStringAsync());
        return (await validateResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> EnsurePropertyExistsAsync()
    {
        var onboardingClient = CreateClientForSubject("logto|outbox-onboarding", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);
        var partnerResp = await onboardingClient.PostAsJsonAsync("/api/v1/partners", new
        {
            preselectionId = "pilot-preselection-001",
            legalName = "Outbox Partner",
            legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "00.000.000/0001-00" },
            primaryContact = new { name = "Outbox", email = "outbox@example.test", phone = "+5585999999999" },
        });
        partnerResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await partnerResp.Content.ReadAsStringAsync());
        var partnerId = (await partnerResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var onboardingResp = await onboardingClient.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Outbox Property",
                destinationId = "recife-pe",
                address = new { street = "Rua Outbox", number = "1", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" },
            },
        });
        onboardingResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await onboardingResp.Content.ReadAsStringAsync());
        var propertyId = (await onboardingResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        dbContext.IncorporatedProperties.Add(IncorporatedProperty.Create(propertyId, partnerId, "Outbox Property", "recife-pe", "logto|outbox-onboarding", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return propertyId;
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
