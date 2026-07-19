namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed class ReadinessGate
{
    public Guid Id { get; private set; }
    public ReadinessGateType Type { get; private set; }
    public ReadinessGateStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public IReadOnlyList<EvidenceReference> Evidence => _evidence.AsReadOnly();
    public DateTimeOffset? ValidatedAt { get; private set; }
    public string? ValidatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<EvidenceReference> _evidence = [];

    private ReadinessGate()
    {
    }

    internal static ReadinessGate Create(ReadinessGateType type, DateTimeOffset now)
    {
        return new ReadinessGate
        {
            Id = Guid.NewGuid(),
            Type = type,
            Status = ReadinessGateStatus.Pending,
            UpdatedAt = now.ToUniversalTime(),
        };
    }

    internal void Validate(IReadOnlyList<EvidenceReference> evidence, string validatedBy, DateTimeOffset validatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(validatedBy);
        ArgumentNullException.ThrowIfNull(evidence);

        ValidateEvidenceForType(evidence);

        _evidence.Clear();
        _evidence.AddRange(evidence);
        Status = ReadinessGateStatus.Validated;
        ValidatedAt = validatedAt.ToUniversalTime();
        ValidatedBy = validatedBy.Trim();
        UpdatedAt = validatedAt.ToUniversalTime();
    }

    internal void Reject(string notes, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);

        Status = ReadinessGateStatus.Rejected;
        Notes = notes.Trim();
        ValidatedAt = null;
        ValidatedBy = null;
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    internal void ResetToPending(DateTimeOffset updatedAt)
    {
        Status = ReadinessGateStatus.Pending;
        Notes = null;
        ValidatedAt = null;
        ValidatedBy = null;
        _evidence.Clear();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    private void ValidateEvidenceForType(IReadOnlyList<EvidenceReference> evidence)
    {
        if (evidence.Count == 0)
        {
            throw new ArgumentException($"Gate '{Type}' requires at least one evidence reference.", nameof(evidence));
        }

        var requiredKind = Type switch
        {
            ReadinessGateType.SignedContract => EvidenceKind.Contract,
            ReadinessGateType.AuthorizedContact => EvidenceKind.FormalAuthorization,
            ReadinessGateType.OperationalChannel => EvidenceKind.Communication,
            _ => (EvidenceKind?)null,
        };

        if (requiredKind.HasValue && !evidence.Any(e => e.Kind == requiredKind.Value))
        {
            throw new ArgumentException(
                $"Gate '{Type}' requires at least one evidence of kind '{requiredKind.Value}'.",
                nameof(evidence));
        }
    }
}

public enum EvidenceKind
{
    OfficialDocument,
    Contract,
    FormalAuthorization,
    Communication,
    Other,
}

public sealed record EvidenceReference
{
    public EvidenceKind Kind { get; }
    public string Reference { get; }
    public string Description { get; }

    public EvidenceReference(EvidenceKind kind, string reference, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (reference.Length > 500)
        {
            throw new ArgumentException("Reference must be at most 500 characters.", nameof(reference));
        }

        if (description.Length > 300)
        {
            throw new ArgumentException("Description must be at most 300 characters.", nameof(description));
        }

        Kind = kind;
        Reference = reference.Trim();
        Description = description.Trim();
    }
}
