namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed class CurationReturn
{
    public Guid Id { get; private set; }
    public string? CurationReference { get; private set; }
    public CurationReturnReasonCode ReasonCode { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public IReadOnlyList<CurationReturnIssue> Issues => _issues.AsReadOnly();
    public DateTimeOffset ReturnedAt { get; private set; }
    public string ReturnedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<CurationReturnIssue> _issues = [];

    private CurationReturn()
    {
    }

    internal static CurationReturn Create(
        Guid id,
        string? curationReference,
        CurationReturnReasonCode reasonCode,
        string reason,
        IEnumerable<CurationReturnIssue> issues,
        DateTimeOffset returnedAt,
        string returnedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnedBy);
        ArgumentNullException.ThrowIfNull(issues);

        if (reason.Length is < 3 or > 1000)
        {
            throw new ArgumentException("Reason must be between 3 and 1000 characters.", nameof(reason));
        }

        if (curationReference is not null && curationReference.Length > 120)
        {
            throw new ArgumentException("CurationReference must be at most 120 characters.", nameof(curationReference));
        }

        var issueList = issues.ToList();
        if (issueList.Count == 0)
        {
            throw new ArgumentException("At least one issue is required.", nameof(issues));
        }

        var curationReturn = new CurationReturn
        {
            Id = id,
            CurationReference = curationReference?.Trim(),
            ReasonCode = reasonCode,
            Reason = reason.Trim(),
            ReturnedAt = returnedAt.ToUniversalTime(),
            ReturnedBy = returnedBy.Trim(),
            CreatedAt = returnedAt.ToUniversalTime(),
        };

        curationReturn._issues.AddRange(issueList);
        return curationReturn;
    }
}

public sealed record CurationReturnIssue
{
    public string Description { get; }
    public PendingOwnerType OwnerType { get; }
    public ReadinessGateType? RelatedGateType { get; }

    public CurationReturnIssue(string description, PendingOwnerType ownerType, ReadinessGateType? relatedGateType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (description.Length is < 3 or > 1000)
        {
            throw new ArgumentException("Description must be between 3 and 1000 characters.", nameof(description));
        }

        Description = description.Trim();
        OwnerType = ownerType;
        RelatedGateType = relatedGateType;
    }
}
