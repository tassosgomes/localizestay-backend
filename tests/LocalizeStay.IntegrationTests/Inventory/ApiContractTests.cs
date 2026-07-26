using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Guards the API-first boundary for F01 (partners &amp; property onboarding). The OpenAPI YAML remains
/// the sole source of truth: this test delegates parsing to <see cref="OpenApiContractDocument"/> and
/// verifies every operation's identity, HTTP surface and declared payload shapes. F02 reuses the same
/// parser via <c>CommercialOfferApiContractTests</c>.
/// </summary>
public sealed class ApiContractTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public ApiContractTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void OpenApiContract_DeclaredOperations_ShouldMatchExposedInventoryHttpMetadata()
    {
        // Arrange
        var lines = File.ReadAllLines(ContractPath);
        var contract = ReadContract(lines);
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        // Assert
        contract.Operations.Should().HaveCount(18, "the F01 OpenAPI contract defines exactly 18 operations");
        contract.Operations.Select(operation => operation.OperationId).Should().OnlyHaveUniqueItems();
        contract.Operations.SelectMany(operation => operation.Responses.Values).Select(response => response.SchemaName).Should()
            .OnlyContain(schema => contract.Schemas.ContainsKey(schema), "every response schema referenced by an operation must be declared by the YAML contract");

        foreach (var operation in contract.Operations)
        {
            var endpoint = endpoints.SingleOrDefault(candidate =>
                candidate.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == operation.OperationId);

            endpoint.Should().NotBeNull($"{operation.OperationId} must be exposed with its contract operationId");
            endpoint!.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Should().ContainSingle()
                .Which.Should().Be(operation.Method);
            Normalize(endpoint.RoutePattern.RawText).Should().Be(Normalize("/api/v1" + operation.Path));

            var responseMetadata = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
            responseMetadata.Select(metadata => metadata.StatusCode).Should().Contain(operation.Responses.Keys,
                $"{operation.OperationId} must declare every contract response status");
            responseMetadata.Where(metadata => operation.SuccessResponses.Select(response => response.StatusCode).Contains(metadata.StatusCode)).Should()
                .OnlyContain(metadata => metadata.ContentTypes.Contains("application/json"),
                    $"{operation.OperationId} successful responses must be JSON");
            responseMetadata.SelectMany(metadata => metadata.ContentTypes).Should().Contain(operation.Responses.Values.SelectMany(response => response.ContentTypes),
                $"{operation.OperationId} must expose every response content type declared by the contract");

            foreach (var successResponse in operation.SuccessResponses)
            {
                var metadata = responseMetadata.FirstOrDefault(item => item.StatusCode == successResponse.StatusCode);
                metadata.Should().NotBeNull($"{operation.OperationId} must publish the {successResponse.SchemaName} success schema");
                metadata!.Type.Should().NotBeNull($"{operation.OperationId} cannot satisfy {successResponse.SchemaName} with an untyped response");
                AssertJsonTypeMatchesSchema(metadata.Type!, successResponse.SchemaName, contract, operation.OperationId);
            }

            if (operation.RequestContentTypes.Count > 0)
            {
                var requestMetadata = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
                requestMetadata.Should().NotBeNull($"{operation.OperationId} accepts the request body declared in the contract");
                requestMetadata!.ContentTypes.Should().Contain(operation.RequestContentTypes);
                requestMetadata.RequestType.Should().NotBeNull();
            }

            if (operation.RequiresLocationHeader)
            {
                operation.SuccessResponses.Select(response => response.StatusCode).Should().Contain(201, $"{operation.OperationId} creates a resource");
                responseMetadata.Should().Contain(metadata => metadata.StatusCode == 201 && metadata.Type != null,
                    $"{operation.OperationId} must return the created resource alongside its Location header");
            }
        }
    }

    [Fact]
    public async Task OpenApiContract_DeclaredOperations_ShouldRejectAnonymousHttpRequestsAsSpecified()
    {
        // Arrange: the scenario matrix is derived from the OpenAPI YAML, never a copied endpoint list.
        var contract = ReadContract(File.ReadLines(ContractPath));
        using var client = _factory.CreateClient();

        // Act & Assert
        foreach (var operation in contract.Operations)
        {
            operation.Responses.Keys.Should().Contain((int)HttpStatusCode.Unauthorized,
                $"{operation.OperationId} must declare the common protected-endpoint scenario");

            using var request = new HttpRequestMessage(
                new HttpMethod(operation.Method),
                BuildConcretePath(operation.Path));
            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"{operation.OperationId} {operation.Method} {operation.Path} is protected by the YAML contract");
            await AssertProblemAsync(response, operation, HttpStatusCode.Unauthorized, contract);
        }
    }

    [Fact]
    public async Task OpenApiContract_DeclaredProblemStatuses_ShouldBeProducedByRealInventoryHttpRequests()
    {
        // Assertions remain tied to YAML operation/status declarations; the requests deliberately
        // use real Inventory endpoints rather than the test-only scenario surface.
        var contract = ReadContract(File.ReadLines(ContractPath));
        var listPartners = contract.Operations.Single(operation => operation.OperationId == "listPartners");
        var createPartner = contract.Operations.Single(operation => operation.OperationId == "createPartner");
        var getPartner = contract.Operations.Single(operation => operation.OperationId == "getPartner");
        var createOnboarding = contract.Operations.Single(operation => operation.OperationId == "createPropertyOnboarding");
        await EnsureInventoryMigrationAsync();
        using var anonymousClient = _factory.CreateClient();
        using var forbiddenClient = CreateAuthorizedClient();
        using var writerClient = CreateAuthorizedClient(PortfolioOnboardingPermissions.Read, PortfolioOnboardingPermissions.Write);

        await AssertProblemAsync(await anonymousClient.GetAsync("/api/v1/partners"), listPartners, HttpStatusCode.Unauthorized, contract);
        await AssertProblemAsync(await forbiddenClient.GetAsync("/api/v1/partners"), listPartners, HttpStatusCode.Forbidden, contract);
        await AssertProblemAsync(await writerClient.PostAsJsonAsync("/api/v1/partners", new { }), createPartner, HttpStatusCode.BadRequest, contract);
        await AssertProblemAsync(await writerClient.GetAsync($"/api/v1/partners/{Guid.NewGuid()}"), getPartner, HttpStatusCode.NotFound, contract);

        var legalIdentifier = Guid.NewGuid().ToString("N");
        var request = new
        {
            preselectionId = "pilot-preselection-001",
            legalName = "Contract status certification partner",
            legalIdentifier = new { type = "other", countryCode = "BR", value = legalIdentifier },
            primaryContact = new { name = "Contract Operator", email = "contract@example.test", phone = "+5585999999999" },
        };
        var created = await writerClient.PostAsJsonAsync("/api/v1/partners", request);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        created.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        await AssertProblemAsync(await writerClient.PostAsJsonAsync("/api/v1/partners", request), createPartner, HttpStatusCode.Conflict, contract);

        var partnerId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var invalidDestination = await writerClient.PostAsJsonAsync("/api/v1/property-onboardings", new
        {
            partnerId,
            preselectionId = "pilot-preselection-001",
            property = new
            {
                name = "Contract status certification property",
                destinationId = "destination-not-approved-for-pilot",
                address = new { street = "Rua Contract", number = "1", district = "Centro", city = "Recife", state = "PE", postalCode = "50000-000", countryCode = "BR" },
            },
        });
        await AssertProblemAsync(invalidDestination, createOnboarding, HttpStatusCode.UnprocessableEntity, contract);
    }

    [Fact]
    public async Task OpenApiContract_DeclaredRateLimitResponse_ShouldBeProducedByAnF01Route()
    {
        var contract = ReadContract(File.ReadLines(ContractPath));
        var operation = contract.Operations.Single(item => item.OperationId == "listPartners");
        using var limitedFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = "1",
                ["RateLimit:TokensPerSecond"] = "1",
                ["RateLimit:QueueLimit"] = "0",
            })));
        await EnsureInventoryMigrationAsync(limitedFactory.Services);
        using var client = limitedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalizeStayWebApplicationFactory.CreateToken(
            "logto|api-contract-rate-limit", PortfolioOnboardingPermissions.Read));

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.GetAsync("/api/v1/partners")));
        var limited = responses.FirstOrDefault(response => response.StatusCode == HttpStatusCode.TooManyRequests);
        limited.Should().NotBeNull("the F01 listPartners operation declares a 429 response in api-contract.yaml");
        await AssertProblemAsync(limited!, operation, HttpStatusCode.TooManyRequests, contract);

        foreach (var response in responses.Where(response => !ReferenceEquals(response, limited))) response.Dispose();
    }

    private HttpClient CreateAuthorizedClient(params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            LocalizeStayWebApplicationFactory.CreateToken("logto|api-contract", permissions));
        return client;
    }

    private Task EnsureInventoryMigrationAsync() => EnsureInventoryMigrationAsync(_factory.Services);

    private static async Task EnsureInventoryMigrationAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, ContractOperation operation, HttpStatusCode expectedStatus, Contract contract)
    {
        using (response)
        {
            operation.Responses.Should().ContainKey((int)expectedStatus,
                $"{operation.OperationId} declares {(int)expectedStatus} in api-contract.yaml");
            var expectedResponse = operation.Responses[(int)expectedStatus];
            response.StatusCode.Should().Be(expectedStatus, operation.OperationId);
            response.Content.Headers.ContentType?.MediaType.Should().Be(expectedResponse.ContentTypes.Single());
            foreach (var header in expectedResponse.Headers)
            {
                response.Headers.Should().Contain(item => string.Equals(item.Key, header, StringComparison.OrdinalIgnoreCase),
                    $"{operation.OperationId} {(int)expectedStatus} declares the {header} header");
            }
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            AssertJsonElementMatchesSchema(body, expectedResponse.SchemaName, contract, $"{operation.OperationId} {(int)expectedStatus}");
            body.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
            body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            body.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
            body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    private static string ContractPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.tasks/prd-incorporar-parceiros-e-propriedades/api-contract.yaml"));

    private static Contract ReadContract(IEnumerable<string> lines) => OpenApiContractDocument.Parse(lines);

    private static void AssertJsonTypeMatchesSchema(Type responseType, string schemaName, Contract contract, string operationId, HashSet<(Type Type, string Schema)>? visited = null)
    {
        visited ??= [];
        if (!visited.Add((responseType, schemaName))) return;
        var schema = contract.Schemas[schemaName];
        var typeInfo = JsonSerializerOptions.Web.GetTypeInfo(responseType) as JsonTypeInfo;
        typeInfo.Should().NotBeNull($"{operationId} must serialize {schemaName} as JSON");
        foreach (var requiredProperty in schema.RequiredProperties)
        {
            var jsonProperty = typeInfo!.Properties.SingleOrDefault(property => string.Equals(property.Name, requiredProperty, StringComparison.Ordinal));
            jsonProperty.Should().NotBeNull($"{operationId} response type {responseType.Name} must serialize required {schemaName}.{requiredProperty}");
            AssertJsonPropertyMatchesSchema(jsonProperty!, schema.Properties[requiredProperty], contract, operationId, visited);
        }
    }

    private static void AssertJsonPropertyMatchesSchema(JsonPropertyInfo jsonProperty, ContractProperty expected, Contract contract, string operationId, HashSet<(Type Type, string Schema)> visited)
    {
        var propertyType = Nullable.GetUnderlyingType(jsonProperty.PropertyType) ?? jsonProperty.PropertyType;
        if (!string.IsNullOrEmpty(expected.ItemsReference))
        {
            propertyType.Should().Implement(typeof(System.Collections.IEnumerable), $"{operationId} must serialize {jsonProperty.Name} as the contract array");
            var itemType = propertyType.IsArray ? propertyType.GetElementType()! : propertyType.GetGenericArguments().FirstOrDefault()!;
            AssertJsonTypeMatchesSchema(itemType, expected.ItemsReference, contract, operationId, visited);
        }
        else if (!string.IsNullOrEmpty(expected.Reference)) AssertJsonTypeMatchesSchema(propertyType, expected.Reference, contract, operationId, visited);
        else if (!string.IsNullOrEmpty(expected.Type))
        {
            var expectedType = expected.Type.Replace("[", string.Empty, StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal).Split(',')[0].Trim().Trim('\'');
            var valid = expectedType switch
            {
                "string" => propertyType == typeof(string) || propertyType == typeof(Guid) || propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTime),
                "integer" => propertyType == typeof(int) || propertyType == typeof(long),
                "number" => propertyType == typeof(decimal) || propertyType == typeof(double) || propertyType == typeof(float),
                "boolean" => propertyType == typeof(bool),
                "array" => typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType),
                _ => true,
            };
            valid.Should().BeTrue($"{operationId} serializes {jsonProperty.Name} as {propertyType.Name}, which must match OpenAPI {expectedType}");
        }
    }

    private static void AssertJsonElementMatchesSchema(JsonElement value, string schemaName, Contract contract, string context)
    {
        var schema = contract.Schemas[schemaName];
        value.ValueKind.Should().Be(JsonValueKind.Object, $"{context} must serialize the {schemaName} object");
        foreach (var requiredProperty in schema.RequiredProperties)
        {
            value.TryGetProperty(requiredProperty, out var property).Should().BeTrue($"{context} must include required {schemaName}.{requiredProperty}");
            var expected = schema.Properties[requiredProperty];
            if (!string.IsNullOrEmpty(expected.Reference)) AssertJsonElementMatchesSchema(property, expected.Reference, contract, context);
            else if (expected.Type == "string") property.ValueKind.Should().Be(JsonValueKind.String, $"{context} {requiredProperty} must be a string");
            else if (expected.Type == "integer") property.ValueKind.Should().Be(JsonValueKind.Number, $"{context} {requiredProperty} must be an integer");
        }
    }

    private static int FindLine(IReadOnlyList<string> lines, string expected) => Enumerable.Range(0, lines.Count)
        .FirstOrDefault(index => string.Equals(lines[index], expected, StringComparison.Ordinal));

    private static string Normalize(string? route) => (route ?? string.Empty)
        .Replace(":guid", string.Empty, StringComparison.Ordinal)
        .TrimEnd('/');

    private static string BuildConcretePath(string path) => "/api/v1" + Regex.Replace(
        path,
        "\\{[^}]+\\}",
        Guid.NewGuid().ToString());
}
