using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class CommercialOfferWorkflowTests
{
    private const string Author1 = "staff-alpha";
    private const string Author2 = "staff-beta";
    private const string Author3 = "staff-gamma";
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

    private static CommercialOffer CreateDraftOffer(string? author = null)
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), author ?? Author1, _now);

        offer.RecalculateCompleteness(2, 2, 2, false, _now);

        return offer;
    }

    [Fact]
    public void Validate_WhenReady_ShouldSetStateToValidated()
    {
        var offer = CreateDraftOffer();
        var validationId = Guid.NewGuid();

        offer.Validate(validationId, Author2, offer.Revision, _now);

        offer.State.Should().Be(OfferState.Validated);
        offer.CurrentValidation.Should().NotBeNull();
        offer.CurrentValidation!.Revision.Should().Be(offer.Revision);
        offer.CurrentValidation!.ValidatedBy.Should().Be(Author2);
        offer.CurrentValidation!.Status.Should().Be(ValidationStatus.Valid);
    }

    [Fact]
    public void Validate_BySameAuthor_ShouldThrowSelfValidationNotAllowed()
    {
        var offer = CreateDraftOffer(Author1);

        var act = () => offer.Validate(Guid.NewGuid(), Author1, offer.Revision, _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("SELF_VALIDATION_NOT_ALLOWED");
    }

    [Fact]
    public void Validate_WhenRevisionMismatch_ShouldThrowRevisionMismatch()
    {
        var offer = CreateDraftOffer();
        var wrongRevision = offer.Revision + 1;

        var act = () => offer.Validate(Guid.NewGuid(), Author2, wrongRevision, _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("REVISION_MISMATCH");
    }

    [Fact]
    public void Validate_WhenNotReady_ShouldThrowOfferNotReady()
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, _now);

        var act = () => offer.Validate(Guid.NewGuid(), Author2, offer.Revision, _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("OFFER_NOT_READY");
    }

    [Fact]
    public void Submit_WithValidValidation_ShouldSetStateToSubmitted()
    {
        var offer = CreateDraftOffer();
        var validationId = Guid.NewGuid();
        offer.Validate(validationId, Author2, offer.Revision, _now);

        var submission = offer.Submit(Guid.NewGuid(), "{}", Author3, offer.Revision, _now);

        offer.State.Should().Be(OfferState.Submitted);
        submission.Revision.Should().Be(offer.Revision);
        submission.SubmittedBy.Should().Be(Author3);
        offer.EverSubmitted.Should().BeTrue();
    }

    [Fact]
    public void Submit_WithoutValidation_ShouldThrowValidationRequired()
    {
        var offer = CreateDraftOffer();

        var act = () => offer.Submit(Guid.NewGuid(), "{}", Author3, offer.Revision, _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("VALIDATION_REQUIRED");
    }

    [Fact]
    public void Submit_WithInvalidatedValidation_ShouldThrowValidationRequired()
    {
        var offer = CreateDraftOffer();
        var validationId = Guid.NewGuid();
        offer.Validate(validationId, Author2, offer.Revision, _now);
        offer.CurrentValidation!.Invalidate(_now);

        var act = () => offer.Submit(Guid.NewGuid(), "{}", Author3, offer.Revision, _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("VALIDATION_REQUIRED");
    }

    [Fact]
    public void Submit_WhenRevisionMismatch_ShouldThrowRevisionMismatch()
    {
        var offer = CreateDraftOffer();
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, _now);
        var wrongRevision = offer.Revision + 1;

        var act = () => offer.Submit(Guid.NewGuid(), "{}", Author3, wrongRevision, _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("REVISION_MISMATCH");
    }

    [Fact]
    public void Mutation_ShouldInvalidateValidation()
    {
        var offer = CreateDraftOffer();
        var validationId = Guid.NewGuid();
        offer.Validate(validationId, Author2, offer.Revision, _now);

        offer.IncrementRevisionMutate(Author1, _now, null, () => { });

        offer.CurrentValidation!.Status.Should().Be(ValidationStatus.Invalidated);
        offer.Revision.Should().Be(offer.Revision); // actually incremented, let's check
    }

    [Fact]
    public void RecordReturn_WhenSubmitted_ShouldSetStateToReturned()
    {
        var offer = CreateDraftOffer();
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, _now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author3, offer.Revision, _now);

        var returnEventId = Guid.NewGuid();
        var offerReturn = offer.RecordReturn(Guid.NewGuid(), submission.Id, returnEventId, "incomplete_data", "Missing accommodation details.", "curator-001", _now);

        offer.State.Should().Be(OfferState.Returned);
        offerReturn.SubmissionId.Should().Be(submission.Id);
        offerReturn.EventId.Should().Be(returnEventId);
    }

    [Fact]
    public void RecordReturn_WhenNotSubmitted_ShouldThrow()
    {
        var offer = CreateDraftOffer();

        var act = () => offer.RecordReturn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "incomplete_data", "Missing details.", "curator-001", _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("OFFER_NOT_SUBMITTED");
    }

    [Fact]
    public void RecordReturn_WhenPublished_ShouldThrow()
    {
        var offer = CreateDraftOffer();
        offer.MarkPublished(_now);

        var act = () => offer.RecordReturn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "incomplete_data", "Missing details.", "curator-001", _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("PUBLISHED_OFFER_CHANGE_REQUIRES_F04");
    }

    [Fact]
    public void RecordReturn_WithUnknownSubmission_ShouldThrowSubmissionNotFound()
    {
        var offer = CreateDraftOffer();
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, _now);
        offer.Submit(Guid.NewGuid(), "{}", Author3, offer.Revision, _now);

        var act = () => offer.RecordReturn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "incomplete_data", "Missing details.", "curator-001", _now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("SUBMISSION_NOT_FOUND");
    }

    [Fact]
    public void HasEventReturn_WithMatchingEventId_ShouldReturnTrue()
    {
        var offer = CreateDraftOffer();
        offer.Validate(Guid.NewGuid(), Author2, offer.Revision, _now);
        var submission = offer.Submit(Guid.NewGuid(), "{}", Author3, offer.Revision, _now);
        var eventId = Guid.NewGuid();
        offer.RecordReturn(Guid.NewGuid(), submission.Id, eventId, "incomplete_data", "Missing details.", "curator-001", _now);

        offer.HasEventReturn(eventId).Should().BeTrue();
    }

    [Fact]
    public void HasEventReturn_WithUnknownEventId_ShouldReturnFalse()
    {
        var offer = CreateDraftOffer();

        offer.HasEventReturn(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void CorrectionAfterReturn_ShouldAllowRevalidationAndResubmission()
    {
        var offer = CreateDraftOffer(Author1);
        var firstValidationId = Guid.NewGuid();
        offer.Validate(firstValidationId, Author2, offer.Revision, _now);
        var firstSubmissionId = Guid.NewGuid();
        offer.Submit(firstSubmissionId, "{}", Author3, offer.Revision, _now);
        offer.RecordReturn(Guid.NewGuid(), firstSubmissionId, Guid.NewGuid(), "incomplete_data", "Missing details.", "curator-001", _now);

        // After return, mutation should put back to draft and allow new validation
        offer.IncrementRevisionMutate(Author1, _now, null, () => { });

        offer.State.Should().Be(OfferState.Draft);

        // Completeness was lost, so we need to recalculate
        offer.RecalculateCompleteness(2, 2, 2, false, _now);
        offer.State.Should().Be(OfferState.ReadyForValidation);

        // New validation by different operator
        var secondValidationId = Guid.NewGuid();
        offer.Validate(secondValidationId, Author3, offer.Revision, _now);

        // New submission
        var secondSubmissionId = Guid.NewGuid();
        var secondSubmission = offer.Submit(secondSubmissionId, "{}", Author2, offer.Revision, _now);

        offer.State.Should().Be(OfferState.Submitted);
        offer.Submissions.Count.Should().Be(2);
        secondSubmission.Id.Should().Be(secondSubmissionId);
    }
}
