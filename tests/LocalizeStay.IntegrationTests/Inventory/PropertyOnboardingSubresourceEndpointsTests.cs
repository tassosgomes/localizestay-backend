using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

public sealed class PropertyOnboardingSubresourceEndpointsTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;
    public PropertyOnboardingSubresourceEndpointsTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SubresourceEndpoints_WithAuthorizedStaff_ShouldCreateIssueAndRejectInvalidGateEvidence()
    {
        await ClearInventoryAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|staff-001", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write));
        var partner = await client.PostAsJsonAsync("/api/v1/partners", new { preselectionId = "pilot-preselection-001", legalName = "Readiness Partner", legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "12.345.678/0001-90" }, primaryContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+5511999999999" } });
        var partnerId = (await partner.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var onboarding = await client.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Readiness Hotel",
                destinationId = "recife-pe",
                address = new
                {
                    street = "Rua Test",
                    number = "10",
                    district = "Centro",
                    city = "Recife",
                    state = "PE",
                    postalCode = "50000-000",
                    countryCode = "BR",
                },
            },
        });
        var onboardingId = (await onboarding.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var issue = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/pending-issues", new { description = "Confirm formal authorization.", ownerType = "legal" });
        var invalidGate = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/authorizedContact", new { status = "validated", authorizedContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+5511999999999" } });

        issue.StatusCode.Should().Be(HttpStatusCode.Created);
        invalidGate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalidGate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_GATE_EVIDENCE");
    }

    [Fact]
    public async Task UpdateReadinessGate_WithoutOperationalChannelTestedAt_ShouldReturnBadRequest()
    {
        var (client, onboardingId) = await CreateOnboardingAsync();

        var response = await client.PatchAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/readiness-gates/operationalChannel", new
        {
            status = "validated",
            operationalChannelTest = new
            {
                channel = "email",
                contact = "operations@example.com",
                resultSummary = "Operational test confirmed.",
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCommunicationRecord_WithoutTimestamps_ShouldReturnBadRequest()
    {
        var (client, onboardingId) = await CreateOnboardingAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/communication-records", new
        {
            channel = "email",
            resultSummary = "Communication was processed by the operations team.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("BAD_REQUEST");
    }

    [Fact]
    public async Task CreateDuplicateReview_WithRepeatedIdempotencyKey_ShouldReplayAndCloseDuplicateOnboarding()
    {
        var (client, onboardingId) = await CreateOnboardingAsync();
        var existingPropertyId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var onboarding = await dbContext.PropertyOnboardings.SingleAsync(item => item.Id == onboardingId);
            onboarding.FlagDuplicateReviewRequired();
            await dbContext.SaveChangesAsync();
        }

        var request = new
        {
            idempotencyKey,
            decision = "duplicateOfExistingProperty",
            existingPropertyId,
            justification = "The property matches an existing active record.",
        };

        var first = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/duplicate-reviews", request);
        var replay = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/duplicate-reviews", request);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var closedOnboarding = await verificationDbContext.PropertyOnboardings.Include(item => item.DuplicateReviews).SingleAsync(item => item.Id == onboardingId);
        closedOnboarding.DuplicateReviews.Should().ContainSingle();
        closedOnboarding.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.Closed);
        closedOnboarding.ReasonCode.Should().Be(CloseReasonCode.DuplicateProperty);
    }

    [Theory]
    [InlineData("assigneeId")]
    [InlineData("targetAt")]
    public async Task UpdatePendingIssue_WithExplicitNull_ShouldClearNullableField(string field)
    {
        var (client, onboardingId) = await CreateOnboardingAsync();
        var created = await client.PostAsJsonAsync($"/api/v1/property-onboardings/{onboardingId}/pending-issues", new
        {
            description = "Confirm formal authorization.",
            ownerType = "legal",
            assigneeId = "logto|staff-002",
            targetAt = DateTimeOffset.UtcNow.AddDays(2),
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var issueId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await client.PatchAsync(
            $"/api/v1/property-onboardings/{onboardingId}/pending-issues/{issueId}",
            new StringContent($"{{\"{field}\":null}}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var issue = await response.Content.ReadFromJsonAsync<JsonElement>();
        issue.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task<(HttpClient Client, Guid OnboardingId)> CreateOnboardingAsync()
    {
        await ClearInventoryAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken("logto|staff-001", PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write));
        var partner = await client.PostAsJsonAsync("/api/v1/partners", new { preselectionId = "pilot-preselection-001", legalName = "Readiness Partner", legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "12.345.678/0001-90" }, primaryContact = new { name = "Jane Doe", email = "jane@example.com", phone = "+5511999999999" } });
        var partnerId = (await partner.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var onboarding = await client.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Readiness Hotel",
                destinationId = "recife-pe",
                address = new { street = "Rua Test", number = "10", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" },
            },
        });

        return (client, (await onboarding.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    private async Task ClearInventoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE inventory.property_onboardings, inventory.partners, inventory.audit_entries CASCADE;");
    }
}
