using System.Collections.ObjectModel;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed class PropertyOnboarding
{
    internal Guid Id { get; private set; }
    internal Guid PartnerId { get; private set; }
    internal string PreselectionId { get; private set; } = string.Empty;
    internal Property Property { get; private set; } = null!;
    internal OnboardingLifecycleStatus LifecycleStatus { get; private set; }
    internal ReadinessStatus ReadinessStatus => IsReady ? ReadinessStatus.Ready : ReadinessStatus.Blocked;
    internal IReadOnlyList<ReadinessGate> ReadinessGates => _readinessGates.AsReadOnly();
    internal IReadOnlyList<PendingIssue> PendingIssues => _pendingIssues.AsReadOnly();
    internal IReadOnlyList<CommunicationRecord> CommunicationRecords => _communicationRecords.AsReadOnly();
    internal IReadOnlyList<DuplicateReview> DuplicateReviews => _duplicateReviews.AsReadOnly();
    internal IReadOnlyList<CurationReturn> CurationReturns => _curationReturns.AsReadOnly();
    internal bool DuplicateReviewRequiresDecision { get; private set; }
    internal DateTimeOffset OpenedAt { get; private set; }
    internal DateTimeOffset TargetSubmissionAt { get; private set; }
    internal DateTimeOffset? SubmittedAt { get; private set; }
    internal DateTimeOffset? ClosedAt { get; private set; }
    internal CloseReasonCode? ReasonCode { get; private set; }
    internal string? CloseReason { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Normalized property similarity key persisted so the database can enforce a partial unique
    /// index preventing more than one active onboarding cycle for the same property.
    /// </summary>
    internal string PropertySimilarityKey { get; private set; } = string.Empty;

    private readonly List<ReadinessGate> _readinessGates = [];
    private readonly List<PendingIssue> _pendingIssues = [];
    private readonly List<CommunicationRecord> _communicationRecords = [];
    private readonly List<DuplicateReview> _duplicateReviews = [];
    private readonly List<CurationReturn> _curationReturns = [];
    private readonly IdempotencyTracker _idempotencyTracker = new();
    private PropertyOnboarding()
    {
    }
    internal bool IsReady =>
        _readinessGates.Count == 6
        && _readinessGates.All(gate => gate.Status == ReadinessGateStatus.Validated)
        && !_pendingIssues.Any(issue => issue.Status == PendingIssueStatus.Open)
        && !DuplicateReviewRequiresDecision;
    internal static PropertyOnboarding Create(
        Guid id,
        Guid partnerId,
        string preselectionId,
        Property property,
        DateTimeOffset openedAt,
        TimeSpan targetSubmissionOffset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preselectionId);
        ArgumentNullException.ThrowIfNull(property);
        if (preselectionId.Length > 100)
        {
            throw new ArgumentException("PreselectionId must be at most 100 characters.", nameof(preselectionId));
        }
        var openedUtc = openedAt.ToUniversalTime();
        var onboarding = new PropertyOnboarding
        {
            Id = id,
            PartnerId = partnerId,
            PreselectionId = preselectionId.Trim(),
            Property = property,
            PropertySimilarityKey = property.SimilarityKey,
            LifecycleStatus = OnboardingLifecycleStatus.InProgress,
            DuplicateReviewRequiresDecision = false,
            OpenedAt = openedUtc,
            TargetSubmissionAt = openedUtc.Add(targetSubmissionOffset),
            CreatedAt = openedUtc,
            UpdatedAt = openedUtc,
        };
        onboarding._readinessGates.AddRange(
            Enum.GetValues<ReadinessGateType>()
                .Select(type => ReadinessGate.Create(type, openedUtc)));
        return onboarding;
    }
    internal void UpdateProperty(string name, Address address, DateTimeOffset updatedAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);
        if (name.Length is < 2 or > 180)
        {
            throw new ArgumentException("Name must be between 2 and 180 characters.", nameof(name));
        }
        Property = new Property(name, Property.DestinationId, address);
        PropertySimilarityKey = Property.SimilarityKey;
        UpdatedAt = updatedAt.ToUniversalTime();
    }
    internal void ValidateGate(
        ReadinessGateType gateType,
        IReadOnlyList<EvidenceReference> evidence,
        string validatedBy,
        DateTimeOffset validatedAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        _readinessGates.FindGate(gateType).Validate(evidence, validatedBy, validatedAt);
        UpdatedAt = validatedAt.ToUniversalTime();
    }
    internal void RejectGate(ReadinessGateType gateType, string notes, DateTimeOffset updatedAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        _readinessGates.FindGate(gateType).Reject(notes, updatedAt);
        UpdatedAt = updatedAt.ToUniversalTime();
    }
    internal void ResetGateToPending(ReadinessGateType gateType, DateTimeOffset updatedAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        _readinessGates.FindGate(gateType).ResetToPending(updatedAt);
        UpdatedAt = updatedAt.ToUniversalTime();
    }
    internal PendingIssue AddPendingIssue(
        Guid id,
        string description,
        PendingOwnerType ownerType,
        string? assigneeId,
        ReadinessGateType? relatedGateType,
        DateTimeOffset? targetAt,
        DateTimeOffset openedAt,
        string openedBy)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        var issue = PendingIssue.Create(id, description, ownerType, assigneeId, relatedGateType, targetAt, openedAt, openedBy);
        _pendingIssues.Add(issue);
        UpdatedAt = openedAt.ToUniversalTime();
        return issue;
    }
    internal void UpdatePendingIssue(
        Guid issueId,
        string description,
        PendingOwnerType ownerType,
        string? assigneeId,
        DateTimeOffset? targetAt,
        DateTimeOffset updatedAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        _pendingIssues.FindPendingIssue(issueId).UpdateDetails(description, ownerType, assigneeId, targetAt, updatedAt);
        UpdatedAt = updatedAt.ToUniversalTime();
    }
    internal void ResolvePendingIssue(Guid issueId, string resolutionNote, DateTimeOffset resolvedAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        _pendingIssues.FindPendingIssue(issueId).Resolve(resolutionNote, resolvedAt);
        UpdatedAt = resolvedAt.ToUniversalTime();
    }
    internal void CancelPendingIssue(Guid issueId, string resolutionNote, DateTimeOffset cancelledAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        _pendingIssues.FindPendingIssue(issueId).Cancel(resolutionNote, cancelledAt);
        UpdatedAt = cancelledAt.ToUniversalTime();
    }
    internal CommunicationRecord RecordCommunication(
        Guid id,
        CommunicationChannel channel,
        DateTimeOffset receivedAt,
        DateTimeOffset processedAt,
        string resultSummary,
        TimeSpan sla,
        string createdBy,
        DateTimeOffset createdAt)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        var record = CommunicationRecord.Create(id, channel, receivedAt, processedAt, resultSummary, sla, createdBy, createdAt);
        _communicationRecords.Add(record);
        UpdatedAt = createdAt.ToUniversalTime();
        return record;
    }
    internal void FlagDuplicateReviewRequired(DateTimeOffset? updatedAt = null)
    {
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        DuplicateReviewRequiresDecision = true;
        UpdatedAt = (updatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
    internal DuplicateReview SubmitDuplicateReview(
        Guid reviewId,
        DuplicateReviewDecision decision,
        Guid? existingPropertyId,
        string justification,
        DateTimeOffset reviewedAt,
        string reviewedBy,
        Guid idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy);
        _idempotencyTracker.AssertAndRecord(idempotencyKey, IdempotencyScope.DuplicateReview);
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        if (!DuplicateReviewRequiresDecision)
        {
            throw new BusinessRuleViolationException(
                "No duplicate review is pending for this onboarding.",
                "DUPLICATE_REVIEW_NOT_PENDING");
        }
        var review = DuplicateReview.Create(reviewId, decision, existingPropertyId, justification, reviewedAt, reviewedBy);
        _duplicateReviews.Add(review);
        if (decision == DuplicateReviewDecision.DuplicateOfExistingProperty)
        {
            Close(CloseReasonCode.DuplicateProperty, $"Duplicate of property {existingPropertyId}.", reviewedAt, reviewedBy);
        }
        else
        {
            DuplicateReviewRequiresDecision = false;
        }
        UpdatedAt = reviewedAt.ToUniversalTime();
        return review;
    }
    internal void SubmitToCuration(
        Guid idempotencyKey,
        string decisionNote,
        DateTimeOffset submittedAt,
        string submittedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionNote);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        _idempotencyTracker.AssertAndRecord(idempotencyKey, IdempotencyScope.SubmitToCuration);
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureCanProgress(LifecycleStatus);
        if (!IsReady)
        {
            throw new BusinessRuleViolationException(
                "Onboarding is not ready for curation submission.",
                "ONBOARDING_NOT_READY");
        }
        SubmittedAt = submittedAt.ToUniversalTime();
        LifecycleStatus = OnboardingLifecycleStatus.SubmittedToCuration;
        UpdatedAt = submittedAt.ToUniversalTime();
    }
    internal CurationReturn RecordCurationReturn(
        Guid returnId,
        string? curationReference,
        CurationReturnReasonCode reasonCode,
        string reason,
        IReadOnlyList<CurationReturnIssue> issues,
        DateTimeOffset returnedAt,
        string returnedBy,
        Guid idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnedBy);
        ArgumentNullException.ThrowIfNull(issues);
        _idempotencyTracker.AssertAndRecord(idempotencyKey, IdempotencyScope.CurationReturn);
        OnboardingGuard.EnsureNotClosed(LifecycleStatus);
        OnboardingGuard.EnsureSubmittedToCuration(LifecycleStatus);
        var curationReturn = CurationReturn.Create(returnId, curationReference, reasonCode, reason, issues, returnedAt, returnedBy);
        _curationReturns.Add(curationReturn);
        LifecycleStatus = OnboardingLifecycleStatus.ReturnedByCuration;
        SubmittedAt = null;
        _pendingIssues.AddRange(issues.Select(issue => PendingIssue.FromCurationReturn(issue, returnedAt, returnedBy)));
        UpdatedAt = returnedAt.ToUniversalTime();
        return curationReturn;
    }
    internal void Close(CloseReasonCode reasonCode, string reason, DateTimeOffset closedAt, string closedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(closedBy);
        if (LifecycleStatus == OnboardingLifecycleStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                "Onboarding is already closed.",
                "ONBOARDING_ALREADY_CLOSED");
        }
        if (reason.Length is < 10 or > 1000)
        {
            throw new ArgumentException("Reason must be between 10 and 1000 characters.", nameof(reason));
        }
        LifecycleStatus = OnboardingLifecycleStatus.Closed;
        ReasonCode = reasonCode;
        CloseReason = reason.Trim();
        ClosedAt = closedAt.ToUniversalTime();
        UpdatedAt = closedAt.ToUniversalTime();
    }
    internal IReadOnlyList<BlockingReason> GetBlockingReasons() =>
        BlockingReasonBuilder.Build(LifecycleStatus, _readinessGates, _pendingIssues, DuplicateReviewRequiresDecision);
}
