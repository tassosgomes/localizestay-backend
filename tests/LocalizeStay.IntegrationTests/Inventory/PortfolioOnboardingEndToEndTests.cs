using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

public sealed class PortfolioOnboardingEndToEndTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public PortfolioOnboardingEndToEndTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PortfolioOnboarding_ReadyProperty_ShouldResolveCurationReturnResubmitCloseAndExposeHistoryAndMetrics()
    {
        await ClearInventoryAsync();
        using var client = CreateClient();

        await AssertInventoryMigrationAppliedAsync();

        var partner = await client.PostAsJsonAsync("/api/v1/partners", new { preselectionId = "pilot-preselection-001", legalName = "Release certification partner", legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "12.345.678/0001-90" }, primaryContact = new { name = "Release Operator", email = "release@example.test", phone = "+5585999999999" } });
        partner.StatusCode.Should().Be(HttpStatusCode.Created);
        var partnerBody = await ReadRequiredJsonAsync(partner, "id", "preselectionId", "legalName", "legalIdentifier", "primaryContact", "createdAt", "updatedAt");
        var partnerId = partnerBody.GetProperty("id").GetGuid();
        AssertLocation(partner, $"/api/v1/partners/{partnerId}");
        partnerBody.GetProperty("legalIdentifier").GetProperty("countryCode").GetString().Should().Be("BR");
        partnerBody.GetProperty("primaryContact").GetProperty("email").GetString().Should().Be("release@example.test");

        var onboarding = await client.PostAsJsonAsync("/api/v1/property-onboardings", new { partnerId, preselectionId = "pilot-preselection-001", property = new { name = "Release certification hotel", destinationId = "recife-pe", address = new { street = "Rua Release", number = "11", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" } } });
        onboarding.StatusCode.Should().Be(HttpStatusCode.Created);
        var onboardingBody = await ReadRequiredJsonAsync(onboarding, "id", "partnerId", "preselectionId", "property", "lifecycleStatus", "readinessStatus", "readinessGates", "pendingIssues", "duplicateReview", "blockingReasons", "openedAt", "targetSubmissionAt", "createdAt", "updatedAt");
        var onboardingId = onboardingBody.GetProperty("id").GetGuid();
        AssertLocation(onboarding, $"/api/v1/property-onboardings/{onboardingId}");
        onboardingBody.GetProperty("partnerId").GetGuid().Should().Be(partnerId);
        onboardingBody.GetProperty("property").GetProperty("address").GetProperty("countryCode").GetString().Should().Be("BR");
        onboardingBody.GetProperty("readinessGates").GetArrayLength().Should().Be(6);

        var issue = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/pending-issues", new { description = "Confirm legal evidence.", ownerType = "legal", relatedGateType = "legalIdentification" });
        issue.StatusCode.Should().Be(HttpStatusCode.Created);
        var issueBody = await ReadRequiredJsonAsync(issue, "id", "description", "ownerType", "status", "openedAt", "openedBy");
        var issueId = issueBody.GetProperty("id").GetGuid();
        AssertLocation(issue, $"/api/v1/property-onboardings/{onboardingId}/pending-issues/{issueId}");
        var resolved = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/pending-issues/{issueId}", new { status = "resolved", resolutionNote = "Legal evidence validated." });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);

        var communication = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/communication-records", new { channel = "email", receivedAt = DateTimeOffset.UtcNow.AddMinutes(-5), processedAt = DateTimeOffset.UtcNow, resultSummary = "Partner confirmed the operational contact." });
        communication.StatusCode.Should().Be(HttpStatusCode.Created);
        var communicationBody = await ReadRequiredJsonAsync(communication, "id", "channel", "receivedAt", "processedAt", "resultSummary", "processedWithinSla", "createdBy", "createdAt");
        AssertLocation(communication, $"/api/v1/property-onboardings/{onboardingId}/communication-records/{communicationBody.GetProperty("id").GetGuid()}");

        await ValidateGatesAsync(client, onboardingId);
        var submitted = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", new { idempotencyKey = Guid.NewGuid(), decisionNote = "All readiness gates are validated." });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertSubmittedStateAuditAndOutboxAsync(onboardingId);

        var returned = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/curation-returns", new { idempotencyKey = Guid.NewGuid(), curationReference = "curation-release-001", reasonCode = "missingData", reason = "A clearer legal reference is required.", issues = new[] { new { description = "Attach a clearer legal document reference.", ownerType = "legal", relatedGateType = "legalIdentification" } } });
        returned.StatusCode.Should().Be(HttpStatusCode.Created);
        var returnedBody = await returned.Content.ReadFromJsonAsync<JsonElement>();
        returnedBody.TryGetProperty("curationReturn", out var curationReturn).Should().BeTrue();
        returnedBody.TryGetProperty("onboarding", out var returnedOnboarding).Should().BeTrue();
        AssertRequiredProperties(curationReturn, "id", "reasonCode", "reason", "issues", "returnedAt", "returnedBy");
        AssertRequiredProperties(returnedOnboarding, "id", "lifecycleStatus", "pendingIssues", "readinessGates");
        AssertLocation(returned, $"/api/v1/property-onboardings/{onboardingId}/curation-returns/{curationReturn.GetProperty("id").GetGuid()}");
        returnedBody.GetProperty("onboarding").GetProperty("lifecycleStatus").GetString().Should().Be("returnedByCuration");
        var returnedIssueId = returnedBody.GetProperty("onboarding").GetProperty("pendingIssues").EnumerateArray()
            .Single(issue => issue.GetProperty("status").GetString() == "open")
            .GetProperty("id").GetGuid();

        var returnedIssueResolution = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/pending-issues/{returnedIssueId}", new { status = "resolved", resolutionNote = "The requested legal reference was attached and validated." });
        returnedIssueResolution.StatusCode.Should().Be(HttpStatusCode.OK);
        await ValidateGatesAsync(client, onboardingId);

        var resubmitted = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", new { idempotencyKey = Guid.NewGuid(), decisionNote = "Curation return was resolved and readiness was reconfirmed." });
        resubmitted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resubmitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("onboarding").GetProperty("lifecycleStatus").GetString().Should().Be("submittedToCuration");

        var closed = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/close", new { reasonCode = "partnerWithdrawal", reason = "Partner elected not to continue the pilot." });
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertFinalAuditTrailAsync(onboardingId);

        var history = await client.GetAsync($"/api/v1/property-onboardings/{onboardingId}/history?_page=1&_size=50");
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        var historyBody = await ReadRequiredJsonAsync(history, "data", "pagination");
        historyBody.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
        AssertRequiredProperties(historyBody.GetProperty("data")[0], "id", "type", "occurredAt", "actorId", "summary");
        var metrics = await client.GetAsync($"/api/v1/property-onboarding-metrics?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}");
        metrics.StatusCode.Should().Be(HttpStatusCode.OK);
        var metricsBody = await ReadRequiredJsonAsync(metrics, "from", "to", "totalOpened", "propertiesPreparedForCuration", "submittedWithinTenBusinessDays", "curationReturnRate", "completeGatesSubmissionRate", "communicationsWithinFourBusinessHours", "byLifecycleStatus");
        AssertRequiredProperties(metricsBody.GetProperty("curationReturnRate"), "numerator", "denominator", "percentage");
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|release-certification", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write, PortfolioOnboardingPermissions.Submit, PortfolioOnboardingPermissions.Close, PortfolioOnboardingPermissions.Metrics));
        return client;
    }

    private static async Task ValidateGatesAsync(HttpClient client, Guid onboardingId)
    {
        foreach (var gate in new[] { "legalIdentification", "commercialTerms", "propertyBasics" })
        {
            var response = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/{gate}", new { status = "validated", evidence = new[] { new { kind = "officialDocument", reference = $"release-{gate}", description = "Release certification evidence." } } });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/signedContract", new { status = "validated", contractReference = new { repositoryReference = "contracts/release-001", contractNumber = "RC-001", signedAt = DateTimeOffset.UtcNow, responsibleParties = new[] { "Release certification partner" } } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/authorizedContact", new { status = "validated", evidence = new[] { new { kind = "formalAuthorization", reference = "release-authorization", description = "Authorized representative." } }, authorizedContact = new { name = "Release Operator", email = "release@example.test", phone = "+5585999999999" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/operationalChannel", new { status = "validated", operationalChannelTest = new { channel = "email", contact = "operations@example.test", testedAt = DateTimeOffset.UtcNow, resultSummary = "Operational channel confirmed." } })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<JsonElement> ReadRequiredJsonAsync(HttpResponseMessage response, params string[] requiredProperties)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertRequiredProperties(body, requiredProperties);
        return body;
    }

    private static void AssertRequiredProperties(JsonElement body, params string[] requiredProperties)
    {
        body.ValueKind.Should().Be(JsonValueKind.Object);
        body.EnumerateObject().Select(property => property.Name).Should().Contain(requiredProperties);
    }

    private static void AssertLocation(HttpResponseMessage response, string expectedPath)
    {
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.IsAbsoluteUri.Should().BeFalse();
        response.Headers.Location.OriginalString.Should().Be(expectedPath);
    }

    private async Task ClearInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE inventory.property_onboardings, inventory.partners, inventory.audit_entries CASCADE;");
    }

    private async Task AssertInventoryMigrationAppliedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await dbContext.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty(
            "the E2E journey must run against the migrated Inventory schema");
    }

    private async Task AssertSubmittedStateAuditAndOutboxAsync(Guid onboardingId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var persistedOnboarding = await dbContext.PropertyOnboardings.AsNoTracking()
            .SingleAsync(item => item.Id == onboardingId);
        persistedOnboarding.LifecycleStatus.ToString().Should().Be("SubmittedToCuration");
        persistedOnboarding.SubmittedAt.Should().NotBeNull();

        var audit = await dbContext.BusinessAuditEntries.AsNoTracking().SingleAsync(entry =>
            entry.AggregateId == onboardingId.ToString() && entry.AuditType == "SubmittedToCuration");
        audit.Metadata.Should().ContainKey("idempotencyKey");

        var outbox = await dbContext.OutboxMessages.AsNoTracking().SingleAsync(message =>
            message.Type.Contains("InventoryPropertyOnboardedV1") && message.ProcessedOnUtc == null);
        outbox.Content.Should().Contain(onboardingId.ToString());
    }

    private async Task AssertFinalAuditTrailAsync(Guid onboardingId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var auditTypes = await dbContext.BusinessAuditEntries.AsNoTracking()
            .Where(entry => entry.AggregateId == onboardingId.ToString())
            .Select(entry => entry.AuditType)
            .ToListAsync();
        auditTypes.Should().Contain(["PropertyOnboardingCreated", "SubmittedToCuration", "ReturnedByCuration", "OnboardingClosed"]);
    }
}
