using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LocalizeStay.Modules.Inventory.Application.Observability;

/// <summary>
/// Inventory instruments consumed by the platform collector. Alert when
/// <c>inventory.outbox.failures</c> occurs after the fifth retry and when
/// <c>inventory.communication.sla</c> has any <c>result=outside_sla</c> sample during the pilot.
/// Tags intentionally contain only bounded operational values.
/// </summary>
internal static class InventoryTelemetry
{
    internal const string SourceName = "LocalizeStay.Inventory.Lifecycle";
    internal static readonly ActivitySource ActivitySource = new(SourceName);
    private static readonly Meter _meter = new(SourceName);

    internal static readonly Counter<long> OnboardingsOpened = _meter.CreateCounter<long>("inventory.onboarding.opened", unit: "{onboarding}");
    internal static readonly Counter<long> Submitted = _meter.CreateCounter<long>("inventory.onboarding.submitted", unit: "{onboarding}");
    internal static readonly Counter<long> Returns = _meter.CreateCounter<long>("inventory.onboarding.returned", unit: "{onboarding}");
    internal static readonly Counter<long> Gates = _meter.CreateCounter<long>("inventory.onboarding.gates", unit: "{gate}");
    internal static readonly Counter<long> CommunicationSla = _meter.CreateCounter<long>("inventory.communication.sla", unit: "{communication}");
    internal static readonly Counter<long> OutboxFailures = _meter.CreateCounter<long>("inventory.outbox.failures", unit: "{failure}");
    internal static readonly Histogram<double> SubmissionDuration = _meter.CreateHistogram<double>("inventory.onboarding.submission.duration", unit: "s");
}
