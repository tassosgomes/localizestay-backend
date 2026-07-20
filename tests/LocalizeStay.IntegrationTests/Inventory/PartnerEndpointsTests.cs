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

public sealed class PartnerEndpointsTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public PartnerEndpointsTests(LocalizeStayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PartnerEndpoints_WithAuthorizedRequest_ShouldCreateListMaskAndRejectDuplicate()
    {
        await ClearInventoryAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|staff-001", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write));
        var request = new { preselectionId = "pilot-preselection-001", legalName = "Hotel Integration", tradeName = "Integration", legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "12.345.678/0001-90" }, primaryContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+55 11 99999-9999" } };

        var created = await client.PostAsJsonAsync("/api/v1/partners", request);
        var duplicate = await client.PostAsJsonAsync("/api/v1/partners", request);
        var listed = await client.GetAsync("/api/v1/partners?_page=1&_size=20&search=Integration");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location!.ToString().Should().StartWith("/api/v1/partners/");
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("DUPLICATE_LEGAL_IDENTIFIER");
        problem.GetProperty("metadata").TryGetProperty("conflictingResourceId", out _).Should().BeTrue();
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await listed.Content.ReadFromJsonAsync<JsonElement>();
        response.GetProperty("data")[0].GetProperty("maskedLegalIdentifier").GetString().Should().NotBe("12.345.678/0001-90");
    }

    [Fact]
    public async Task ListPartners_WithMultipleMatches_ShouldApplyRealPagination()
    {
        await ClearInventoryAsync();
        var client = CreateAuthorizedClient(PortfolioOnboardingPermissions.Write, PortfolioOnboardingPermissions.Read);
        const string marker = "Pagination Hotel";
        await CreatePartnerAsync(client, marker + " One", "12.345.678/0001-90");
        await CreatePartnerAsync(client, marker + " Two", "98.765.432/0001-10");

        var response = await client.GetAsync($"/api/v1/partners?_page=2&_size=1&search={Uri.EscapeDataString(marker)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("data").GetArrayLength().Should().Be(1);
        payload.GetProperty("pagination").GetProperty("page").GetInt32().Should().Be(2);
        payload.GetProperty("pagination").GetProperty("size").GetInt32().Should().Be(1);
        payload.GetProperty("pagination").GetProperty("total").GetInt32().Should().Be(2);
        payload.GetProperty("pagination").GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task PartnerEndpoints_WithoutJwtOrPermission_ShouldReturn401And403()
    {
        var unauthenticated = _factory.CreateClient();
        var forbidden = _factory.CreateClient();
        forbidden.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|staff-002"));

        var unauthorizedResponse = await unauthenticated.GetAsync("/api/v1/partners");
        var forbiddenResponse = await forbidden.GetAsync("/api/v1/partners");

        unauthorizedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreatePartner_WithConcurrentDuplicateRequests_ShouldReturnOneCreatedAndOneConflict()
    {
        await ClearInventoryAsync();
        var firstClient = CreateAuthorizedClient(PortfolioOnboardingPermissions.Write);
        var secondClient = CreateAuthorizedClient(PortfolioOnboardingPermissions.Write);
        const string identifier = "12.345.678/0001-90";
        var request = CreateRequest($"Concurrent {Guid.NewGuid():N}", identifier);

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/partners", request),
            secondClient.PostAsJsonAsync("/api/v1/partners", request));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("DUPLICATE_LEGAL_IDENTIFIER");
        problem.GetProperty("metadata").TryGetProperty("conflictingResourceId", out _).Should().BeTrue();
    }

    private HttpClient CreateAuthorizedClient(params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|staff-001", permissions));
        return client;
    }

    private static async Task CreatePartnerAsync(HttpClient client, string legalName, string identifier)
    {
        var response = await client.PostAsJsonAsync("/api/v1/partners", CreateRequest(legalName, identifier));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static object CreateRequest(string legalName, string identifier) => new
    {
        preselectionId = "pilot-preselection-001",
        legalName,
        tradeName = legalName,
        legalIdentifier = new { type = "cnpj", countryCode = "BR", value = identifier },
        primaryContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+55 11 99999-9999" },
    };

    private async Task ClearInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE inventory.partners, inventory.audit_entries CASCADE;");
    }
}
