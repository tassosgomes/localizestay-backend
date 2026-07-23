using System.Collections.ObjectModel;
using LocalizeStay.Modules.Inventory.Application.LegalPolicies;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class CommercialOffer
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal int Revision { get; private set; }
    internal string RevisionAuthor { get; private set; } = string.Empty;
    internal OfferState State { get; private set; }
    internal OfferValidation? CurrentValidation { get; private set; }

    internal bool EverSubmitted => _submissions.Count > 0;
    internal IReadOnlyList<OfferSubmission> Submissions => _submissions.AsReadOnly();
    internal IReadOnlyList<OfferReturn> Returns => _returns.AsReadOnly();
    internal IReadOnlyList<CommercialPolicy> Policies => _policies.AsReadOnly();
    internal IReadOnlyList<Accommodation> Accommodations => _accommodations.AsReadOnly();
    internal IReadOnlyList<CommercialRate> Rates => _rates.AsReadOnly();

    internal int AccommodationCount { get; private set; }
    internal int BlockingIssueCount { get; private set; }

    internal DateTimeOffset? CompleteInformationReceivedAt { get; private set; }
    internal DateTimeOffset? TargetSubmissionAt { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<OfferSubmission> _submissions = [];
    private readonly List<OfferReturn> _returns = [];
    private readonly List<CommercialPolicy> _policies = [];
    private readonly List<Accommodation> _accommodations = [];
    private readonly List<CommercialRate> _rates = [];
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

        CurrentValidation = validation;
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

        if (CurrentValidation is null || CurrentValidation.Status != ValidationStatus.Valid)
        {
            throw new BusinessRuleViolationException(
                "A valid validation is required before submission.",
                "VALIDATION_REQUIRED");
        }

        if (CurrentValidation.Revision != Revision)
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

    internal CommercialPolicy AddPolicy(
        Guid policyId,
        CommercialPolicyRuleSet ruleSet,
        bool isDefault,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        CommercialPolicy created = null!;

        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            if (_policies.Any(p => p.Type == ruleSet.Type && p.Status == PolicyStatus.Active))
            {
                throw new BusinessRuleViolationException(
                    $"A policy of type '{ruleSet.Type}' is already active for this property.",
                    "POLICY_TYPE_ALREADY_ACTIVE");
            }

            created = CommercialPolicy.Create(policyId, PropertyId, ruleSet, isDefault, now);
            _policies.Add(created);
        });

        return created;
    }

    internal void SetDefaultPolicy(
        Guid policyId,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var policy = _policies.SingleOrDefault(p => p.Id == policyId)
                ?? throw new NotFoundException("Commercial policy was not found.", "POLICY_NOT_FOUND");

            if (policy.Status != PolicyStatus.Active)
                throw new BusinessRuleViolationException(
                    "Only active policies can be set as default.",
                    "POLICY_NOT_ACTIVE");

            if (policy.IsDefault)
                return;

            foreach (var p in _policies.Where(p => p.Id != policyId && p.IsDefault))
            {
                p.UnsetDefault();
            }

            policy.SetDefault();
        });
    }

    internal void DeactivatePolicy(
        Guid policyId,
        Guid replacementPolicyId,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var policy = _policies.SingleOrDefault(p => p.Id == policyId)
                ?? throw new NotFoundException("Commercial policy was not found.", "POLICY_NOT_FOUND");

            var replacement = _policies.SingleOrDefault(
                p => p.Id == replacementPolicyId && p.Status == PolicyStatus.Active
                    && p.Id != policyId && p.PropertyId == PropertyId);

            if (replacement is null)
            {
                throw new BusinessRuleViolationException(
                    "A different active policy from this property is required as replacement.",
                    "REPLACEMENT_POLICY_REQUIRED");
            }

            policy.Deactivate();
        });
    }

    internal void DeletePolicy(
        Guid policyId,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var policy = _policies.SingleOrDefault(p => p.Id == policyId)
                ?? throw new NotFoundException("Commercial policy was not found.", "POLICY_NOT_FOUND");

            if (!policy.CanDelete())
            {
                throw new BusinessRuleViolationException(
                    "Policy cannot be deleted because it was submitted, is the default, or is still in use.",
                    "POLICY_DELETION_NOT_ALLOWED");
            }

            _policies.Remove(policy);
        });
    }

    internal CommercialPolicy? GetPolicy(Guid policyId) =>
        _policies.SingleOrDefault(p => p.Id == policyId);

    internal Accommodation? GetAccommodation(Guid accommodationId) =>
        _accommodations.SingleOrDefault(a => a.Id == accommodationId);

    internal Accommodation AddAccommodation(
        Guid accommodationId,
        string commercialName,
        Guid? defaultPolicyId,
        ChildAgeRange? propertyDefaultChildAgeRange,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        Accommodation created = null!;

        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            created = Accommodation.Create(
                accommodationId,
                PropertyId,
                commercialName,
                defaultPolicyId,
                propertyDefaultChildAgeRange,
                now);
            _accommodations.Add(created);
        });

        return created;
    }

    internal void UpdateAccommodation(
        Guid accommodationId,
        string author,
        int? expectedRevision,
        DateTimeOffset now,
        Action<Accommodation> apply)
    {
        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var accommodation = _accommodations.SingleOrDefault(a => a.Id == accommodationId)
                ?? throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

            apply(accommodation);
            accommodation.UpdateTimestamp(now);
        });
    }

    internal void DeleteAccommodation(
        Guid accommodationId,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var accommodation = _accommodations.SingleOrDefault(a => a.Id == accommodationId)
                ?? throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

            if (!accommodation.CanDelete())
            {
                throw new BusinessRuleViolationException(
                    "Accommodation cannot be deleted because it was already submitted.",
                    "ACCOMMODATION_DELETION_NOT_ALLOWED");
            }

            _accommodations.Remove(accommodation);
        });
    }

    internal ChildAgeRange? GetDefaultChildAgeRange()
    {
        return null;
    }

    internal CommercialRate? GetRate(Guid rateId) =>
        _rates.SingleOrDefault(r => r.Id == rateId);

    internal CommercialRate AddRate(
        Guid rateId,
        Guid accommodationId,
        string name,
        string conditionCode,
        long? basePriceCents,
        int? includedGuests,
        long? additionalAdultPriceCents,
        long? additionalChildPriceCents,
        DateOnly? validFrom,
        DateOnly? validTo,
        int? minimumNights,
        Guid? policyId,
        MealPlan? mealPlan,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        CommercialRate created = null!;

        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var accommodation = _accommodations.SingleOrDefault(a => a.Id == accommodationId)
                ?? throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

            created = CommercialRate.Create(
                rateId,
                accommodationId,
                PropertyId,
                name,
                conditionCode,
                basePriceCents,
                includedGuests,
                additionalAdultPriceCents,
                additionalChildPriceCents,
                validFrom,
                validTo,
                minimumNights,
                policyId,
                mealPlan,
                now);

            _rates.Add(created);
        });

        return created;
    }

    internal void UpdateRate(
        Guid rateId,
        string? name,
        bool hasName,
        string? conditionCode,
        bool hasConditionCode,
        long? basePriceCents,
        bool hasBasePriceCents,
        int? includedGuests,
        bool hasIncludedGuests,
        long? additionalAdultPriceCents,
        bool hasAdditionalAdultPriceCents,
        long? additionalChildPriceCents,
        bool hasAdditionalChildPriceCents,
        DateOnly? validFrom,
        bool hasValidFrom,
        DateOnly? validTo,
        bool hasValidTo,
        int? minimumNights,
        bool hasMinimumNights,
        Guid? policyId,
        bool hasPolicyId,
        string? mealPlan,
        bool hasMealPlan,
        string? deactivationReason,
        bool hasDeactivationReason,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var rate = _rates.SingleOrDefault(r => r.Id == rateId)
                ?? throw new NotFoundException("Commercial rate was not found.", "RATE_NOT_FOUND");

            rate.Update(
                name,
                hasName,
                conditionCode,
                hasConditionCode,
                basePriceCents,
                hasBasePriceCents,
                includedGuests,
                hasIncludedGuests,
                additionalAdultPriceCents,
                hasAdditionalAdultPriceCents,
                additionalChildPriceCents,
                hasAdditionalChildPriceCents,
                validFrom,
                hasValidFrom,
                validTo,
                hasValidTo,
                minimumNights,
                hasMinimumNights,
                policyId,
                hasPolicyId,
                mealPlan,
                hasMealPlan,
                deactivationReason,
                hasDeactivationReason,
                now);
        });
    }

    internal void DeleteRate(
        Guid rateId,
        string author,
        int? expectedRevision,
        DateTimeOffset now)
    {
        IncrementRevisionMutate(author, now, expectedRevision, () =>
        {
            var rate = _rates.SingleOrDefault(r => r.Id == rateId)
                ?? throw new NotFoundException("Commercial rate was not found.", "RATE_NOT_FOUND");

            if (!rate.CanDelete())
            {
                throw new BusinessRuleViolationException(
                    "Rate cannot be deleted because it was already submitted. Deactivate it instead.",
                    "RATE_DELETION_NOT_ALLOWED");
            }

            _rates.Remove(rate);
        });
    }

    internal IReadOnlyList<CommercialRate> GetOverlappingRates(CommercialRate candidate)
    {
        if (candidate.Status != RateStatus.Active)
            return new ReadOnlyCollection<CommercialRate>([]);

        return _rates
            .Where(r => r.Id != candidate.Id && r.OverlapsWith(candidate))
            .ToList()
            .AsReadOnly();
    }
    internal void RecalculateCompletenessFromAccommodations(DateTimeOffset now)
    {
        var accommodationCount = _accommodations.Count(a => a.Status == AccommodationStatus.Active);
        var completeAccommodationCount = _accommodations.Count(
            a => a.Status == AccommodationStatus.Active && a.IsCommerciallyComplete());

        var activeRateCount = _rates.Count(r => r.Status == RateStatus.Active && r.IsComplete());
        var hasAnyRateOverlap = _rates
            .Where(r => r.Status == RateStatus.Active)
            .Any(r => GetOverlappingRates(r).Count > 0);

        RecalculateCompleteness(
            accommodationCount,
            completeAccommodationCount,
            activeRateCount,
            hasAnyRateOverlap,
            now);
    }

    private void InvalidateValidationOnMutate()
    {
        CurrentValidation?.Invalidate(UpdatedAt);
    }
}
