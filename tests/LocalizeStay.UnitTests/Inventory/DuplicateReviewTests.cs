using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.UnitTests.Inventory;

public class DuplicateReviewTests
{
    private static PropertyOnboarding CreateOnboarding()
    {
        var property = new Property(
            "Pousada Mar do Sol",
            "dest-porto-de-galinhas",
            new Address(
                "Avenida Beira Mar",
                "250",
                null,
                "Centro",
                "Ipojuca",
                "PE",
                "55590-000",
                "BR"));

        return PropertyOnboarding.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "preselection-2026-0042",
            property,
            DateTimeOffset.Parse("2026-07-18T13:00:00Z"),
            TimeSpan.FromDays(10));
    }

    [Fact]
    public void SubmitDuplicateReview_WhenFlaggedAsDuplicate_ShouldResolveAndSetNotDuplicate()
    {
        var onboarding = CreateOnboarding();
        onboarding.FlagDuplicateReviewRequired();
        var reviewId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        var review = onboarding.SubmitDuplicateReview(
            reviewId,
            DuplicateReviewDecision.NotDuplicate,
            null,
            "Endereços próximos, porém estabelecimentos distintos.",
            DateTimeOffset.Parse("2026-07-19T14:00:00Z"),
            "staff-001",
            idempotencyKey);

        review.Decision.Should().Be(DuplicateReviewDecision.NotDuplicate);
        review.ExistingPropertyId.Should().BeNull();
        onboarding.DuplicateReviewRequiresDecision.Should().BeFalse();
        onboarding.DuplicateReviews.Should().ContainSingle();
    }

    [Fact]
    public void SubmitDuplicateReview_WhenConfirmedDuplicate_ShouldCloseOnboarding()
    {
        var onboarding = CreateOnboarding();
        onboarding.FlagDuplicateReviewRequired();
        var existingPropertyId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        var review = onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.DuplicateOfExistingProperty,
            existingPropertyId,
            "Mesma inscrição legal e endereço idêntico.",
            DateTimeOffset.Parse("2026-07-19T14:00:00Z"),
            "staff-001",
            idempotencyKey);

        review.Decision.Should().Be(DuplicateReviewDecision.DuplicateOfExistingProperty);
        review.ExistingPropertyId.Should().Be(existingPropertyId);
        onboarding.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.Closed);
        onboarding.ReasonCode.Should().Be(CloseReasonCode.DuplicateProperty);
    }

    [Fact]
    public void SubmitDuplicateReview_WithoutExistingPropertyId_WhenDuplicate_ShouldThrow()
    {
        var onboarding = CreateOnboarding();
        onboarding.FlagDuplicateReviewRequired();

        var act = () => onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.DuplicateOfExistingProperty,
            null,
            "Justification text that meets the minimum length requirement.",
            DateTimeOffset.UtcNow,
            "staff-001",
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SubmitDuplicateReview_WhenNotFlaggedAndNotDuplicateDecision_ShouldThrow()
    {
        var onboarding = CreateOnboarding();

        var act = () => onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.NotDuplicate,
            null,
            "Justification text that meets the minimum length requirement.",
            DateTimeOffset.UtcNow,
            "staff-001",
            Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void SubmitDuplicateReview_WhenNotFlaggedAndDuplicateDecision_ShouldThrow()
    {
        var onboarding = CreateOnboarding();

        var act = () => onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.DuplicateOfExistingProperty,
            Guid.NewGuid(),
            "Justification text that meets the minimum length requirement.",
            DateTimeOffset.UtcNow,
            "staff-001",
            Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "DUPLICATE_REVIEW_NOT_PENDING");
    }

    [Fact]
    public void SubmitDuplicateReview_WithSameIdempotencyKey_ShouldBeIdempotent()
    {
        var onboarding = CreateOnboarding();
        onboarding.FlagDuplicateReviewRequired();
        var idempotencyKey = Guid.NewGuid();

        onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.NotDuplicate,
            null,
            "Endereços próximos, porém estabelecimentos distintos.",
            DateTimeOffset.Parse("2026-07-19T14:00:00Z"),
            "staff-001",
            idempotencyKey);

        var act = () => onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.NotDuplicate,
            null,
            "Endereços próximos, porém estabelecimentos distintos.",
            DateTimeOffset.Parse("2026-07-19T14:00:00Z"),
            "staff-001",
            idempotencyKey);

        act.Should().Throw<IdempotentReplayException>();
    }

    [Fact]
    public void SubmitDuplicateReview_WithSameKeyForDifferentScope_ShouldThrowConflict()
    {
        var onboarding = CreateOnboarding();
        onboarding.FlagDuplicateReviewRequired();
        var idempotencyKey = Guid.NewGuid();

        onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.NotDuplicate,
            null,
            "Endereços próximos, porém estabelecimentos distintos.",
            DateTimeOffset.Parse("2026-07-19T14:00:00Z"),
            "staff-001",
            idempotencyKey);

        FillAllGates(onboarding);
        ResolveAllIssues(onboarding);

        var act = () => onboarding.SubmitToCuration(
            idempotencyKey,
            "Ready for curation",
            DateTimeOffset.Parse("2026-07-20T10:00:00Z"),
            "staff-001");

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void SubmitDuplicateReview_WithShortJustification_ShouldThrow()
    {
        var onboarding = CreateOnboarding();
        onboarding.FlagDuplicateReviewRequired();

        var act = () => onboarding.SubmitDuplicateReview(
            Guid.NewGuid(),
            DuplicateReviewDecision.NotDuplicate,
            null,
            "Too short",
            DateTimeOffset.UtcNow,
            "staff-001",
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    private static void FillAllGates(PropertyOnboarding onboarding)
    {
        foreach (var gateType in Enum.GetValues<ReadinessGateType>())
        {
            var evidence = gateType switch
            {
                ReadinessGateType.SignedContract => new[] { new EvidenceReference(EvidenceKind.Contract, "contract-ref", "Contract signed") },
                ReadinessGateType.AuthorizedContact => new[] { new EvidenceReference(EvidenceKind.FormalAuthorization, "auth-ref", "Authorization verified") },
                ReadinessGateType.OperationalChannel => new[] { new EvidenceReference(EvidenceKind.Communication, "comm-ref", "Channel tested") },
                _ => new[] { new EvidenceReference(EvidenceKind.OfficialDocument, "doc-ref", "Document verified") },
            };

            onboarding.ValidateGate(gateType, evidence, "staff-001", DateTimeOffset.UtcNow);
        }
    }

    private static void ResolveAllIssues(PropertyOnboarding onboarding)
    {
        foreach (var issue in onboarding.PendingIssues.Where(i => i.Status == PendingIssueStatus.Open).ToList())
        {
            onboarding.ResolvePendingIssue(issue.Id, "Resolved", DateTimeOffset.UtcNow);
        }
    }
}
