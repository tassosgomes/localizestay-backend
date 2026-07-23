using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class CommercialOfferTests
{
    private const string Author1 = "staff-alpha";
    private const string Author2 = "staff-beta";

    [Fact]
    public void Create_WithValidInputs_ShouldCreateDraftOffer()
    {
        var propertyId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var offer = CommercialOffer.Create(propertyId, Author1, now);

        offer.Id.Should().Be(propertyId);
        offer.PropertyId.Should().Be(propertyId);
        offer.Revision.Should().Be(1);
        offer.RevisionAuthor.Should().Be(Author1);
        offer.State.Should().Be(OfferState.Draft);
        offer.AccommodationCount.Should().Be(0);
        offer.BlockingIssueCount.Should().Be(3);
        offer.EverSubmitted.Should().BeFalse();
        offer.CompleteInformationReceivedAt.Should().BeNull();
        offer.CreatedAt.Should().Be(now);
        offer.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithBlankAuthor_ShouldThrow()
    {
        var act = () => CommercialOffer.Create(Guid.NewGuid(), "   ", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhiteSpaceAuthor_ShouldThrow(string author)
    {
        var act = () => CommercialOffer.Create(Guid.NewGuid(), author, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_IdempotentCreate_ShouldProduceSameShape()
    {
        var propertyId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var first = CommercialOffer.Create(propertyId, Author1, now);
        var second = CommercialOffer.Create(propertyId, Author1, now);

        first.Id.Should().Be(second.Id);
        first.PropertyId.Should().Be(second.PropertyId);
        first.Revision.Should().Be(second.Revision);
        first.State.Should().Be(second.State);
    }

    [Fact]
    public void InitialOffer_ShouldHaveExpectedPendingIssues()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        var issues = offer.GetPendingIssues();

        issues.Should().Contain(PendingIssueType.MissingPolicy);
        issues.Should().Contain(PendingIssueType.IncompleteAccommodation);
        issues.Should().Contain(PendingIssueType.MissingActiveRate);
    }

    [Fact]
    public void RecalculateCompleteness_WithNoAccommodations_ShouldStayIncomplete()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        offer.RecalculateCompleteness(0, 0, 0, false, DateTimeOffset.UtcNow);

        offer.State.Should().Be(OfferState.Draft);
        offer.BlockingIssueCount.Should().Be(3);
        offer.CompleteInformationReceivedAt.Should().BeNull();
    }

    [Fact]
    public void RecalculateCompleteness_WithCompleteAccommodationsAndRates_ShouldBecomeReady()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        offer.RecalculateCompleteness(2, 2, 2, false, now);

        offer.State.Should().Be(OfferState.ReadyForValidation);
        offer.AccommodationCount.Should().Be(2);
        offer.BlockingIssueCount.Should().Be(0);
        offer.CompleteInformationReceivedAt.Should().Be(now);
    }

    [Fact]
    public void RecalculateCompleteness_FirstTimeComplete_SetsCompleteInformationReceivedAt()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var firstComplete = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        offer.RecalculateCompleteness(1, 1, 1, false, firstComplete);

        offer.CompleteInformationReceivedAt.Should().Be(firstComplete);
    }

    [Fact]
    public void RecalculateCompleteness_TwiceComplete_DoesNotOverwriteFirstTimestamp()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var first = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
        var later = first.AddHours(5);

        offer.RecalculateCompleteness(1, 1, 1, false, first);
        offer.RecalculateCompleteness(2, 2, 2, false, later);

        offer.CompleteInformationReceivedAt.Should().Be(first);
    }

    [Fact]
    public void RecalculateCompleteness_WithRateOverlap_ShouldAddBlockingIssue()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        offer.RecalculateCompleteness(1, 1, 1, true, DateTimeOffset.UtcNow);

        offer.BlockingIssueCount.Should().Be(1);
        offer.State.Should().Be(OfferState.Draft);
        offer.HasAnyBlockingIssue(PendingIssueType.RatePeriodOverlap).Should().BeTrue();
    }

    [Fact]
    public void RecalculateCompleteness_LosingCompleteness_ShouldReturnToDraft()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.RecalculateCompleteness(1, 1, 1, false, DateTimeOffset.UtcNow);
        offer.State.Should().Be(OfferState.ReadyForValidation);

        offer.RecalculateCompleteness(1, 0, 0, false, DateTimeOffset.UtcNow);

        offer.State.Should().Be(OfferState.Draft);
    }

    [Fact]
    public void Validate_WithDifferentReviewer_ShouldTransitionToValidated()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        var validationId = Guid.NewGuid();

        offer.Validate(validationId, Author2, offer.Revision, now);

        offer.State.Should().Be(OfferState.Validated);
        offer.CurrentValidation.Should().NotBeNull();
        offer.CurrentValidation!.Id.Should().Be(validationId);
        offer.CurrentValidation.ValidatedBy.Should().Be(Author2);
        offer.CurrentValidation.Revision.Should().Be(offer.Revision);
        offer.CurrentValidation.Status.Should().Be(ValidationStatus.Valid);
    }

    [Fact]
    public void Validate_SameAsAuthor_ShouldThrowSelfValidation()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.RecalculateCompleteness(1, 1, 1, false, DateTimeOffset.UtcNow);

        var act = () => offer.Validate(Guid.NewGuid(), Author1, offer.Revision, DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "SELF_VALIDATION_NOT_ALLOWED");
    }

    [Fact]
    public void Validate_WithStaleRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.RecalculateCompleteness(1, 1, 1, false, DateTimeOffset.UtcNow);

        var act = () => offer.Validate(Guid.NewGuid(), Author2, offer.Revision + 1, DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void Validate_WhenNotReady_ShouldThrowNotReady()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        var act = () => offer.Validate(Guid.NewGuid(), Author2, offer.Revision, DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "OFFER_NOT_READY");
    }

    [Fact]
    public void Submit_WithValidValidation_ShouldTransitionToSubmitted()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        var validationId = Guid.NewGuid();
        offer.Validate(validationId, Author2, offer.Revision, now);
        var submissionId = Guid.NewGuid();
        var snapshot = "{\"version\":1,\"revision\":" + offer.Revision + "}";

        var submission = offer.Submit(submissionId, snapshot, Author1, offer.Revision, now);

        offer.State.Should().Be(OfferState.Submitted);
        offer.EverSubmitted.Should().BeTrue();
        submission.Id.Should().Be(submissionId);
        submission.SnapshotJson.Should().Be(snapshot);
    }

    [Fact]
    public void Submit_WithoutValidation_ShouldThrowValidationRequired()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.RecalculateCompleteness(1, 1, 1, false, DateTimeOffset.UtcNow);

        var act = () => offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "VALIDATION_REQUIRED");
    }

    [Fact]
    public void Submit_WithStaleRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);

        var act = () => offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision + 1, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void RecordReturn_OfSubmittedOffer_ShouldTransitionToReturned()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);
        var returnId = Guid.NewGuid();

        var offerReturn = offer.RecordReturn(
            returnId,
            submission.Id,
            "incomplete_data",
            "Missing rate details for high season.",
            Author2,
            now);

        offer.State.Should().Be(OfferState.Returned);
        offerReturn.Id.Should().Be(returnId);
        offerReturn.SubmissionId.Should().Be(submission.Id);
        offerReturn.ReturnedBy.Should().Be(Author2);
    }

    [Fact]
    public void RecordReturn_OnNonSubmittedOffer_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        var act = () => offer.RecordReturn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "incomplete_data",
            "Missing rate details.",
            Author2,
            DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "OFFER_NOT_SUBMITTED");
    }

    [Fact]
    public void RecordReturn_WithUnknownSubmission_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);

        var act = () => offer.RecordReturn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "incomplete_data",
            "Missing rate details.",
            Author2,
            now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "SUBMISSION_NOT_FOUND");
    }

    [Fact]
    public void IncrementRevisionMutate_ShouldIncrementRevisionAndUpdateAuthor()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;

        offer.IncrementRevisionMutate(Author2, now, null, () => { });

        offer.Revision.Should().Be(2);
        offer.RevisionAuthor.Should().Be(Author2);
        offer.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void IncrementRevisionMutate_WithStaleExpectedRevision_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        var act = () => offer.IncrementRevisionMutate(Author2, DateTimeOffset.UtcNow, 5, () => { });

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void IncrementRevisionMutate_OnPublishedOffer_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.MarkPublished(DateTimeOffset.UtcNow);

        var act = () => offer.IncrementRevisionMutate(Author2, DateTimeOffset.UtcNow, null, () => { });

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "PUBLISHED_OFFER_CHANGE_REQUIRES_F04");
    }

    [Fact]
    public void IncrementRevisionMutate_InvalidatesCurrentValidation()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        offer.CurrentValidation!.Status.Should().Be(ValidationStatus.Valid);

        offer.IncrementRevisionMutate(Author2, now, null, () => { });

        offer.CurrentValidation!.Status.Should().Be(ValidationStatus.Invalidated);
    }

    [Fact]
    public void IncrementRevisionMutate_AfterReturn_ShouldTransitionToDraft()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);
        offer.RecordReturn(Guid.NewGuid(), submission.Id, "incomplete_data", "Fix rates.", Author2, now);
        offer.State.Should().Be(OfferState.Returned);

        offer.IncrementRevisionMutate(Author1, now.AddMinutes(1), null, () => { });

        offer.State.Should().Be(OfferState.Draft);
    }

    [Fact]
    public void ConcurrentMutations_SameRevision_ShouldThrowOnSecond()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;

        offer.IncrementRevisionMutate(Author2, now, null, () => { });

        var act = () => offer.IncrementRevisionMutate(Author2, now, 1, () => { });

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void MultipleSequentialMutations_ShouldIncrementRevisionCorrectly()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        offer.IncrementRevisionMutate(Author2, DateTimeOffset.UtcNow, null, () => { });
        offer.Revision.Should().Be(2);

        offer.IncrementRevisionMutate(Author2, DateTimeOffset.UtcNow, 2, () => { });
        offer.Revision.Should().Be(3);

        offer.IncrementRevisionMutate(Author1, DateTimeOffset.UtcNow, 3, () => { });
        offer.Revision.Should().Be(4);
    }

    [Fact]
    public void MarkPublished_ShouldSetStateToPublished()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;

        offer.MarkPublished(now);

        offer.State.Should().Be(OfferState.Published);
        offer.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void MarkPublished_AlreadyPublished_ShouldNotThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.MarkPublished(DateTimeOffset.UtcNow);

        var act = () => offer.MarkPublished(DateTimeOffset.UtcNow.AddHours(1));

        act.Should().NotThrow();
        offer.State.Should().Be(OfferState.Published);
    }

    [Fact]
    public void Validate_WithBlankReviewer_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.RecalculateCompleteness(1, 1, 1, false, DateTimeOffset.UtcNow);

        var act = () => offer.Validate(Guid.NewGuid(), "   ", offer.Revision, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submit_WithBlankSnapshot_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);

        var act = () => offer.Submit(Guid.NewGuid(), "   ", Author1, offer.Revision, now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordReturn_WithBlankReasonCode_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);

        var act = () => offer.RecordReturn(Guid.NewGuid(), submission.Id, "   ", "Fix rates.", Author2, now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordReturn_WithBlankReason_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);

        var act = () => offer.RecordReturn(Guid.NewGuid(), submission.Id, "incomplete_data", "   ", Author2, now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordReturn_WithBlankReturnedBy_ShouldThrow()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);

        var act = () => offer.RecordReturn(Guid.NewGuid(), submission.Id, "incomplete_data", "Fix rates.", "   ", now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submissions_ShouldBeImmutableFromOutside()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);

        offer.Submissions.Should().HaveCount(1);
    }

    [Fact]
    public void Returns_ShouldBeImmutableFromOutside()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author1, offer.Revision, now);
        offer.RecordReturn(Guid.NewGuid(), submission.Id, "incomplete_data", "Fix rates.", Author2, now);

        offer.Returns.Should().HaveCount(1);
    }

    [Fact]
    public void RecalculateCompleteness_WithPartialComplete_StaysDraft()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        offer.RecalculateCompleteness(1, 0, 0, false, DateTimeOffset.UtcNow);

        offer.State.Should().Be(OfferState.Draft);
        offer.BlockingIssueCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RecalculateCompleteness_AccommodationsWithNoRates_ShouldBlock()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        offer.RecalculateCompleteness(2, 2, 0, false, DateTimeOffset.UtcNow);

        offer.HasAnyBlockingIssue(PendingIssueType.MissingActiveRate).Should().BeTrue();
        offer.State.Should().Be(OfferState.Draft);
    }

    [Fact]
    public void Validate_TrimmedReviewer_RemovesWhitespace()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        offer.RecalculateCompleteness(1, 1, 1, false, DateTimeOffset.UtcNow);

        offer.Validate(Guid.NewGuid(), "  staff-beta  ", offer.Revision, DateTimeOffset.UtcNow);

        offer.CurrentValidation!.ValidatedBy.Should().Be("staff-beta");
    }

    [Fact]
    public void Submit_TrimmedSubmitter()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, now);

        var submission = offer.Submit(Guid.NewGuid(), "{}", "  staff-alpha  ", offer.Revision, now);

        submission.SubmittedBy.Should().Be("staff-alpha");
    }

    [Fact]
    public void IncrementRevisionMutate_TrimsAuthor()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);

        offer.IncrementRevisionMutate("  staff-beta  ", DateTimeOffset.UtcNow, null, () => { });

        offer.RevisionAuthor.Should().Be("staff-beta");
    }

    [Fact]
    public void SetTargetSubmissionAt_ShouldPersistUtcValue()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, DateTimeOffset.UtcNow);
        var target = DateTimeOffset.Parse("2026-07-24T10:00:00+05:00");

        offer.SetTargetSubmissionAt(target);

        offer.TargetSubmissionAt.Should().Be(target.ToUniversalTime());
    }

    [Fact]
    public void MoneyInCents_FromBRL_ShouldConvertCorrectly()
    {
        var money = MoneyInCents.FromBRL(150.75m);

        money.Cents.Should().Be(15075);
        money.ToBRL().Should().Be(150.75m);
    }

    [Fact]
    public void MoneyInCents_Zero_CentsShouldBeZero()
    {
        var money = MoneyInCents.FromBRL(0m);

        money.Cents.Should().Be(0);
        money.ToBRL().Should().Be(0m);
    }

    [Fact]
    public void ChildAgeRange_Create_WithValidInputs_ShouldSucceed()
    {
        var range = ChildAgeRange.Create(2, 12);

        range.MinimumAge.Should().Be(2);
        range.MaximumAge.Should().Be(12);
    }

    [Fact]
    public void ChildAgeRange_Create_WithNegativeMinAge_ShouldThrow()
    {
        var act = () => ChildAgeRange.Create(-1, 12);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChildAgeRange_Create_WithMaxLessThanMin_ShouldThrow()
    {
        var act = () => ChildAgeRange.Create(10, 5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChildAgeRange_Create_WithMaxAbove17_ShouldThrow()
    {
        var act = () => ChildAgeRange.Create(5, 18);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BedEntry_Create_WithValidInputs_ShouldSucceed()
    {
        var bed = BedEntry.Create(BedType.Double, 2);

        bed.Type.Should().Be(BedType.Double);
        bed.Count.Should().Be(2);
    }

    [Fact]
    public void BedEntry_Create_WithZeroCount_ShouldThrow()
    {
        var act = () => BedEntry.Create(BedType.Single, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BedEntry_Create_WithNegativeCount_ShouldThrow()
    {
        var act = () => BedEntry.Create(BedType.Queen, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OfferValidation_Create_ShouldBeValid()
    {
        var validation = OfferValidation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            "reviewer-1",
            DateTimeOffset.UtcNow);

        validation.Status.Should().Be(ValidationStatus.Valid);
    }

    [Fact]
    public void OfferValidation_Invalidate_ShouldChangeStatus()
    {
        var validation = OfferValidation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            "reviewer-1",
            DateTimeOffset.UtcNow);

        validation.Invalidate(DateTimeOffset.UtcNow);

        validation.Status.Should().Be(ValidationStatus.Invalidated);
    }

    [Fact]
    public void OfferValidation_InvalidateTwice_ShouldStayInvalidated()
    {
        var validation = OfferValidation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            "reviewer-1",
            DateTimeOffset.UtcNow);
        validation.Invalidate(DateTimeOffset.UtcNow);

        validation.Invalidate(DateTimeOffset.UtcNow.AddHours(1));

        validation.Status.Should().Be(ValidationStatus.Invalidated);
    }

    [Fact]
    public void OfferSubmission_Create_ShouldStorePayload()
    {
        var snapshot = "{\"version\":2,\"accommodations\":[]}";

        var submission = OfferSubmission.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            5,
            snapshot,
            "submitter-1",
            DateTimeOffset.UtcNow);

        submission.SnapshotJson.Should().Be(snapshot);
        submission.Revision.Should().Be(5);
        submission.SubmittedBy.Should().Be("submitter-1");
    }

    [Fact]
    public void OfferReturn_Create_ShouldStoreEvidence()
    {
        var offerReturn = OfferReturn.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            4,
            "incomplete_data",
            "Missing rate details.",
            "curation-user",
            DateTimeOffset.UtcNow);

        offerReturn.ReasonCode.Should().Be("incomplete_data");
        offerReturn.Reason.Should().Be("Missing rate details.");
        offerReturn.ReturnedBy.Should().Be("curation-user");
        offerReturn.Revision.Should().Be(4);
    }

    [Fact]
    public void CommercialOfferIdempotencyKey_Create_ShouldStorePayload()
    {
        var key = CommercialOfferIdempotencyKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "submission",
            DateTimeOffset.UtcNow,
            "SHA256_FINGERPRINT",
            Guid.NewGuid());

        key.Scope.Should().Be("submission");
        key.PayloadFingerprint.Should().Be("SHA256_FINGERPRINT");
        key.ResultReferenceId.Should().NotBeNull();
    }

    [Fact]
    public void CommercialOfferIdempotencyKey_Create_WithoutFingerprint_ShouldBeNull()
    {
        var key = CommercialOfferIdempotencyKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "submission",
            DateTimeOffset.UtcNow);

        key.PayloadFingerprint.Should().BeNull();
        key.ResultReferenceId.Should().BeNull();
    }

    [Fact]
    public void CompletenessResult_Incomplete_ShouldHaveIssues()
    {
        var result = CompletenessResult.Incomplete(
            PendingIssueType.MissingPolicy,
            PendingIssueType.IncompleteAccommodation);

        result.IsComplete.Should().BeFalse();
        result.PendingIssues.Should().HaveCount(2);
        result.BlockingIssueCount.Should().Be(2);
    }

    [Fact]
    public void CompletenessResult_Complete_ShouldHaveNoIssues()
    {
        var result = CompletenessResult.Complete(3);

        result.IsComplete.Should().BeTrue();
        result.PendingIssues.Should().BeEmpty();
        result.AccommodationCount.Should().Be(3);
        result.BlockingIssueCount.Should().Be(0);
    }
}
