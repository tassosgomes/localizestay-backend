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

public sealed class PropertyOnboardingEndpointsTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public PropertyOnboardingEndpointsTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PropertyOnboardingEndpoints_ShouldCreateFlagSimilarityAndApplyFiltersAndPagination()
    {
        await ClearInventoryAsync();
        var client = CreateAuthorizedClient();
        var partnerId = await CreatePartnerAsync(client, "Integration Onboarding Partner", "12.345.678/0001-90");
        var created = await client.PostAsJsonAsync("/api/v1/property-onboardings", CreateOnboardingRequest(partnerId, "Pousada Integration", "10"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location!.ToString().Should().StartWith("/api/v1/property-onboardings/");
        var createdPayload = await created.Content.ReadFromJsonAsync<JsonElement>();
        createdPayload.GetProperty("readinessGates").GetArrayLength().Should().Be(6);
        createdPayload.GetProperty("lifecycleStatus").GetString().Should().Be("inProgress");
        var similar = await client.PostAsJsonAsync("/api/v1/property-onboardings", CreateOnboardingRequest(partnerId, "Pousada Integration", "11"));
        similar.StatusCode.Should().Be(HttpStatusCode.Created);
        var similarPayload = await similar.Content.ReadFromJsonAsync<JsonElement>();
        similarPayload.GetProperty("duplicateReview").GetProperty("required").GetBoolean().Should().BeTrue();
        var candidates = similarPayload.GetProperty("duplicateReview").GetProperty("candidates");
        candidates.GetArrayLength().Should().Be(1);
        var candidate = candidates[0];
        candidate.GetProperty("propertyId").GetGuid().Should().NotBeEmpty();
        candidate.GetProperty("name").GetString().Should().Be("Pousada Integration");
        candidate.GetProperty("addressSummary").GetString().Should().NotBeNullOrWhiteSpace();
        candidate.GetProperty("matchReasons")[0].GetString().Should().Be("similarName");
        candidate.GetProperty("similarityScore").GetDecimal().Should().Be(1m);
        similarPayload.GetProperty("duplicateReview").GetProperty("latestDecision").ValueKind.Should().Be(JsonValueKind.Null);

        var list = await client.GetAsync($"/api/v1/property-onboardings?_page=1&_size=1&partnerId={partnerId}&destinationId=recife-pe&lifecycleStatus=inProgress&readinessStatus=blocked&sort=propertyName&order=asc");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listPayload = await list.Content.ReadFromJsonAsync<JsonElement>();
        listPayload.GetProperty("data").GetArrayLength().Should().Be(1);
        listPayload.GetProperty("pagination").GetProperty("total").GetInt32().Should().Be(2);
        listPayload.GetProperty("pagination").GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task CreatePropertyOnboarding_WithInvalidDestinationOrActiveCycle_ShouldReturnContractErrors()
    {
        await ClearInventoryAsync();
        var client = CreateAuthorizedClient();
        var partnerId = await CreatePartnerAsync(client, "Conflict Onboarding Partner", "98.765.432/0001-10");
        var invalidDestination = await client.PostAsJsonAsync("/api/v1/property-onboardings", CreateOnboardingRequest(partnerId, "Invalid Destination", "20", "not-approved"));
        var created = await client.PostAsJsonAsync("/api/v1/property-onboardings", CreateOnboardingRequest(partnerId, "Same Property", "21"));
        var conflict = await client.PostAsJsonAsync("/api/v1/property-onboardings", CreateOnboardingRequest(partnerId, "Different Name", "21"));

        invalidDestination.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalidDestination.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("DESTINATION_NOT_APPROVED");
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("ACTIVE_ONBOARDING_CYCLE_EXISTS");
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|staff-001", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write));
        return client;
    }

    private static object CreateOnboardingRequest(Guid partnerId, string name, string number, string destinationId = "recife-pe") => new
    {
        partnerId,
        preselectionId = "pilot-preselection-001",
        property = new { name, destinationId, address = new { street = "Rua Integration", number, complement = (string?)null, district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" } },
    };

    private static async Task<Guid> CreatePartnerAsync(HttpClient client, string legalName, string identifier)
    {
        var response = await client.PostAsJsonAsync("/api/v1/partners", new { preselectionId = "pilot-preselection-001", legalName, tradeName = legalName, legalIdentifier = new { type = "cnpj", countryCode = "BR", value = identifier }, primaryContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+55 11 99999-9999" } });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task ClearInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE inventory.property_onboardings, inventory.partners, inventory.audit_entries CASCADE;");
    }
}
