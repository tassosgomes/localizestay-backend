using System.Globalization;
using System.Text.RegularExpressions;

namespace LocalizeStay.IntegrationTests.Infrastructure;

/// <summary>
/// Reusable OpenAPI contract parser shared by every API-first certification suite (F01 partners,
/// F02 commercial offers, ...). It reads the YAML contract that lives next to the PRD without
/// pulling in a full OpenAPI library: the goal is to assert that the HTTP surface exposed by
/// Minimal API matches the YAML operation-by-operation. The parser is intentionally tolerant of
/// both multi-line and inline schema references so it can read contracts authored in either style.
/// </summary>
public static class OpenApiContractDocument
{
    private static readonly Regex _pathExpression = new("^  (?<path>/[^:]+):$", RegexOptions.Compiled);
    private static readonly Regex _methodExpression = new("^    (?<method>get|post|put|patch|delete):$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _operationExpression = new("^      operationId: (?<id>\\w+)$", RegexOptions.Compiled);
    private static readonly Regex _responseExpression = new("^        '(?<status>\\d{3})':", RegexOptions.Compiled);
    private static readonly Regex _contentTypeExpression = new("^\\s{8,}(?<contentType>[\\w.+-]+/[\\w.+-]+):$", RegexOptions.Compiled);
    private static readonly Regex _schemaExpression = new("^\\s+\\$ref: '#/components/schemas/(?<schema>[^']+)'$", RegexOptions.Compiled);
    private static readonly Regex _schemaReferenceExpression = new("\\$ref: '#/components/schemas/(?<schema>[^']+)'", RegexOptions.Compiled);
    private static readonly Regex _inlineSchemaReferenceExpression = new("schema:\\s*\\{\\s*\\$ref:\\s*'#/components/schemas/(?<schema>[^']+)'", RegexOptions.Compiled);
    private static readonly Regex _schemaDefinitionExpression = new("^    (?<schema>[A-Za-z][A-Za-z0-9]+):$", RegexOptions.Compiled);
    private static readonly Regex _responseReferenceExpression = new("\\$ref: '#/components/responses/(?<response>[^']+)'", RegexOptions.Compiled);
    private static readonly Regex _componentResponseExpression = new("^    (?<response>[A-Za-z][A-Za-z0-9]+):$", RegexOptions.Compiled);
    private static readonly Regex _headerExpression = new("^\\s+Location:\\s*$", RegexOptions.Compiled);
    private static readonly Regex _requiredExpression = new("^      required: \\[(?<properties>[^]]*)]$", RegexOptions.Compiled);
    private static readonly Regex _propertyExpression = new("^        (?<property>[A-Za-z][A-Za-z0-9]*):(?: (?<inline>.+))?$", RegexOptions.Compiled);
    private static readonly Regex _typeExpression = new("type: (?<type>\\[[^]]+]|[A-Za-z]+)", RegexOptions.Compiled);
    private static readonly Regex _itemsReferenceExpression = new("items: \\{ \\$ref: '#/components/schemas/(?<schema>[^']+)' \\}", RegexOptions.Compiled);

    /// <summary>Loads and parses the OpenAPI YAML contract at <paramref name="absolutePath"/>.</summary>
    public static Contract Load(string absolutePath) => Parse(File.ReadLines(absolutePath));

    /// <summary>Parses the supplied YAML lines into a <see cref="Contract"/> instance.</summary>
    public static Contract Parse(IEnumerable<string> lines)
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
                currentResponseStatus = int.Parse(responseMatch.Groups["status"].Value, CultureInfo.InvariantCulture);
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

            if (inHeaders && _headerExpression.IsMatch(line))
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
            var inlineSchemaMatch = _inlineSchemaReferenceExpression.Match(line);
            if (inResponses && currentResponseStatus is not null)
            {
                if (schemaMatch.Success) currentOperation.ResponseSchemas.Add(schemaMatch.Groups["schema"].Value);
                if (inlineSchemaMatch.Success) currentOperation.ResponseSchemas.Add(inlineSchemaMatch.Groups["schema"].Value);
            }
        }

        foreach (var builder in operationBuilders)
        {
            foreach (var status in builder.ResponseStatuses.Where(status => !builder.Responses.ContainsKey(status)))
            {
                builder.Responses[status] = new ContractResponse(status, builder.ResponseContentTypes, builder.ResponseSchemas.SingleOrDefault() ?? string.Empty, new HashSet<string>());
            }
        }

        return new Contract(operationBuilders
            .Where(operation => operation.OperationId is not null)
            .Select(operation => operation.Build())
            .ToList(), schemas, componentResponses);
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
                .Select(line => _headerExpression.IsMatch(line) ? "Location" : null).Where(item => item is not null).Cast<string>().ToHashSet();
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

    private static int FindLine(IReadOnlyList<string> lines, string expected) => Enumerable.Range(0, lines.Count)
        .FirstOrDefault(index => string.Equals(lines[index], expected, StringComparison.Ordinal));
}

public sealed record Contract(
    IReadOnlyList<ContractOperation> Operations,
    IReadOnlyDictionary<string, ContractSchema> Schemas,
    IReadOnlyDictionary<string, ContractResponse> ComponentResponses);

public sealed record ContractOperation(
    string OperationId,
    string Method,
    string Path,
    IReadOnlyDictionary<int, ContractResponse> Responses,
    IReadOnlySet<string> RequestContentTypes,
    bool RequiresLocationHeader)
{
    public IEnumerable<ContractResponse> SuccessResponses => Responses.Values.Where(response => response.StatusCode >= 200 && response.StatusCode < 300);
}

public sealed record ContractResponse(int StatusCode, IReadOnlySet<string> ContentTypes, string SchemaName, IReadOnlySet<string> Headers);

public sealed record ContractSchema(IReadOnlySet<string> RequiredProperties, IReadOnlyDictionary<string, ContractProperty> Properties);

public sealed record ContractProperty(string Type, string Reference, string ItemsReference);

internal sealed class ContractOperationBuilder(string method, string path)
{
    public string? OperationId { get; set; }
    public HashSet<int> ResponseStatuses { get; } = [];
    public HashSet<string> RequestContentTypes { get; } = [];
    public HashSet<string> ResponseContentTypes { get; } = [];
    public HashSet<string> ResponseSchemas { get; } = [];
    public Dictionary<int, ContractResponse> Responses { get; } = [];
    public bool RequiresLocationHeader { get; set; }
    public ContractOperation Build() => new(
        OperationId!,
        method,
        path,
        Responses.ToDictionary(item => item.Key, item => item.Value with { StatusCode = item.Key }),
        RequestContentTypes,
        RequiresLocationHeader);
}
