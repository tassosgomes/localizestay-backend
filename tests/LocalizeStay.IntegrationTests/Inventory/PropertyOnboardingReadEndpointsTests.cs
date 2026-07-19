using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

public sealed class PropertyOnboardingReadEndpointsTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public PropertyOnboardingReadEndpointsTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task History_ReturnsReversePaginationAndOnlySafeMetadata()
    {
        await ClearInventoryAsync();
        var onboarding = await SeedOnboardingAsync(DateTimeOffset.Parse("2026-07-01T10:00:00Z"));
        await SeedAuditAsync(onboarding.Id, "PropertyOnboardingCreated", "2026-07-01T10:00:00Z", new Dictionary<string, string> { ["destinationId"] = "recife-pe" });
        await SeedAuditAsync(onboarding.Id, "SubmittedToCuration", "2026-07-01T11:00:00Z", new Dictionary<string, string> { ["idempotencyKey"] = Guid.NewGuid().ToString() });
        using var client = CreateAuthorizedClient(PortfolioOnboardingPermissions.Read);

        var response = await client.GetAsync($"/api/v1/property-onboardings/{onboarding.Id}/history?_page=1&_size=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("pagination").GetProperty("total").GetInt32().Should().Be(2);
        payload.GetProperty("data").GetArrayLength().Should().Be(1);
        payload.GetProperty("data")[0].GetProperty("type").GetString().Should().Be("submittedToCuration");
        payload.GetProperty("data")[0].GetProperty("metadata").EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public async Task Metrics_UsesExclusiveToBoundaryAndMetricsPolicy()
    {
        await ClearInventoryAsync();
        await SeedOnboardingAsync(DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        await SeedOnboardingAsync(DateTimeOffset.Parse("2026-07-02T00:00:00Z"));
        using var client = CreateAuthorizedClient(PortfolioOnboardingPermissions.Metrics);

        var response = await client.GetAsync("/api/v1/property-onboarding-metrics?from=2026-07-01T00:00:00Z&to=2026-07-02T00:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("totalOpened").GetInt32().Should().Be(1);
        payload.GetProperty("submittedWithinTenBusinessDays").GetProperty("denominator").GetInt32().Should().Be(0);
    }

    private HttpClient CreateAuthorizedClient(params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|read-staff", permissions));
        return client;
    }

    private async Task<PropertyOnboarding> SeedOnboardingAsync(DateTimeOffset openedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var partner = Partner.Create(Guid.NewGuid(), $"pre-{Guid.NewGuid():N}", "Read model partner", null, new LegalIdentifier(LegalIdentifierType.Other, "BR", Guid.NewGuid().ToString("N")), new Contact("Read Staff", "read.staff@example.test", "+5585999999999"), openedAt);
        var onboarding = PropertyOnboarding.Create(Guid.NewGuid(), partner.Id, partner.PreselectionId, new Property("Read model property", "recife-pe", new Address("Street", Guid.NewGuid().ToString("N")[..20], null, "District", "Recife", "PE", "50000-000", "BR")), openedAt, TimeSpan.FromDays(10));
        await dbContext.AddRangeAsync(partner, onboarding);
        await dbContext.SaveChangesAsync();
        return onboarding;
    }

    private async Task SeedAuditAsync(Guid onboardingId, string type, string occurredAt, IReadOnlyDictionary<string, string> metadata)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.BusinessAuditEntries.AddAsync(BusinessAuditEntry.Create("PropertyOnboarding", onboardingId.ToString(), "staff-001", type, "Safe audit summary.", DateTimeOffset.Parse(occurredAt), "correlation-001", metadata));
        await dbContext.SaveChangesAsync();
    }

    private async Task ClearInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE inventory.property_onboardings, inventory.partners, inventory.audit_entries CASCADE;");
    }
}
