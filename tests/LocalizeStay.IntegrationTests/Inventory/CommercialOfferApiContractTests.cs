using System.Net;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Certifies the F02 (commercial offer) API-first boundary against
/// <c>.tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml</c>. The matrix is
/// derived from the YAML via <see cref="OpenApiContractDocument"/>: every operationId, HTTP method,
/// path, declared response status, success content type, request body content type, Location header
/// and 204-no-content contract is asserted against the exposed Minimal API metadata. Hard-coded
/// operation lists are intentionally avoided so the suite cannot diverge from the contract.
/// </summary>
public sealed class CommercialOfferApiContractTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private const int ExpectedF02OperationCount = 20;

    private static readonly string _contractPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../.tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml"));

    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferApiContractTests(LocalizeStayWebApplicationFactory factory) => _factory = factory;

    public static IEnumerable<object[]> DeclaredOperations()
    {
        var contract = OpenApiContractDocument.Load(_contractPath);
        return contract.Operations.Select(operation => new object[] { operation.OperationId });
    }

    [Fact]
    public void OpenApiContract_DeclaredOperations_ShouldExposeExactlyTwentyCommercialOfferOperations()
    {
        var contract = OpenApiContractDocument.Load(_contractPath);

        contract.Operations.Should().HaveCount(ExpectedF02OperationCount,
            "the F02 OpenAPI contract defines exactly 20 commercial-offer operations");
        contract.Operations.Select(operation => operation.OperationId).Should().OnlyHaveUniqueItems();
        contract.Operations.Select(operation => (operation.Method, operation.Path)).Should().OnlyHaveUniqueItems(
            "each operation maps to a distinct method/path pair");

        var supportedMethods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" };
        contract.Operations.Select(operation => operation.Method).Should().OnlyContain(method => supportedMethods.Contains(method),
            "the parser must support every HTTP verb declared by the F02 contract");

        // The F02 contract exercises every parser-supported verb, so the certification also proves
        // the parser is no longer limited to the F01 get/post/patch subset.
        contract.Operations.Select(operation => operation.Method).Should().Contain(supportedMethods);
    }

    [Fact]
    public void OpenApiContract_DeclaredOperations_ShouldMatchExposedCommercialOfferHttpMetadata()
    {
        var contract = OpenApiContractDocument.Load(_contractPath);
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        var exposedNames = endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        // Every response schema referenced by an operation must exist in the components/schemas block.
        contract.Operations
            .SelectMany(operation => operation.Responses.Values)
            .Where(response => !string.IsNullOrEmpty(response.SchemaName))
            .Select(response => response.SchemaName)
            .Should().OnlyContain(schema => contract.Schemas.ContainsKey(schema!),
                "every response schema referenced by an F02 operation must be declared by the YAML contract");

        foreach (var operation in contract.Operations)
        {
            exposedNames.Should().Contain(operation.OperationId,
                $"{operation.OperationId} must be exposed with its contract operationId");

            var endpoint = endpoints.SingleOrDefault(candidate =>
                candidate.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == operation.OperationId);

            endpoint.Should().NotBeNull($"{operation.OperationId} must be exposed with its contract operationId");
            endpoint!.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Should().ContainSingle()
                .Which.Should().Be(operation.Method);
            Normalize(endpoint.RoutePattern.RawText).Should().Be(Normalize("/api/v1" + operation.Path));

            var responseMetadata = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
            responseMetadata.Select(metadata => metadata.StatusCode).Should().Contain(operation.Responses.Keys,
                $"{operation.OperationId} must declare every contract response status");

            // 204 responses must remain body-less; success responses (2xx other than 204) must be JSON.
            foreach (var successResponse in operation.SuccessResponses)
            {
                if (successResponse.StatusCode == 204)
                {
                    successResponse.ContentTypes.Should().BeEmpty(
                        $"{operation.OperationId} 204 must not declare a response body per the contract");
                    var noContentMetadata = responseMetadata.FirstOrDefault(metadata => metadata.StatusCode == 204);
                    noContentMetadata.Should().NotBeNull($"{operation.OperationId} must declare the 204 response");
                }
                else
                {
                    successResponse.ContentTypes.Should().Contain("application/json",
                        $"{operation.OperationId} {successResponse.StatusCode} must declare application/json");
                }
            }

            // Every response content type declared by the contract must be exposed by the endpoint.
            responseMetadata.SelectMany(metadata => metadata.ContentTypes).Should()
                .Contain(operation.Responses.Values.SelectMany(response => response.ContentTypes),
                    $"{operation.OperationId} must expose every response content type declared by the contract");

            // Request body contract: when declared, the endpoint must accept the same content type.
            if (operation.RequestContentTypes.Count > 0)
            {
                var requestMetadata = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
                requestMetadata.Should().NotBeNull($"{operation.OperationId} accepts the request body declared in the contract");
                requestMetadata!.ContentTypes.Should().Contain(operation.RequestContentTypes);
                requestMetadata.RequestType.Should().NotBeNull();
            }

            // Location-header contract: operations that declare Location must return 201 with a typed body.
            if (operation.RequiresLocationHeader)
            {
                operation.SuccessResponses.Select(response => response.StatusCode).Should().Contain(201,
                    $"{operation.OperationId} declares a Location header and therefore creates a resource");
                responseMetadata.Should().Contain(metadata => metadata.StatusCode == 201 && metadata.Type != null,
                    $"{operation.OperationId} must return the created resource alongside its Location header");
            }
        }
    }

    [Fact]
    public async Task OpenApiContract_DeclaredOperations_ShouldRejectAnonymousHttpRequestsAsUnauthorized()
    {
        // The scenario matrix is derived from the OpenAPI YAML, never a copied endpoint list.
        var contract = OpenApiContractDocument.Load(_contractPath);
        using var client = _factory.CreateClient();

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
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json",
                $"{operation.OperationId} 401 must return RFC 9457 problem+json");
        }
    }

    [Theory]
    [MemberData(nameof(DeclaredOperations))]
    public void OpenApiContract_DeclaredOperation_ShouldDeclareEveryCommonProblemStatus(string operationId)
    {
        // Restful-api baseline: every protected operation must declare 400/401/403/422/429/500
        // (and 404 for resource-scoped paths), so the dashboard and clients can rely on the matrix.
        var contract = OpenApiContractDocument.Load(_contractPath);
        var operation = contract.Operations.Single(item => item.OperationId == operationId);

        var mandatoryStatuses = new[] { 400, 401, 403, 422, 429, 500 };
        operation.Responses.Keys.Should().Contain(mandatoryStatuses,
            $"{operation.OperationId} must declare every common Problem Details status required by the F02 contract");

        // 404 is mandatory for resource-scoped paths (those that take propertyId/accommodationId/...).
        if (operation.Path.Contains("{", StringComparison.Ordinal))
        {
            operation.Responses.Keys.Should().Contain(404,
                $"{operation.OperationId} is resource-scoped and must declare 404 PROPERTY_NOT_FOUND");
        }
    }

    private static string Normalize(string? route) => (route ?? string.Empty)
        .Replace(":guid", string.Empty, StringComparison.Ordinal)
        .TrimEnd('/');

    private static string BuildConcretePath(string path) => "/api/v1" + System.Text.RegularExpressions.Regex.Replace(
        path,
        "\\{[^}]+\\}",
        Guid.NewGuid().ToString());
}
