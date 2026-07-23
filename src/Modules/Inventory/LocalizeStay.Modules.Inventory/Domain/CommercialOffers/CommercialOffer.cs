using System.Collections.ObjectModel;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class CommercialOffer
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal int Revision { get; private set; }
    internal string RevisionAuthor { get; private set; } = string.Empty;
    internal OfferState State { get; private set; }
    internal OfferValidation? CurrentValidation => _validation;

    internal bool EverSubmitted => _submissions.Count > 0;
    internal IReadOnlyList<OfferSubmission> Submissions => _submissions.AsReadOnly();
    internal IReadOnlyList<OfferReturn> Returns => _returns.AsReadOnly();

    internal int AccommodationCount { get; private set; }
    internal int BlockingIssueCount { get; private set; }

    internal DateTimeOffset? CompleteInformationReceivedAt { get; private set; }
    internal DateTimeOffset? TargetSubmissionAt { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    private OfferValidation? _validation;
    private readonly List<OfferSubmission> _submissions = [];
    private readonly List<OfferReturn> _returns = [];
    private readonly List<PendingIssueType> _pendingIssues = [];

    private CommercialOffer()
    {
    }

    internal static CommercialOffer Create(
        Guid propertyId,
        string author,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(author);

        var utcNow = now.ToUniversalTime();

        var offer = new CommercialOffer
        {
            Id = propertyId,
            PropertyId = propertyId,
            Revision = 1,
            RevisionAuthor = author.Trim(),
            State = OfferState.Draft,
            AccommodationCount = 0,
            BlockingIssueCount = 3,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        offer._pendingIssues.AddRange(
        [
            PendingIssueType.MissingPolicy,
            PendingIssueType.IncompleteAccommodation,
            PendingIssueType.MissingActiveRate,
        ]);

        return offer;
    }

    internal void Validate(
        Guid validationId,
        string validatedBy,
        int expectedRevision,
        DateTimeOffset validatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(validatedBy);

        if (string.Equals(RevisionAuthor, validatedBy, StringComparison.Ordinal))
        {
            throw new BusinessRuleViolationException(
                "The author of the current revision cannot validate their own offer.",
                "SELF_VALIDATION_NOT_ALLOWED");
        }

        if (Revision != expectedRevision)
        {
            throw new BusinessRuleViolationException(
                $"Expected revision {expectedRevision} but current revision is {Revision}.",
                "REVISION_MISMATCH");
        }

        if (State != OfferState.ReadyForValidation)
        {
            throw new BusinessRuleViolationException(
                "Offer is not ready for validation.",
                "OFFER_NOT_READY");
        }

        var validation = OfferValidation.Create(
            validationId,
            PropertyId,
            Revision,
            validatedBy,
            validatedAt);

        _validation = validation;
        State = OfferState.Validated;
        UpdatedAt = validatedAt.ToUniversalTime();
    }

    internal OfferSubmission Submit(
        Guid submissionId,
        string snapshotJson,
        string submittedBy,
        int expectedRevision,
        DateTimeOffset submittedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);

        if (Revision != expectedRevision)
        {
            throw new BusinessRuleViolationException(
                $"Expected revision {expectedRevision} but current revision is {Revision}.",
                "REVISION_MISMATCH");
        }

        if (_validation is null || _validation.Status != ValidationStatus.Valid)
        {
            throw new BusinessRuleViolationException(
                "A valid validation is required before submission.",
                "VALIDATION_REQUIRED");
        }

        if (_validation.Revision != Revision)
        {
            throw new BusinessRuleViolationException(
                "The validation was created for a different revision.",
                "VALIDATION_REQUIRED");
        }

        var submission = OfferSubmission.Create(
            submissionId,
            PropertyId,
            Revision,
            snapshotJson,
            submittedBy,
            submittedAt);

        _submissions.Add(submission);
        State = OfferState.Submitted;
        UpdatedAt = submittedAt.ToUniversalTime();

        return submission;
    }

    internal OfferReturn RecordReturn(
        Guid returnId,
        Guid submissionId,
        string reasonCode,
        string reason,
        string returnedBy,
        DateTimeOffset returnedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnedBy);

        if (State != OfferState.Submitted)
        {
            throw new BusinessRuleViolationException(
                "Only submitted offers can be returned.",
                "OFFER_NOT_SUBMITTED");
        }

        if (_submissions.All(s => s.Id != submissionId))
        {
            throw new BusinessRuleViolationException(
                $"Submission '{submissionId}' was not found for this offer.",
                "SUBMISSION_NOT_FOUND");
        }

        var offerReturn = OfferReturn.Create(
            returnId,
            PropertyId,
            submissionId,
            Revision,
            reasonCode,
            reason,
            returnedBy,
            returnedAt);

        _returns.Add(offerReturn);
        State = OfferState.Returned;
        UpdatedAt = returnedAt.ToUniversalTime();

        return offerReturn;
    }

    internal void SetTargetSubmissionAt(DateTimeOffset targetSubmissionAt)
    {
        TargetSubmissionAt = targetSubmissionAt.ToUniversalTime();
    }

    internal void MarkPublished(DateTimeOffset publishedAt)
    {
        if (State == OfferState.Published)
            return;

        State = OfferState.Published;
        UpdatedAt = publishedAt.ToUniversalTime();
    }

    internal void RecalculateCompleteness(
        int accommodationCount,
        int completeAccommodationCount,
        int activeRateCount,
        bool hasAnyRateOverlap,
        DateTimeOffset now)
    {
        var result = CommercialOfferCompleteness.Compute(
            accommodationCount,
            completeAccommodationCount,
            activeRateCount,
            hasAnyRateOverlap);

        AccommodationCount = result.AccommodationCount;
        BlockingIssueCount = result.BlockingIssueCount;

        _pendingIssues.Clear();

        if (!result.IsComplete)
        {
            _pendingIssues.AddRange(result.PendingIssues);
        }

        if (result.IsComplete && CompleteInformationReceivedAt is null)
        {
            CompleteInformationReceivedAt = now.ToUniversalTime();
        }

        if (result.IsComplete && State == OfferState.Draft)
        {
            State = OfferState.ReadyForValidation;
        }
        else if (!result.IsComplete && State == OfferState.ReadyForValidation)
        {
            State = OfferState.Draft;
        }
    }

    internal void IncrementRevisionMutate(
        string author,
        DateTimeOffset now,
        int? expectedRevision,
        Action apply)
    {
        ExpectNotPublished();

        if (expectedRevision.HasValue && Revision != expectedRevision.Value)
        {
            throw new BusinessRuleViolationException(
                $"Expected revision {expectedRevision.Value} but current revision is {Revision}.",
                "REVISION_MISMATCH");
        }

        apply();

        Revision++;
        RevisionAuthor = author.Trim();
        UpdatedAt = now.ToUniversalTime();

        InvalidateValidationOnMutate();

        if (State == OfferState.Returned)
        {
            State = OfferState.Draft;
        }
    }

    internal bool HasAnyBlockingIssue(PendingIssueType issueType) =>
        _pendingIssues.Contains(issueType);

    internal IReadOnlyList<PendingIssueType> GetPendingIssues() =>
        _pendingIssues.AsReadOnly();

    private void ExpectNotPublished()
    {
        if (State == OfferState.Published)
        {
            throw new BusinessRuleViolationException(
                "Published offers cannot be modified through F02. Changes require F04 governance.",
                "PUBLISHED_OFFER_CHANGE_REQUIRES_F04");
        }
    }

    private void InvalidateValidationOnMutate()
    {
        _validation?.Invalidate(UpdatedAt);
    }
}
