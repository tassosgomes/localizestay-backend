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

public sealed class OutboxAndAuditTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public OutboxAndAuditTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Submit_WithPostgreSqlOutboxFailure_ShouldRollbackStateAuditAndOutbox()
    {
        await ClearInventoryAsync();
        var client = CreateClient();
        var onboardingId = await CreateReadyOnboardingAsync(client);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("CREATE OR REPLACE FUNCTION inventory.reject_test_outbox() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced outbox failure'; END; $$;");
        await dbContext.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_test_outbox BEFORE INSERT ON inventory.outbox_messages FOR EACH ROW EXECUTE FUNCTION inventory.reject_test_outbox();");
        try
        {
            var response = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", new { idempotencyKey = Guid.NewGuid(), decisionNote = "Atomic transaction must fail." });
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS reject_test_outbox ON inventory.outbox_messages; DROP FUNCTION IF EXISTS inventory.reject_test_outbox();");
        }
        dbContext.ChangeTracker.Clear();
        var onboarding = await dbContext.PropertyOnboardings.SingleAsync(item => item.Id == onboardingId);
        onboarding.LifecycleStatus.ToString().Should().Be("InProgress");
        (await dbContext.OutboxMessages.CountAsync()).Should().Be(0);
        (await dbContext.BusinessAuditEntries.CountAsync(entry => entry.AggregateId == onboardingId.ToString() && entry.AuditType == "SubmittedToCuration")).Should().Be(0);
    }

    [Fact]
    public async Task Submit_WithReadyOnboarding_ShouldPersistStateAuditAndVersionedOutboxTogether()
    {
        await ClearInventoryAsync();
        var client = CreateClient();
        var onboardingId = await CreateReadyOnboardingAsync(client);
        var response = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/submit-to-curation", new { idempotencyKey = Guid.NewGuid(), decisionNote = "All gates were checked for curation." });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var onboarding = await dbContext.PropertyOnboardings.SingleAsync(item => item.Id == onboardingId);
        onboarding.LifecycleStatus.ToString().Should().Be("SubmittedToCuration");
        var outbox = await dbContext.OutboxMessages.SingleAsync();
        outbox.Type.Should().Contain("InventoryPropertyOnboardedV1");
        JsonDocument.Parse(outbox.Content).RootElement.GetProperty("version").GetInt32().Should().Be(1);
        (await dbContext.BusinessAuditEntries.CountAsync(entry => entry.AggregateId == onboardingId.ToString() && entry.AuditType == "SubmittedToCuration")).Should().Be(1);
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|outbox-staff", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write, PortfolioOnboardingPermissions.Submit));
        return client;
    }

    private async Task<Guid> CreateReadyOnboardingAsync(HttpClient client)
    {
        var partner = await client.PostAsJsonAsync("/api/v1/partners", new { preselectionId = "pilot-preselection-001", legalName = "Outbox Partner", legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "12.345.678/0001-90" }, primaryContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+5511999999999" } });
        var partnerId = (await partner.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var onboarding = await client.PostAsJsonAsync("/api/v1/property-onboardings", new { partnerId, preselectionId = "pilot-preselection-001", property = new { name = "Outbox Hotel", destinationId = "recife-pe", address = new { street = "Rua Outbox", number = "20", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" } } });
        var onboardingId = (await onboarding.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var gate in new[] { "legalIdentification", "commercialTerms", "propertyBasics" }) await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/{gate}", new { status = "validated", evidence = new[] { new { kind = "officialDocument", reference = $"evidence-{gate}", description = "Validated operational evidence." } } });
        await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/signedContract", new { status = "validated", contractReference = new { repositoryReference = "contracts/outbox-001", contractNumber = "OB-001", signedAt = DateTimeOffset.UtcNow, responsibleParties = new[] { "Outbox Partner" } } });
        await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/authorizedContact", new { status = "validated", evidence = new[] { new { kind = "formalAuthorization", reference = "authorization-outbox", description = "Formal authorization validated." } }, authorizedContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+5511999999999" } });
        await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/operationalChannel", new { status = "validated", operationalChannelTest = new { channel = "email", contact = "operations@example.com", testedAt = DateTimeOffset.UtcNow, resultSummary = "Operational channel tested." } });
        return onboardingId;
    }

    private async Task ClearInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE inventory.property_onboardings, inventory.partners, inventory.audit_entries CASCADE;");
    }
}
