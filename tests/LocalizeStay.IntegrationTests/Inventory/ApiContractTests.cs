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
/// Guards the API-first boundary. The OpenAPI YAML remains the sole source of truth: this test
/// extracts every operation and verifies its identity, HTTP surface and declared payload shapes.
/// </summary>
public sealed class ApiContractTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private static readonly Regex _pathExpression = new("^  (?<path>/[^:]+):$", RegexOptions.Compiled);
    private static readonly Regex _methodExpression = new("^    (?<method>get|post|patch):$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _operationExpression = new("^      operationId: (?<id>\\w+)$", RegexOptions.Compiled);
    private static readonly Regex _responseExpression = new("^        '(?<status>\\d{3})':", RegexOptions.Compiled);
    private static readonly Regex _contentTypeExpression = new("^\\s{8,}(?<contentType>[\\w.+-]+/[\\w.+-]+):$", RegexOptions.Compiled);
    private static readonly Regex _schemaExpression = new("^\\s+\\$ref: '#/components/schemas/(?<schema>[^']+)'$", RegexOptions.Compiled);
    private static readonly Regex _schemaReferenceExpression = new("\\$ref: '#/components/schemas/(?<schema>[^']+)'", RegexOptions.Compiled);
    private static readonly Regex _schemaDefinitionExpression = new("^    (?<schema>[A-Za-z][A-Za-z0-9]+):$", RegexOptions.Compiled);
    private static readonly Regex _responseReferenceExpression = new("\\$ref: '#/components/responses/(?<response>[^']+)'", RegexOptions.Compiled);
    private static readonly Regex _componentResponseExpression = new("^    (?<response>[A-Za-z][A-Za-z0-9]+):$", RegexOptions.Compiled);
    private static readonly Regex _headerExpression = new("^      (?<header>[A-Za-z][A-Za-z-]+):$", RegexOptions.Compiled);
    private static readonly Regex _requiredExpression = new("^      required: \\[(?<properties>[^]]*)]$", RegexOptions.Compiled);
    private static readonly Regex _propertyExpression = new("^        (?<property>[A-Za-z][A-Za-z0-9]*):(?: (?<inline>.+))?$", RegexOptions.Compiled);
    private static readonly Regex _typeExpression = new("type: (?<type>\\[[^]]+]|[A-Za-z]+)", RegexOptions.Compiled);
    private static readonly Regex _itemsReferenceExpression = new("items: \\{ \\$ref: '#/components/schemas/(?<schema>[^']+)' \\}", RegexOptions.Compiled);
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

    private static Contract ReadContract(IEnumerable<string> lines)
    {
        var source = lines.ToList();
        var operationBuilders = new List<ContractOperationBuilder>();
        var schemas = ReadSchemas(source);
        var componentResponses = ReadComponentResponses(source);
        ContractOperationBuilder? currentOperation = null;
        string? currentPath = null;
        int? currentResponseStatus = null;
        var inComponentsSchemas = false;
        var inRequestBody = false;
        var inResponses = false;
        var inHeaders = false;

        foreach (var line in source)
        {
            if (line == "components:")
            {
                inComponentsSchemas = false;
                currentOperation = null;
                continue;
            }

            if (line == "  schemas:") { inComponentsSchemas = true; continue; }
            if (inComponentsSchemas) continue;

            var pathMatch = _pathExpression.Match(line);
            if (pathMatch.Success)
            {
                currentPath = pathMatch.Groups["path"].Value;
                currentOperation = null;
                continue;
            }

            var methodMatch = _methodExpression.Match(line);
            if (methodMatch.Success && currentPath is not null)
            {
                currentOperation = new ContractOperationBuilder(methodMatch.Groups["method"].Value.ToUpperInvariant(), currentPath);
                operationBuilders.Add(currentOperation);
                currentResponseStatus = null;
                inRequestBody = false;
                inResponses = false;
                inHeaders = false;
                continue;
            }

            if (currentOperation is null) continue;
            var operationMatch = _operationExpression.Match(line);
            if (operationMatch.Success)
            {
                currentOperation.OperationId = operationMatch.Groups["id"].Value;
                continue;
            }

            if (line == "      requestBody:")
            {
                inRequestBody = true;
                inResponses = false;
                continue;
            }

            if (line == "      responses:")
            {
                inRequestBody = false;
                inResponses = true;
                continue;
            }

            var responseMatch = _responseExpression.Match(line);
            if (inResponses && responseMatch.Success)
            {
                currentResponseStatus = int.Parse(responseMatch.Groups["status"].Value, System.Globalization.CultureInfo.InvariantCulture);
                currentOperation.ResponseStatuses.Add(currentResponseStatus.Value);
                var inlineResponse = _responseReferenceExpression.Match(line);
                if (inlineResponse.Success)
                {
                    currentOperation.Responses[currentResponseStatus.Value] = componentResponses[inlineResponse.Groups["response"].Value];
                }
                inHeaders = false;
                continue;
            }

            var responseReference = _responseReferenceExpression.Match(line);
            if (inResponses && currentResponseStatus is not null && responseReference.Success)
            {
                currentOperation.Responses[currentResponseStatus.Value] = componentResponses[responseReference.Groups["response"].Value];
                continue;
            }

            if (inResponses && line == "          headers:")
            {
                inHeaders = true;
                continue;
            }

            if (inHeaders && line == "            Location:")
            {
                currentOperation.RequiresLocationHeader = true;
                continue;
            }

            var contentTypeMatch = _contentTypeExpression.Match(line);
            if (contentTypeMatch.Success)
            {
                if (inRequestBody) currentOperation.RequestContentTypes.Add(contentTypeMatch.Groups["contentType"].Value);
                if (inResponses && currentResponseStatus is not null) currentOperation.ResponseContentTypes.Add(contentTypeMatch.Groups["contentType"].Value);
                continue;
            }

            var schemaMatch = _schemaExpression.Match(line);
            if (schemaMatch.Success && inResponses && currentResponseStatus is not null)
            {
                currentOperation.ResponseSchemas.Add(schemaMatch.Groups["schema"].Value);
            }
        }

        foreach (var builder in operationBuilders)
        {
            foreach (var status in builder.ResponseStatuses.Where(status => !builder.Responses.ContainsKey(status)))
            {
                builder.Responses[status] = new ContractResponse(status, builder.ResponseContentTypes, builder.ResponseSchemas.Single(), new HashSet<string>());
            }
        }

        return new Contract(operationBuilders
            .Where(operation => operation.OperationId is not null)
            .Select(operation => operation.Build())
            .ToList(), schemas);
    }

    private static IReadOnlyDictionary<string, ContractResponse> ReadComponentResponses(IReadOnlyList<string> lines)
    {
        var responses = new Dictionary<string, ContractResponse>(StringComparer.Ordinal);
        var start = FindLine(lines, "  responses:");
        var end = FindLine(lines, "  schemas:");
        for (var index = start + 1; start >= 0 && index < end; index++)
        {
            var match = _componentResponseExpression.Match(lines[index]);
            if (!match.Success) continue;
            var next = index + 1;
            while (next < end && !_componentResponseExpression.IsMatch(lines[next])) next++;
            var block = lines.Skip(index).Take(next - index).ToList();
            var contentTypes = block.Select(line => _contentTypeExpression.Match(line)).Where(item => item.Success).Select(item => item.Groups["contentType"].Value).ToHashSet();
            var schema = block.Select(line => _schemaReferenceExpression.Match(line)).First(item => item.Success).Groups["schema"].Value;
            var headers = block.SkipWhile(line => line != "      headers:").Skip(1).TakeWhile(line => line != "      content:")
                .Select(line => _headerExpression.Match(line)).Where(item => item.Success).Select(item => item.Groups["header"].Value).ToHashSet();
            responses[match.Groups["response"].Value] = new ContractResponse(0, contentTypes, schema, headers);
            index = next - 1;
        }
        return responses;
    }

    private static IReadOnlyDictionary<string, ContractSchema> ReadSchemas(IReadOnlyList<string> lines)
    {
        var schemas = new Dictionary<string, ContractSchema>(StringComparer.Ordinal);
        var start = FindLine(lines, "  schemas:");
        for (var index = start + 1; start >= 0 && index < lines.Count; index++)
        {
            var match = _schemaDefinitionExpression.Match(lines[index]);
            if (!match.Success) continue;
            var next = index + 1;
            while (next < lines.Count && !_schemaDefinitionExpression.IsMatch(lines[next])) next++;
            var block = lines.Skip(index).Take(next - index).ToList();
            var required = block.Select(line => _requiredExpression.Match(line)).Where(item => item.Success)
                .SelectMany(item => item.Groups["properties"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.Ordinal);
            var properties = new Dictionary<string, ContractProperty>(StringComparer.Ordinal);
            var propertiesStart = block.FindIndex(line => line == "      properties:");
            if (propertiesStart >= 0)
            {
                for (var propertyIndex = propertiesStart + 1; propertyIndex < block.Count; propertyIndex++)
                {
                    var propertyMatch = _propertyExpression.Match(block[propertyIndex]);
                    if (!propertyMatch.Success) continue;
                    var propertyNext = propertyIndex + 1;
                    while (propertyNext < block.Count && !_propertyExpression.IsMatch(block[propertyNext])) propertyNext++;
                    var propertyBlock = string.Join(' ', block.Skip(propertyIndex).Take(propertyNext - propertyIndex));
                    var reference = _schemaExpression.Match(propertyBlock).Groups["schema"].Value;
                    var inlineReference = _schemaReferenceExpression.Match(propertyBlock).Groups["schema"].Value;
                    var type = _typeExpression.Match(propertyBlock).Groups["type"].Value;
                    var itemReference = _itemsReferenceExpression.Match(propertyBlock).Groups["schema"].Value;
                    properties[propertyMatch.Groups["property"].Value] = new ContractProperty(type, string.IsNullOrEmpty(reference) ? inlineReference : reference, itemReference);
                    propertyIndex = propertyNext - 1;
                }
            }
            schemas[match.Groups["schema"].Value] = new ContractSchema(required, properties);
            index = next - 1;
        }
        return schemas;
    }

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

    private sealed record Contract(IReadOnlyList<ContractOperation> Operations, IReadOnlyDictionary<string, ContractSchema> Schemas);

    private sealed record ContractOperation(string OperationId, string Method, string Path, IReadOnlyDictionary<int, ContractResponse> Responses, IReadOnlySet<string> RequestContentTypes, bool RequiresLocationHeader)
    {
        public IEnumerable<ContractResponse> SuccessResponses => Responses.Values.Where(response => response.StatusCode >= 200 && response.StatusCode < 300);
    }
    private sealed record ContractResponse(int StatusCode, IReadOnlySet<string> ContentTypes, string SchemaName, IReadOnlySet<string> Headers);
    private sealed record ContractSchema(IReadOnlySet<string> RequiredProperties, IReadOnlyDictionary<string, ContractProperty> Properties);
    private sealed record ContractProperty(string Type, string Reference, string ItemsReference);

    private sealed class ContractOperationBuilder(string method, string path)
    {
        public string? OperationId { get; set; }
        public HashSet<int> ResponseStatuses { get; } = [];
        public HashSet<string> RequestContentTypes { get; } = [];
        public HashSet<string> ResponseContentTypes { get; } = [];
        public HashSet<string> ResponseSchemas { get; } = [];
        public Dictionary<int, ContractResponse> Responses { get; } = [];
        public bool RequiresLocationHeader { get; set; }
        public ContractOperation Build() => new(OperationId!, method, path, Responses.ToDictionary(item => item.Key, item => item.Value with { StatusCode = item.Key }), RequestContentTypes, RequiresLocationHeader);
    }
}
