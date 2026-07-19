using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

public sealed class PropertyOnboardingWorkflowTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public PropertyOnboardingWorkflowTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task LifecycleEndpoints_WithPoliciesRetryReturnCloseAndNewCycle_ShouldPreserveWorkflowHistory()
    {
        await ClearInventoryAsync();
        var unauthorizedClient = _factory.CreateClient();
        var unauthorized = await unauthorizedClient.PostAsJsonAsync($"/api/v1/property-onboardings/{Guid.NewGuid()}/submit-to-curation", new { idempotencyKey = Guid.NewGuid(), decisionNote = "Submit ready property." });
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var writeOnlyClient = CreateClient(PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);
        var (partnerId, onboardingId) = await CreateOnboardingAsync(writeOnlyClient);
        var forbiddenSubmit = await writeOnlyClient.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", new { idempotencyKey = Guid.NewGuid(), decisionNote = "Submit ready property." });
        forbiddenSubmit.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var client = CreateClient(PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write, PortfolioOnboardingPermissions.Submit, PortfolioOnboardingPermissions.Close);
        var blocked = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", new { idempotencyKey = Guid.NewGuid(), decisionNote = "Submit ready property." });
        blocked.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("ONBOARDING_NOT_READY");

        await ValidateAllGatesAsync(client, onboardingId);
        var submitKey = Guid.NewGuid();
        var request = new { idempotencyKey = submitKey, decisionNote = "All contractual gates are complete." };
        var submissions = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", request),
            client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", request));
        submissions.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        var conflict = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", new { idempotencyKey = submitKey, decisionNote = "Incompatible decision note." });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("STATE_CONFLICT");

        var returnKey = Guid.NewGuid();
        var returnRequest = new { idempotencyKey = returnKey, curationReference = "curation-123", reasonCode = "missingData", reason = "The legal document needs a clearer reference.", issues = new[] { new { description = "Upload a clearer legal document reference.", ownerType = "legal", relatedGateType = "legalIdentification" } } };
        var returns = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/curation-returns", returnRequest),
            client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/curation-returns", returnRequest));
        returns.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Created);
        var returnConflict = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/curation-returns", new { idempotencyKey = returnKey, curationReference = "curation-123", reasonCode = "inconsistentData", reason = "The legal document needs a clearer reference.", issues = returnRequest.issues });
        returnConflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var closed = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/close", new { reasonCode = "partnerWithdrawal", reason = "Partner withdrew after curation requested more documentation." });
        closed.StatusCode.Should().Be(HttpStatusCode.OK);

        var newCycle = await client.PostAsJsonAsync("/api/v1/property-onboardings", OnboardingRequest(partnerId, "Workflow Hotel", "10"));
        newCycle.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var original = await dbContext.PropertyOnboardings.Include(item => item.CurationReturns).Include(item => item.PendingIssues).SingleAsync(item => item.Id == onboardingId);
        original.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.Closed);
        original.CurationReturns.Should().ContainSingle();
        original.PendingIssues.Should().Contain(issue => issue.Status == PendingIssueStatus.Open);
    }

    private HttpClient CreateClient(params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|workflow-staff", permissions));
        return client;
    }

    private async Task<(Guid PartnerId, Guid OnboardingId)> CreateOnboardingAsync(HttpClient client)
    {
        var partner = await client.PostAsJsonAsync("/api/v1/partners", new { preselectionId = "pilot-preselection-001", legalName = "Workflow Partner", legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "12.345.678/0001-90" }, primaryContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+5511999999999" } });
        partner.StatusCode.Should().Be(HttpStatusCode.Created);
        var partnerId = (await partner.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var onboarding = await client.PostAsJsonAsync("/api/v1/property-onboardings", OnboardingRequest(partnerId, "Workflow Hotel", "10"));
        onboarding.StatusCode.Should().Be(HttpStatusCode.Created);
        return (partnerId, (await onboarding.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    private static object OnboardingRequest(Guid partnerId, string name, string number) => new { partnerId, preselectionId = "pilot-preselection-001", property = new { name, destinationId = "recife-pe", address = new { street = "Rua Workflow", number, district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" } } };

    private static async Task ValidateAllGatesAsync(HttpClient client, Guid onboardingId)
    {
        foreach (var gate in new[] { "legalIdentification", "commercialTerms", "propertyBasics" })
        {
            var response = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/{gate}", new { status = "validated", evidence = new[] { new { kind = "officialDocument", reference = $"evidence-{gate}", description = "Validated operational evidence." } } });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var contract = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/signedContract", new { status = "validated", contractReference = new { repositoryReference = "contracts/workflow-001", contractNumber = "WF-001", signedAt = DateTimeOffset.UtcNow, responsibleParties = new[] { "Workflow Partner" } } });
        contract.StatusCode.Should().Be(HttpStatusCode.OK);
        var contact = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/authorizedContact", new { status = "validated", evidence = new[] { new { kind = "formalAuthorization", reference = "authorization-workflow", description = "Formal authorization validated." } }, authorizedContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+5511999999999" } });
        contact.StatusCode.Should().Be(HttpStatusCode.OK);
        var channel = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/operationalChannel", new { status = "validated", operationalChannelTest = new { channel = "email", contact = "operations@example.com", testedAt = DateTimeOffset.UtcNow, resultSummary = "Operational channel tested." } });
        channel.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task ClearInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE inventory.property_onboardings, inventory.partners, inventory.audit_entries CASCADE;");
    }
}
