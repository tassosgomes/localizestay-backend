using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace LocalizeStay.SharedKernel.Auditing;

/// <summary>
/// Immutable, append-only audit record shared by every module that opts into business auditing
/// (ADR-003). Each owning <c>DbContext</c> materializes its own <c>audit_entries</c> table — the
/// Shared Kernel only describes the shape and protects against PII leakage.
/// </summary>
/// <remarks>
/// Captures only business-relevant identifiers plus a single-line summary; never stores raw legal
/// ids, contract references, message bodies or any field that would turn the audit trail into a
/// secondary data store for sensitive data (LGPD baseline). Metadata is restricted to a curated
/// whitelist of non-PII business keys.
/// </remarks>
public sealed partial class BusinessAuditEntry
{
    /// <summary>Approved metadata keys — non-PII business identifiers only. Keep curated; never accept free-form keys.</summary>
    private static readonly IReadOnlySet<string> _approvedMetadataKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "partnerId",
        "propertyOnboardingId",
        "onboardingId",
        "destinationId",
        "gateType",
        "permission",
        "idempotencyKey",
        "cycleId",
        "reviewId",
        "curationReturnId",
        "communicationChannel",
        "reasonCode",
    };

    private const int MaxSummaryLength = 500;
    private const int MaxMetadataEntries = 16;
    private const int MaxMetadataKeyLength = 64;
    private const int MaxMetadataValueLength = 200;

    public Guid Id { get; private set; }

    /// <summary>Business aggregate type that produced the audited change (e.g. <c>PropertyOnboarding</c>).</summary>
    public string AggregateType { get; private set; } = string.Empty;

    /// <summary>Stable identifier of the aggregate instance; opaque to the Shared Kernel.</summary>
    public string AggregateId { get; private set; } = string.Empty;

    /// <summary>Identifier of the actor that performed the change (subject claim, internal job name, etc.).</summary>
    public string Actor { get; private set; } = string.Empty;

    /// <summary>High-level audit type (<c>PropertySubmitted</c>, <c>ReadinessGateCompleted</c>, ...).</summary>
    public string AuditType { get; private set; } = string.Empty;

    /// <summary>Single-line, human-readable summary. No PII, no contract references, no full message bodies.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>Correlation id echoing <see cref="Correlation.ICorrelationIdAccessor"/> so the entry joins the request trace.</summary>
    public string CorrelationId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private set; }

    /// <summary>Approved, non-PII business metadata. Frozen snapshot — mutating the source dictionary after creation has no effect.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; private set; } = ReadOnlyDictionary<string, string>.Empty;

    private BusinessAuditEntry()
    {
        // EF Core requires a parameterless constructor; factory is the only way callers can build a valid entry.
    }

    /// <summary>Builds an immutable audit entry. Throws <see cref="ArgumentException"/> if any field is empty or metadata is not approved.</summary>
    public static BusinessAuditEntry Create(
        string aggregateType,
        string aggregateId,
        string actor,
        string auditType,
        string summary,
        DateTimeOffset occurredOnUtc,
        string correlationId,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditType);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (summary.Trim().Length > MaxSummaryLength)
        {
            throw new ArgumentException(
                $"Summary must be {MaxSummaryLength} characters or fewer.",
                nameof(summary));
        }

        ValidateMetadata(metadata);

        return new BusinessAuditEntry
        {
            Id = Guid.NewGuid(),
            AggregateType = aggregateType.Trim(),
            AggregateId = aggregateId.Trim(),
            Actor = actor.Trim(),
            AuditType = auditType.Trim(),
            Summary = summary.Trim(),
            CorrelationId = correlationId.Trim(),
            OccurredOnUtc = occurredOnUtc.ToUniversalTime(),
            Metadata = CopyMetadata(metadata),
        };
    }

    private static void ValidateMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return;
        }

        if (metadata.Count > MaxMetadataEntries)
        {
            throw new ArgumentException(
                $"Metadata must contain at most {MaxMetadataEntries} entries.",
                nameof(metadata));
        }

        foreach (var (key, value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Metadata keys must be non-empty.", nameof(metadata));
            }

            if (key.Length > MaxMetadataKeyLength)
            {
                throw new ArgumentException(
                    $"Metadata key '{key}' exceeds {MaxMetadataKeyLength} characters.",
                    nameof(metadata));
            }

            if (!_approvedMetadataKeys.Contains(key))
            {
                throw new ArgumentException(
                    $"Metadata key '{key}' is not in the approved business audit whitelist.",
                    nameof(metadata));
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    $"Metadata value for '{key}' must be non-empty.",
                    nameof(metadata));
            }

            if (value.Length > MaxMetadataValueLength)
            {
                throw new ArgumentException(
                    $"Metadata value for '{key}' exceeds {MaxMetadataValueLength} characters.",
                    nameof(metadata));
            }

            if (LooksLikePersonalData(value))
            {
                throw new ArgumentException(
                    $"Metadata value for '{key}' looks like personal or sensitive data and cannot be stored in audit metadata.",
                    nameof(metadata));
            }
        }
    }

    private static bool LooksLikePersonalData(string value) =>
        EmailExpression().IsMatch(value)
        || CpfExpression().IsMatch(value)
        || CnpjExpression().IsMatch(value);

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailExpression();

    [GeneratedRegex(@"\d{3}\.\d{3}\.\d{3}-\d{2}")]
    private static partial Regex CpfExpression();

    [GeneratedRegex(@"\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}")]
    private static partial Regex CnpjExpression();

    private static IReadOnlyDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        var copy = new Dictionary<string, string>(metadata.Count, StringComparer.Ordinal);
        foreach (var (key, value) in metadata)
        {
            copy[key] = value;
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
