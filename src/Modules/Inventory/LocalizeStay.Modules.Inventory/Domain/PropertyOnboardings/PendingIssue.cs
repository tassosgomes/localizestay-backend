namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed class PendingIssue
{
    public Guid Id { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public PendingOwnerType OwnerType { get; private set; }
    public string? AssigneeId { get; private set; }
    public PendingIssueStatus Status { get; private set; }
    public ReadinessGateType? RelatedGateType { get; private set; }
    public DateTimeOffset? TargetAt { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public string OpenedBy { get; private set; } = string.Empty;
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PendingIssue()
    {
    }

    internal static PendingIssue Create(
        Guid id,
        string description,
        PendingOwnerType ownerType,
        string? assigneeId,
        ReadinessGateType? relatedGateType,
        DateTimeOffset? targetAt,
        DateTimeOffset openedAt,
        string openedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(openedBy);

        if (description.Length is < 3 or > 1000)
        {
            throw new ArgumentException("Description must be between 3 and 1000 characters.", nameof(description));
        }

        if (assigneeId is not null && assigneeId.Length > 120)
        {
            throw new ArgumentException("AssigneeId must be at most 120 characters.", nameof(assigneeId));
        }

        return new PendingIssue
        {
            Id = id,
            Description = description.Trim(),
            OwnerType = ownerType,
            AssigneeId = assigneeId?.Trim(),
            Status = PendingIssueStatus.Open,
            RelatedGateType = relatedGateType,
            TargetAt = targetAt?.ToUniversalTime(),
            OpenedAt = openedAt.ToUniversalTime(),
            OpenedBy = openedBy.Trim(),
            UpdatedAt = openedAt.ToUniversalTime(),
        };
    }

    internal static PendingIssue FromCurationReturn(
        CurationReturnIssue issue,
        DateTimeOffset returnedAt,
        string returnedBy)
    {
        return Create(
            Guid.NewGuid(),
            issue.Description,
            issue.OwnerType,
            null,
            issue.RelatedGateType,
            null,
            returnedAt,
            returnedBy);
    }

    internal void UpdateDetails(string description, PendingOwnerType ownerType, string? assigneeId, DateTimeOffset? targetAt, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (description.Length is < 3 or > 1000)
        {
            throw new ArgumentException("Description must be between 3 and 1000 characters.", nameof(description));
        }

        if (assigneeId is not null && assigneeId.Length > 120)
        {
            throw new ArgumentException("AssigneeId must be at most 120 characters.", nameof(assigneeId));
        }

        Description = description.Trim();
        OwnerType = ownerType;
        AssigneeId = assigneeId?.Trim();
        TargetAt = targetAt?.ToUniversalTime();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    internal void Resolve(string resolutionNote, DateTimeOffset resolvedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionNote);

        if (Status != PendingIssueStatus.Open)
        {
            throw new InvalidOperationException("Only open issues can be resolved.");
        }

        if (resolutionNote.Length > 1000)
        {
            throw new ArgumentException("ResolutionNote must be at most 1000 characters.", nameof(resolutionNote));
        }

        Status = PendingIssueStatus.Resolved;
        ResolutionNote = resolutionNote.Trim();
        ResolvedAt = resolvedAt.ToUniversalTime();
        UpdatedAt = resolvedAt.ToUniversalTime();
    }

    internal void Cancel(string resolutionNote, DateTimeOffset cancelledAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionNote);

        if (Status != PendingIssueStatus.Open)
        {
            throw new InvalidOperationException("Only open issues can be cancelled.");
        }

        if (resolutionNote.Length > 1000)
        {
            throw new ArgumentException("ResolutionNote must be at most 1000 characters.", nameof(resolutionNote));
        }

        Status = PendingIssueStatus.Cancelled;
        ResolutionNote = resolutionNote.Trim();
        ResolvedAt = cancelledAt.ToUniversalTime();
        UpdatedAt = cancelledAt.ToUniversalTime();
    }
}
