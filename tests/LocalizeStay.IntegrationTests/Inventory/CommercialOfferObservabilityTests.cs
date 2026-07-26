using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Application.Observability;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Verifies that the F02 commercial-offer capability registers the full OpenTelemetry instrument
/// set declared in the techspec (task 11.0), keeps the liveness probe free of business dependencies
/// and never embeds sensitive content (full prices, snapshots, comments, legal text, tokens or PII)
/// in structured log templates emitted by the F02 handlers.
/// </summary>
public sealed class CommercialOfferObservabilityTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferObservabilityTests(LocalizeStayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void InventoryTelemetry_ShouldRegisterAllCommercialOfferMetricsExactlyOnce()
    {
        var expectedMetrics = new Dictionary<string, string>
        {
            [nameof(InventoryTelemetry.OfferCreated)] = "inventory.commercial_offer.created",
            [nameof(InventoryTelemetry.OfferMutation)] = "inventory.commercial_offer.mutation",
            [nameof(InventoryTelemetry.OfferValidation)] = "inventory.commercial_offer.validation",
            [nameof(InventoryTelemetry.OfferValidationInvalidated)] = "inventory.commercial_offer.validation_invalidated",
            [nameof(InventoryTelemetry.OfferSubmission)] = "inventory.commercial_offer.submission",
            [nameof(InventoryTelemetry.OfferReturned)] = "inventory.commercial_offer.returned",
            [nameof(InventoryTelemetry.OfferRateOverlap)] = "inventory.commercial_offer.rate_overlap",
            [nameof(InventoryTelemetry.OfferSubmissionDuration)] = "inventory.commercial_offer.submission_duration",
            [nameof(InventoryTelemetry.OfferOutboxFailure)] = "inventory.commercial_offer.outbox_failure",
        };

        var instrumentsByMetricName = typeof(InventoryTelemetry)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(IsCounterOrHistogram)
            .Select(field => new { Field = field.Name, Instrument = field.GetValue(null) })
            .Where(x => x.Instrument is not null)
            .GroupBy(x => (string)x.Instrument!.GetType().GetProperty("Name")!.GetValue(x.Instrument)!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Field).ToList(), StringComparer.Ordinal);

        // Each F02 metric name must map to exactly one registered instrument, and the canonical
        // names are part of the dashboard/alert contract — they cannot drift without coordinated
        // changes to dashboards and alerts.
        foreach (var (fieldName, expectedMetricName) in expectedMetrics)
        {
            instrumentsByMetricName.Should().ContainKey(expectedMetricName,
                "the {0} instrument must be registered as metric {1}", fieldName, expectedMetricName);
            instrumentsByMetricName[expectedMetricName].Should().ContainSingle(fieldName,
                "metric {0} must be registered exactly once (no duplicate fields)", expectedMetricName);
        }
    }

    private static bool IsCounterOrHistogram(FieldInfo field)
    {
        if (field.Name == nameof(InventoryTelemetry.ActivitySource) || !field.FieldType.IsGenericType)
        {
            return false;
        }

        var genericName = field.FieldType.Name;
        return genericName.StartsWith("Counter`", StringComparison.Ordinal)
            || genericName.StartsWith("Histogram`", StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryTelemetry_ShouldExposeAllCommercialOfferSpanNames()
    {
        GetConstValue(typeof(InventoryTelemetry.Spans), nameof(InventoryTelemetry.Spans.Load))
            .Should().Be("inventory.commercial_offer.load");
        GetConstValue(typeof(InventoryTelemetry.Spans), nameof(InventoryTelemetry.Spans.Validate))
            .Should().Be("inventory.commercial_offer.validate");
        GetConstValue(typeof(InventoryTelemetry.Spans), nameof(InventoryTelemetry.Spans.Submit))
            .Should().Be("inventory.commercial_offer.submit");
        GetConstValue(typeof(InventoryTelemetry.Spans), nameof(InventoryTelemetry.Spans.Return))
            .Should().Be("inventory.commercial_offer.return");
        GetConstValue(typeof(InventoryTelemetry.Spans), nameof(InventoryTelemetry.Spans.Metrics))
            .Should().Be("inventory.commercial_offer.metrics");
    }

    [Fact]
    public void InventoryTelemetry_ShouldUseBoundedTagValuesForResults()
    {
        GetConstValue(typeof(InventoryTelemetry.Tags), nameof(InventoryTelemetry.Tags.ResultSuccess))
            .Should().Be("success");
        GetConstValue(typeof(InventoryTelemetry.Tags), nameof(InventoryTelemetry.Tags.ResultFailure))
            .Should().Be("failure");
    }

    private static string GetConstValue(Type declaringType, string fieldName)
    {
        var field = declaringType.GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        field.Should().NotBeNull("const {0}.{1} must exist", declaringType.Name, fieldName);
        return (string)field!.GetRawConstantValue()!;
    }

    [Fact]
    public async Task HealthLive_ShouldReturn200WithoutTouchingPostgreSql()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Healthy");
        body.Should().NotContainAny("inventory-database", "postgres", "connection", "table", "select",
            "the liveness probe must not depend on PostgreSQL or expose business/dependency data");
    }

    [Theory]
    [MemberData(nameof(CommercialOfferHandlerSources))]
    public async Task CommercialOfferHandlers_LogTemplates_ShouldNotContainSensitiveContent(string sourcePath)
    {
        var source = await File.ReadAllTextAsync(sourcePath);

        var logCalls = Regex.Matches(source,
            @"\.Log(Debug|Information|Warning|Error|Critical)\s*\(",
            RegexOptions.Compiled);

        if (logCalls.Count == 0)
        {
            return;
        }

        // Forbidden tokens inside the template/format-string argument of any Log call. Prices,
        // snapshots, comments, legal text, tokens and PII must never appear in log output even as
        // interpolated placeholders — they belong to the auditable business_audit_entries table only.
        var forbiddenInTemplates = new[]
        {
            "BasePriceCents",
            "AdditionalAdultPriceCents",
            "AdditionalChildPriceCents",
            "SnapshotJson",
            "snapshotJson",
            "Comment",
            "comment",
            "RulesSummary",
            "RuleSetVersion",
            "ruleSet",
            "Reason",
            "reason",
            "Token",
            "token",
            "Password",
            "password",
        };

        foreach (Match call in logCalls)
        {
            var template = ExtractTemplateArgument(source, call.Index);
            foreach (var forbidden in forbiddenInTemplates)
            {
                template.Should().NotContain(forbidden,
                    "log templates in {0} must not embed {1} (F02 sanitisation baseline)",
                    Path.GetFileName(sourcePath),
                    forbidden);
            }
        }
    }

    public static IEnumerable<object[]> CommercialOfferHandlerSources()
    {
        var repoRoot = ResolveRepositoryRoot();
        var handlersDir = Path.Combine(repoRoot,
            "src", "Modules", "Inventory", "LocalizeStay.Modules.Inventory", "Application", "CommercialOffers");

        if (!Directory.Exists(handlersDir))
        {
            throw new DirectoryNotFoundException(
                $"F02 handlers directory not found at {handlersDir}. The source-scanning test cannot run without it.");
        }

        return Directory.EnumerateFiles(handlersDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new object[] { path })
            .ToArray();
    }

    private static string ExtractTemplateArgument(string source, int callStartIndex)
    {
        // Capture a generous window after the Log( token; the template is the first argument.
        var tail = source.Substring(callStartIndex, Math.Min(400, source.Length - callStartIndex));
        var firstString = Regex.Match(tail, @"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);
        return firstString.Success ? firstString.Value : string.Empty;
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LocalizeStay.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not resolve repository root from test base directory; LocalizeStay.sln was not found walking up the tree.");
    }
}
