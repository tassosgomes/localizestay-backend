using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LocalizeStay.Modules.Inventory.Application.Observability;

/// <summary>
/// Inventory instruments consumed by the platform collector. Alert when
/// <c>inventory.outbox.failures</c> occurs after the fifth retry and when
/// <c>inventory.communication.sla</c> has any <c>result=outside_sla</c> sample during the pilot.
/// Tags intentionally contain only bounded operational values (<c>operation</c>, <c>result</c>,
/// <c>status</c>). Identifiers (propertyId, offerRevision, submissionId, validationId, eventId,
/// correlationId) travel in span tags and log scopes only — never as metric labels — to keep
/// cardinality bounded (observability baseline: low-cardinality metrics).
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

    internal static readonly Counter<long> OfferValidation = _meter.CreateCounter<long>("inventory.commercial_offer.validation", unit: "{validation}");
    internal static readonly Counter<long> OfferSubmission = _meter.CreateCounter<long>("inventory.commercial_offer.submission", unit: "{submission}");
    internal static readonly Counter<long> OfferOutboxFailure = _meter.CreateCounter<long>("inventory.commercial_offer.outbox_failure", unit: "{failure}");
    internal static readonly Histogram<double> OfferSubmissionDuration = _meter.CreateHistogram<double>("inventory.commercial_offer.submission_duration", unit: "s");
    internal static readonly Counter<long> OfferReturned = _meter.CreateCounter<long>("inventory.commercial_offer.returned", unit: "{return}");

    // F02 metrics (task 11.1) — bounded tag values only; identifiers belong to span tags/log scopes.
    internal static readonly Counter<long> OfferCreated = _meter.CreateCounter<long>("inventory.commercial_offer.created", unit: "{offer}");
    internal static readonly Counter<long> OfferMutation = _meter.CreateCounter<long>("inventory.commercial_offer.mutation", unit: "{mutation}");
    internal static readonly Counter<long> OfferValidationInvalidated = _meter.CreateCounter<long>("inventory.commercial_offer.validation_invalidated", unit: "{invalidation}");
    internal static readonly Counter<long> OfferRateOverlap = _meter.CreateCounter<long>("inventory.commercial_offer.rate_overlap", unit: "{overlap}");

    /// <summary>
    /// Canonical F02 span names. Each handler owns exactly one span so a trace always traverses
    /// load → validate → submit → return and the metrics query, matching the techspec list.
    /// </summary>
    internal static class Spans
    {
        internal const string Load = "inventory.commercial_offer.load";
        internal const string Validate = "inventory.commercial_offer.validate";
        internal const string Submit = "inventory.commercial_offer.submit";
        internal const string Return = "inventory.commercial_offer.return";
        internal const string Metrics = "inventory.commercial_offer.metrics";
    }

    /// <summary>
    /// Canonical OpenTelemetry tag and log-scope keys shared by every F02 handler. Log scopes use
    /// the same keys so logs, spans and the (optional) OTLP collector correlate a single request
    /// without leaking sensitive payloads (no prices, snapshots, comments, legal text, tokens or PII).
    /// </summary>
    internal static class Tags
    {
        internal const string PropertyId = "inventory.commercial_offer.property_id";
        internal const string OfferRevision = "inventory.commercial_offer.revision";
        internal const string Operation = "inventory.commercial_offer.operation";
        internal const string Result = "inventory.commercial_offer.result";
        internal const string ValidationId = "inventory.commercial_offer.validation_id";
        internal const string SubmissionId = "inventory.commercial_offer.submission_id";
        internal const string EventId = "inventory.event.id";
        internal const string CorrelationId = "inventory.correlation_id";

        // Bounded metric tag values (never raw identifiers).
        internal const string ResultSuccess = "success";
        internal const string ResultFailure = "failure";
    }
}
