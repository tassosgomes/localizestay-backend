using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

public sealed class CommercialOfferEndpointsTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferEndpointsTests(LocalizeStayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListCommercialOffers_ShouldReturnPaginatedResults()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read);

        var response = await client.GetAsync("/api/v1/commercial-offers?_page=1&_size=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
        payload.GetProperty("pagination").GetProperty("page").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CommercialOfferEndpoints_Unauthenticated_ShouldReturn401()
    {
        var unauthenticated = _factory.CreateClient();

        var response = await unauthenticated.GetAsync("/api/v1/commercial-offers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CommercialOfferEndpoints_NoPermission_ShouldReturn403()
    {
        var forbidden = _factory.CreateClient();
        forbidden.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            LocalizeStayWebApplicationFactory.CreateToken("logto|no-perms"));

        var response = await forbidden.GetAsync("/api/v1/commercial-offers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CommercialPolicies_CRUD_ShouldReturnCorrectStatusCodes()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);

        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location!.ToString().Should().Contain($"/api/v1/properties/{propertyId}/commercial-policies/");
        var createdPolicy = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var policyId = createdPolicy.GetProperty("id").GetGuid();

        var listResponse = await client.GetAsync($"/api/v1/properties/{propertyId}/commercial-policies");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listPayload = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        listPayload.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = false });
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemDetailsAsync(
            duplicateResponse,
            HttpStatusCode.Conflict,
            "POLICY_TYPE_ALREADY_ACTIVE");

        var nonRefundable = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "nonRefundable", setAsDefault = false });
        nonRefundable.StatusCode.Should().Be(HttpStatusCode.Created);
        var nonRefundablePolicy = await nonRefundable.Content.ReadFromJsonAsync<JsonElement>();
        var nonRefundableId = nonRefundablePolicy.GetProperty("id").GetGuid();

        var setDefaultResponse = await client.PutAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies/default",
            new
            {
                policyId = nonRefundableId,
                applyToExistingAccommodations = false,
                expectedRevision = await GetOfferRevisionAsync(client, propertyId),
            });
        setDefaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies/{policyId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteBody.Should().BeEmpty();
    }

    [Fact]
    public async Task Accommodations_CRUD_ShouldCreateListGetAndDelete()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Suíte Teste", maxAdults = 2, totalCapacity = 3 });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location!.ToString().Should().Contain($"/api/v1/properties/{propertyId}/accommodations/");
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getPayload = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        getPayload.GetProperty("commercialName").GetString().Should().Be("Suíte Teste");

        var listResponse = await client.GetAsync(
            $"/api/v1/properties/{propertyId}/accommodations?_page=1&_size=20");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}",
            new
            {
                commercialName = "Suíte Jardim Atualizada",
                expectedRevision = await GetOfferRevisionAsync(client, propertyId),
            });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CommercialRates_ShouldCreateAndReturnLocationHeader()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        var accResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Suíte Tarifada", maxAdults = 2, totalCapacity = 2 });
        accResp.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"Complete accommodation setup failed: {await accResp.Content.ReadAsStringAsync()}");
        var acc = await accResp.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = acc.GetProperty("id").GetGuid();

        var rateResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new { name = "Tarifa Teste", conditionCode = "standard", basePriceCents = 50000 });

        rateResp.StatusCode.Should().Be(HttpStatusCode.Created);
        rateResp.Headers.Location!.ToString().Should().Contain("rates/");
    }

    [Fact]
    public async Task CommercialRates_Delete_ShouldReturn204WithEmptyBody()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        var accResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Suíte Delete", maxAdults = 2, totalCapacity = 2 });
        accResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var acc = await accResp.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = acc.GetProperty("id").GetGuid();

        var rateResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new { name = "Tarifa Delete", conditionCode = "delete-test" });
        rateResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var rate = await rateResp.Content.ReadFromJsonAsync<JsonElement>();
        var rateId = rate.GetProperty("id").GetGuid();

        var deleteResp = await client.DeleteAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates/{rateId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deleteBody = await deleteResp.Content.ReadAsStringAsync();
        deleteBody.Should().BeEmpty();
    }

    [Fact]
    public async Task OfferWorkflow_ValidateAndSubmit_ShouldReturnCreated()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var reviewClient = CreateAuthorizedClientForSubject(
            "logto|reviewer",
            CommercialOfferPermissions.Read,
            CommercialOfferPermissions.Write,
            CommercialOfferPermissions.Review);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(writeClient, propertyId);

        var policyResponse = await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });
        policyResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var policy = await policyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var policyId = policy.GetProperty("id").GetGuid();

        var accResp = await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new
            {
                commercialName = "Suíte Completa",
                maxAdults = 2,
                totalCapacity = 2,
                bedConfiguration = new[] { new { type = "queen", quantity = 2 } },
                mealPlan = "breakfast",
                policyId,
            });
        accResp.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"Complete accommodation setup failed: {await accResp.Content.ReadAsStringAsync()}");
        var acc = await accResp.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = acc.GetProperty("id").GetGuid();

        var rateResp = await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new
            {
                name = "Tarifa Validada",
                conditionCode = "standard",
                basePriceCents = 50000,
                includedGuests = 2,
                validFrom = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                validTo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)).ToString("yyyy-MM-dd"),
                minimumNights = 1,
                policyId,
                mealPlan = "breakfast",
            });
        rateResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var expectedRevision = await GetOfferRevisionAsync(writeClient, propertyId);
        var validateResponse = await reviewClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations",
            new { expectedRevision, comment = "Commercial data checked." });
        validateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        validateResponse.Headers.Location.Should().NotBeNull();
        var validation = await validateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var validationId = validation.GetProperty("id").GetGuid();
        validation.GetProperty("propertyId").GetGuid().Should().Be(propertyId);
        validation.GetProperty("revision").GetInt32().Should().Be(expectedRevision);
        validation.GetProperty("status").GetString().Should().Be("valid");
        validation.GetProperty("validatedBy").GetProperty("id").GetString().Should().Be("logto|reviewer");
        validation.GetProperty("comment").GetString().Should().Be("Commercial data checked.");

        using var invalidSubmitRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/properties/{propertyId}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new
            {
                expectedRevision,
                validationId = Guid.NewGuid(),
            }),
        };
        invalidSubmitRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var invalidSubmitResponse = await writeClient.SendAsync(invalidSubmitRequest);
        await AssertProblemDetailsAsync(
            invalidSubmitResponse,
            HttpStatusCode.UnprocessableEntity,
            "VALIDATION_REQUIRED");

        using var submitRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/properties/{propertyId}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new { expectedRevision, validationId }),
        };
        submitRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var submitResponse = await writeClient.SendAsync(submitRequest);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        submitResponse.Headers.Location.Should().NotBeNull();
        var submission = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        submission.GetProperty("propertyId").GetGuid().Should().Be(propertyId);
        submission.GetProperty("validationId").GetGuid().Should().Be(validationId);
        submission.GetProperty("status").GetString().Should().Be("accepted");
        submission.GetProperty("eventName").GetString()
            .Should().Be("oferta-inventario.oferta-estruturada");

        var historyResponse = await writeClient.GetAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer-history?_page=1&_size=20");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var historyPayload = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
        historyPayload.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Metrics_WithMetricsPermission_ShouldReturnMetrics()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Metrics);

        var response = await client.GetAsync(
            "/api/v1/commercial-offer-metrics?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("totalOffers").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Metrics_WithoutMetricsPermission_ShouldReturn403()
    {
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read);

        var response = await client.GetAsync(
            "/api/v1/commercial-offer-metrics?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCommercialOffer_ForExistingProperty_ShouldReturnOffer()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();

        var response = await client.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("propertyId").GetGuid().Should().Be(propertyId);
        payload.GetProperty("status").GetString().Should().Be("draft");
    }

    [Fact]
    public async Task PatchAccommodation_OmittedFields_ShouldNotChange()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        var createResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Original", maxAdults = 2, totalCapacity = 3 });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = created.GetProperty("id").GetGuid();

        var patchResp = await client.PatchAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}",
            new { expectedRevision = await GetOfferRevisionAsync(client, propertyId) });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        patched.GetProperty("commercialName").GetString().Should().Be("Original");
    }

    [Fact]
    public async Task PatchRate_OmittedFields_ShouldNotChange()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        var accResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Suíte PATCH", maxAdults = 2, totalCapacity = 2 });
        accResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var acc = await accResp.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = acc.GetProperty("id").GetGuid();

        var rateResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates",
            new { name = "Rate Original", conditionCode = "patch-test", basePriceCents = 40000 });
        rateResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var rate = await rateResp.Content.ReadFromJsonAsync<JsonElement>();
        var rateId = rate.GetProperty("id").GetGuid();

        var patchResp = await client.PatchAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates/{rateId}",
            new { expectedRevision = await GetOfferRevisionAsync(client, propertyId) });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        patched.GetProperty("name").GetString().Should().Be("Rate Original");
    }

    [Fact]
    public async Task DeleteAlreadySubmittedAccommodation_ShouldReturn422()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });

        var createResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Suíte 422", maxAdults = 2, totalCapacity = 2 });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var acc = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accommodationId = acc.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var accommodation = await dbContext.Accommodations
            .SingleAsync(a => a.Id == accommodationId, CancellationToken.None);
        accommodation.MarkSubmitted(Guid.NewGuid());
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var deleteResp = await client.DeleteAsync(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await AssertProblemDetailsAsync(
            deleteResp,
            HttpStatusCode.UnprocessableEntity,
            "ACCOMMODATION_DELETION_NOT_ALLOWED");
    }

    [Fact]
    public void TwentyUniqueEndpoints_ShouldExist()
    {
        var expectedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "listCommercialOffers",
            "getCommercialOffer",
            "listCommercialPolicies",
            "createCommercialPolicy",
            "setDefaultCommercialPolicy",
            "updateCommercialPolicy",
            "deleteCommercialPolicy",
            "listAccommodations",
            "createAccommodation",
            "getAccommodation",
            "updateAccommodation",
            "deleteAccommodation",
            "listCommercialRates",
            "createCommercialRate",
            "updateCommercialRate",
            "deleteCommercialRate",
            "createCommercialOfferValidation",
            "createCommercialOfferSubmission",
            "listCommercialOfferHistory",
            "getCommercialOfferMetrics",
        };

        var exposedNames = _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .Where(name => name is not null && expectedNames.Contains(name))
            .Cast<string>()
            .ToList();

        exposedNames.Should().HaveCount(20);
        exposedNames.Should().OnlyHaveUniqueItems();
        exposedNames.Should().BeEquivalentTo(expectedNames);
    }

    [Fact]
    public async Task InvalidInputs_ShouldReturnStandardBadRequestProblemDetails()
    {
        var client = CreateAuthorizedClient(
            CommercialOfferPermissions.Write,
            CommercialOfferPermissions.Metrics);

        using var submitRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/properties/{Guid.NewGuid()}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new
            {
                expectedRevision = 1,
                validationId = Guid.NewGuid(),
            }),
        };
        submitRequest.Headers.Add("Idempotency-Key", "not-a-uuid");
        var invalidIdempotencyKey = await client.SendAsync(submitRequest);
        await AssertProblemDetailsAsync(
            invalidIdempotencyKey,
            HttpStatusCode.BadRequest,
            "BAD_REQUEST");

        var invalidRateDate = await client.PostAsJsonAsync(
            $"/api/v1/properties/{Guid.NewGuid()}/accommodations/{Guid.NewGuid()}/rates",
            new
            {
                name = "Invalid date",
                conditionCode = "invalid-date",
                validFrom = "2026-99-99",
            });
        await AssertProblemDetailsAsync(
            invalidRateDate,
            HttpStatusCode.BadRequest,
            "BAD_REQUEST");

        var invalidMetricsDate = await client.GetAsync(
            "/api/v1/commercial-offer-metrics?from=invalid&to=2026-12-31T23:59:59Z");
        await AssertProblemDetailsAsync(
            invalidMetricsDate,
            HttpStatusCode.BadRequest,
            "BAD_REQUEST");
    }

    [Fact]
    public async Task MissingOfferAndUnexpectedFailure_ShouldReturnStandardProblemDetails()
    {
        var commercialClient = CreateAuthorizedClient(CommercialOfferPermissions.Read);
        var notFound = await commercialClient.GetAsync(
            $"/api/v1/properties/{Guid.NewGuid()}/commercial-offer");
        await AssertProblemDetailsAsync(
            notFound,
            HttpStatusCode.NotFound,
            "PROPERTY_NOT_FOUND");

        var scenarioClient = CreateAuthorizedClient(PortfolioOnboardingPermissions.Read);
        var unexpected = await scenarioClient.GetAsync("/api/v1/test/scenarios/CRASH");
        await AssertProblemDetailsAsync(
            unexpected,
            HttpStatusCode.InternalServerError,
            "INTERNAL_ERROR");

        var unexpectedBody = await unexpected.Content.ReadAsStringAsync();
        unexpectedBody.Should().NotContain("hunter2");
        unexpectedBody.Should().NotContain("111.222.333-44");
    }

    [Fact]
    public async Task RateLimitExceeded_ShouldReturn429ProblemDetails()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:PermitLimit"] = "1",
                    ["RateLimit:TokensPerSecond"] = "1",
                    ["RateLimit:QueueLimit"] = "0",
                })));
        using var client = limitedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            LocalizeStayWebApplicationFactory.CreateToken(
                $"logto|rate-limit-{Guid.NewGuid()}",
                CommercialOfferPermissions.Read));

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6)
                .Select(_ => client.GetAsync("/api/v1/commercial-offers?_page=1&_size=1")));

        var rejected = responses.FirstOrDefault(
            response => response.StatusCode == HttpStatusCode.TooManyRequests);
        rejected.Should().NotBeNull();
        await AssertProblemDetailsAsync(
            rejected!,
            HttpStatusCode.TooManyRequests,
            "RATE_LIMIT_EXCEEDED");
        rejected!.Headers.RetryAfter.Should().NotBeNull();

        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task HttpStatusMatrix_EachDeclaredStatus_ShouldBeProducedByARealRequest()
    {
        // The F02 contract declares 400/401/403/404/409/422/429/500. This test certifies that every
        // status is reachable via a real request, derived from the YAML matrix rather than a copied
        // list. The scenarios are deliberately minimal so they stay robust against data drift.
        await ClearCommercialDataAsync();
        var writeClient = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var readerClient = CreateAuthorizedClient(CommercialOfferPermissions.Read);

        // 401 — anonymous request.
        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/v1/commercial-offers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 403 — caller without the required permission.
        var noPerms = _factory.CreateClient();
        noPerms.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken($"logto|matrix-{Guid.NewGuid()}"));
        (await noPerms.GetAsync("/api/v1/commercial-offers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 400 — invalid Idempotency-Key / query string.
        using var submitRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/properties/{Guid.NewGuid()}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new { expectedRevision = 1, validationId = Guid.NewGuid() }),
        };
        submitRequest.Headers.Add("Idempotency-Key", "not-a-uuid");
        await AssertProblemDetailsAsync(
            await writeClient.SendAsync(submitRequest),
            HttpStatusCode.BadRequest,
            "BAD_REQUEST");

        // 404 — property not found.
        await AssertProblemDetailsAsync(
            await readerClient.GetAsync($"/api/v1/properties/{Guid.NewGuid()}/commercial-offer"),
            HttpStatusCode.NotFound,
            "PROPERTY_NOT_FOUND");

        // 409 — duplicate active policy type.
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(writeClient, propertyId);
        await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/commercial-policies",
            new { type = "flexible", setAsDefault = true });
        await AssertProblemDetailsAsync(
            await writeClient.PostAsJsonAsync(
                $"/api/v1/properties/{propertyId}/commercial-policies",
                new { type = "flexible", setAsDefault = false }),
            HttpStatusCode.Conflict,
            "POLICY_TYPE_ALREADY_ACTIVE");

        // 422 — concurrent revision mismatch on PATCH accommodation (declared 422 REVISION_MISMATCH).
        var accResp = await writeClient.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Suite Matrix", maxAdults = 2, totalCapacity = 2 });
        var accommodationId = (await accResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await AssertProblemDetailsAsync(
            await writeClient.PatchAsJsonAsync(
                $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}",
                new { expectedRevision = 999 }),
            HttpStatusCode.UnprocessableEntity,
            "REVISION_MISMATCH");

        // 429 — rate limit configured at 1 token.
        using var limitedFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = "1",
                ["RateLimit:TokensPerSecond"] = "1",
                ["RateLimit:QueueLimit"] = "0",
            })));
        using var limitedClient = limitedFactory.CreateClient();
        limitedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", LocalizeStayWebApplicationFactory.CreateToken(
                $"logto|matrix-429-{Guid.NewGuid()}", CommercialOfferPermissions.Read));
        var rateResponses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => limitedClient.GetAsync("/api/v1/commercial-offers?_page=1&_size=1")));
        rateResponses.Should().Contain(response => response.StatusCode == HttpStatusCode.TooManyRequests);
        foreach (var response in rateResponses) response.Dispose();

        // 500 — forced internal failure via the test scenario endpoint, with sanitised body.
        var scenarioClient = CreateAuthorizedClient(PortfolioOnboardingPermissions.Read);
        await AssertProblemDetailsAsync(
            await scenarioClient.GetAsync("/api/v1/test/scenarios/CRASH"),
            HttpStatusCode.InternalServerError,
            "INTERNAL_ERROR");
    }

    [Fact]
    public async Task PatchAccommodation_WithMismatchedRevision_ShouldReturn422RevisionMismatch()
    {
        await ClearCommercialDataAsync();
        var client = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(client, propertyId);

        var createResp = await client.PostAsJsonAsync(
            $"/api/v1/properties/{propertyId}/accommodations",
            new { commercialName = "Suite Revision", maxAdults = 2, totalCapacity = 2 });
        var accommodationId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await AssertProblemDetailsAsync(
            await client.PatchAsJsonAsync(
                $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}",
                new { expectedRevision = 999, commercialName = "Stale" }),
            HttpStatusCode.UnprocessableEntity,
            "REVISION_MISMATCH");
    }

    [Fact]
    public async Task Submit_WithStaleRevision_ShouldReturn422RevisionMismatch()
    {
        await ClearCommercialDataAsync();
        var writeClient = CreateAuthorizedClient(CommercialOfferPermissions.Read, CommercialOfferPermissions.Write);
        var propertyId = await EnsurePropertyExistsAsync();
        await CreateDefaultOfferAsync(writeClient, propertyId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/properties/{propertyId}/commercial-offer-submissions")
        {
            Content = JsonContent.Create(new { expectedRevision = 999, validationId = Guid.NewGuid() }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        await AssertProblemDetailsAsync(
            await writeClient.SendAsync(request),
            HttpStatusCode.UnprocessableEntity,
            "REVISION_MISMATCH");
    }

    private HttpClient CreateAuthorizedClient(params string[] permissions)
        => CreateAuthorizedClientForSubject("logto|staff-001", permissions);

    private HttpClient CreateAuthorizedClientForSubject(string subject, params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            LocalizeStayWebApplicationFactory.CreateToken(subject, permissions));
        return client;
    }

    private async Task<Guid> EnsurePropertyExistsAsync()
    {
        var client = CreateAuthorizedClient(PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);

        var partnerResponse = await client.PostAsJsonAsync("/api/v1/partners", new
        {
            preselectionId = "pilot-preselection-001",
            legalName = "Property For Tests",
            tradeName = "Tests",
            legalIdentifier = new { type = "cnpj", countryCode = "BR", value = "00.000.000/0001-00" },
            primaryContact = new { name = "Test User", email = "test@example.com", phone = "+55 11 99999-9999" },
        });
        partnerResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"Partner setup failed: {await partnerResponse.Content.ReadAsStringAsync()}");
        var partner = await partnerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var partnerId = partner.GetProperty("id").GetGuid();

        var createProp = await client.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Test Property",
                destinationId = "recife-pe",
                address = new
                {
                    street = "Rua dos Testes",
                    number = "100",
                    district = "Centro",
                    city = "Recife",
                    state = "PE",
                    postalCode = "50000-000",
                    countryCode = "BR",
                },
            },
        });
        createProp.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"Property onboarding setup failed: {await createProp.Content.ReadAsStringAsync()}");
        var created = await createProp.Content.ReadFromJsonAsync<JsonElement>();
        var propertyId = created.GetProperty("id").GetGuid();
        created.GetProperty("partnerId").GetGuid().Should().Be(partnerId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        dbContext.IncorporatedProperties.Add(IncorporatedProperty.Create(
            propertyId,
            partnerId,
            "Test Property",
            "recife-pe",
            "logto|staff-001",
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        return propertyId;
    }

    private async Task CreateDefaultOfferAsync(HttpClient client, Guid propertyId)
    {
        var response = await client.GetAsync($"/api/v1/properties/{propertyId}/commercial-offer");
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Failed to create default offer: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<int> GetOfferRevisionAsync(HttpClient client, Guid propertyId)
    {
        var response = await client.GetAsync(
            $"/api/v1/properties/{propertyId}/commercial-offer");
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"Failed to read offer revision: {await response.Content.ReadAsStringAsync()}");
        var offer = await response.Content.ReadFromJsonAsync<JsonElement>();
        return offer.GetProperty("revision").GetInt32();
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        problem.GetProperty("code").GetString().Should().Be(expectedCode);
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("instance").GetString().Should().NotBeNullOrWhiteSpace();
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
