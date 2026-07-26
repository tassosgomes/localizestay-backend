using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.Modules.Inventory.Application.LegalPolicies;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class CommercialPolicyTests
{
    private const string StaffAlpha = "staff-alpha";
    private const string StaffBeta = "staff-beta";

    private static CommercialPolicyRuleSet FlexibleRuleSet => new(
        PolicyType.Flexible,
        "Flexible Policy",
        "Free cancellation up to 48h before check-in.",
        "v2026.01");

    private static CommercialPolicyRuleSet NonRefundableRuleSet => new(
        PolicyType.NonRefundable,
        "Non-Refundable Policy",
        "No refunds after booking.",
        "v2026.01");

    private static CommercialOffer CreateDraftOffer()
    {
        return CommercialOffer.Create(
            Guid.NewGuid(),
            StaffAlpha,
            DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
    }

    // --- 4.1 Model ---

    [Fact]
    public void Create_WithFlexibleRuleSet_ShouldCreateActivePolicy()
    {
        var propertyId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var policy = CommercialPolicy.Create(Guid.NewGuid(), propertyId, FlexibleRuleSet, false, now);

        policy.Type.Should().Be(PolicyType.Flexible);
        policy.Title.Should().Be("Flexible Policy");
        policy.RulesSummary.Should().Be("Free cancellation up to 48h before check-in.");
        policy.RuleSetVersion.Should().Be("v2026.01");
        policy.Status.Should().Be(PolicyStatus.Active);
        policy.IsDefault.Should().BeFalse();
        policy.UsageCount.Should().Be(0);
        policy.EverSubmitted.Should().BeFalse();
        policy.SubmissionIds.Should().BeEmpty();
        policy.PropertyId.Should().Be(propertyId);
    }

    [Fact]
    public void Create_WithNonRefundableRuleSet_ShouldCreateActivePolicy()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NonRefundableRuleSet,
            true,
            DateTimeOffset.UtcNow);

        policy.Type.Should().Be(PolicyType.NonRefundable);
        policy.IsDefault.Should().BeTrue();
        policy.Status.Should().Be(PolicyStatus.Active);
    }

    [Fact]
    public void SetDefault_WithActivePolicy_ShouldSetIsDefault()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);

        policy.SetDefault();

        policy.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void SetDefault_WithInactivePolicy_ShouldThrow()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);
        policy.Deactivate();

        var act = () => policy.SetDefault();

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "POLICY_NOT_ACTIVE");
    }

    [Fact]
    public void Deactivate_WithActivePolicy_ShouldSetInactive()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            true,
            DateTimeOffset.UtcNow);

        policy.Deactivate();

        policy.Status.Should().Be(PolicyStatus.Inactive);
        policy.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);
        policy.Deactivate();

        var act = () => policy.Deactivate();

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "POLICY_ALREADY_INACTIVE");
    }

    [Fact]
    public void CanDelete_NeverSubmittedNotDefaultNoUsage_ShouldReturnTrue()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);

        policy.CanDelete().Should().BeTrue();
    }

    [Fact]
    public void CanDelete_WhenEverSubmitted_ShouldReturnFalse()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);
        policy.MarkSubmitted(Guid.NewGuid());

        policy.CanDelete().Should().BeFalse();
    }

    [Fact]
    public void CanDelete_WhenDefault_ShouldReturnFalse()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            true,
            DateTimeOffset.UtcNow);

        policy.CanDelete().Should().BeFalse();
    }

    [Fact]
    public void CanDelete_WhenUsageGreaterThanZero_ShouldReturnFalse()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);
        policy.IncrementUsage();

        policy.CanDelete().Should().BeFalse();
    }

    // --- 4.2 Duplicate type ---

    [Fact]
    public void AddPolicy_DuplicateActiveType_ShouldThrowPolicyTypeAlreadyActive()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        var act = () => offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        act.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "POLICY_TYPE_ALREADY_ACTIVE");
    }

    [Fact]
    public void AddPolicy_DifferentTypes_ShouldSucceed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);
        offer.AddPolicy(Guid.NewGuid(), NonRefundableRuleSet, false, StaffAlpha, null, now);

        offer.Policies.Should().HaveCount(2);
    }

    [Fact]
    public void AddPolicy_InactiveTypeThenActive_ShouldSucceed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var first = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);
        first.Deactivate();

        var second = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        offer.Policies.Should().HaveCount(2);
    }

    // --- 4.3 Default policy ---

    [Fact]
    public void SetDefaultPolicy_WithActivePolicy_ShouldUpdateDefault()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        offer.SetDefaultPolicy(policy.Id, StaffBeta, null, now);

        offer.GetPolicy(policy.Id)!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void SetDefaultPolicy_ReplacesExistingDefault()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var first = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, true, StaffAlpha, null, now);
        var second = offer.AddPolicy(Guid.NewGuid(), NonRefundableRuleSet, false, StaffAlpha, null, now);

        offer.SetDefaultPolicy(second.Id, StaffBeta, null, now);

        offer.GetPolicy(first.Id)!.IsDefault.Should().BeFalse();
        offer.GetPolicy(second.Id)!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void SetDefaultPolicy_NotFound_ShouldThrow()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.SetDefaultPolicy(Guid.NewGuid(), StaffAlpha, null, now);

        act.Should().Throw<NotFoundException>()
            .Where(ex => ex.ErrorCode == "POLICY_NOT_FOUND");
    }

    // --- 4.4 Replacement / deactivation ---

    [Fact]
    public void DeactivatePolicy_WithValidReplacement_ShouldSucceed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);
        var replacement = offer.AddPolicy(Guid.NewGuid(), NonRefundableRuleSet, true, StaffAlpha, null, now);

        offer.DeactivatePolicy(policy.Id, replacement.Id, StaffAlpha, null, now);

        offer.GetPolicy(policy.Id)!.Status.Should().Be(PolicyStatus.Inactive);
        offer.GetPolicy(replacement.Id)!.Status.Should().Be(PolicyStatus.Active);
    }

    [Fact]
    public void DeactivatePolicy_WithoutReplacement_ShouldThrowReplacementRequired()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        var act = () => offer.DeactivatePolicy(policy.Id, Guid.NewGuid(), StaffAlpha, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REPLACEMENT_POLICY_REQUIRED");
    }

    [Fact]
    public void DeactivatePolicy_WithInactiveReplacement_ShouldThrow()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);
        var replacement = offer.AddPolicy(Guid.NewGuid(), NonRefundableRuleSet, false, StaffAlpha, null, now);
        replacement.Deactivate();

        var act = () => offer.DeactivatePolicy(policy.Id, replacement.Id, StaffAlpha, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REPLACEMENT_POLICY_REQUIRED");
    }

    [Fact]
    public void DeactivatePolicy_SameAsReplacement_ShouldThrow()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        var act = () => offer.DeactivatePolicy(policy.Id, policy.Id, StaffAlpha, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REPLACEMENT_POLICY_REQUIRED");
    }

    // --- 4.5 Delete ---

    [Fact]
    public void DeletePolicy_NotSubmittedNotDefaultNoUsage_ShouldSucceed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        offer.DeletePolicy(policy.Id, StaffBeta, null, now);

        offer.Policies.Should().BeEmpty();
    }

    [Fact]
    public void DeletePolicy_WhenDefault_ShouldThrowDeletionNotAllowed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, true, StaffAlpha, null, now);

        var act = () => offer.DeletePolicy(policy.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "POLICY_DELETION_NOT_ALLOWED");
    }

    [Fact]
    public void DeletePolicy_WhenEverSubmitted_ShouldThrowDeletionNotAllowed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);
        policy.MarkSubmitted(Guid.NewGuid());

        var act = () => offer.DeletePolicy(policy.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "POLICY_DELETION_NOT_ALLOWED");
    }

    [Fact]
    public void DeletePolicy_WhenUsageGreaterThanZero_ShouldThrowDeletionNotAllowed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);
        policy.IncrementUsage();

        var act = () => offer.DeletePolicy(policy.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "POLICY_DELETION_NOT_ALLOWED");
    }

    [Fact]
    public void DeletePolicy_NotFound_ShouldThrow()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.DeletePolicy(Guid.NewGuid(), StaffAlpha, null, now);

        act.Should().Throw<NotFoundException>()
            .Where(ex => ex.ErrorCode == "POLICY_NOT_FOUND");
    }

    // --- 4.6 Audit + invalidation ---

    [Fact]
    public void AddPolicy_ShouldIncrementRevision()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var initialRevision = offer.Revision;

        offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        offer.Revision.Should().Be(initialRevision + 1);
    }

    [Fact]
    public void AddPolicy_ShouldUpdateRevisionAuthor()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffBeta, null, now);

        offer.RevisionAuthor.Should().Be(StaffBeta);
    }

    [Fact]
    public void DeactivatePolicy_ShouldInvalidateCurrentValidation()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), StaffBeta, offer.Revision, now);
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);
        offer.SetTargetSubmissionAt(now.AddDays(2));

        var replacement = offer.AddPolicy(Guid.NewGuid(), NonRefundableRuleSet, false, StaffAlpha, null, now);

        offer.DeactivatePolicy(policy.Id, replacement.Id, StaffAlpha, null, now);

        offer.CurrentValidation!.Status.Should().Be(ValidationStatus.Invalidated);
    }

    [Fact]
    public void ExpectedRevision_ShouldEnforceOptimisticConcurrency()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, 999, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    // --- Error codes ---

    [Fact]
    public void DeactivatePolicy_ShouldProduceReplacementPolicyRequiredCode()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        var act = () => offer.DeactivatePolicy(policy.Id, Guid.NewGuid(), StaffAlpha, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REPLACEMENT_POLICY_REQUIRED");
    }

    [Fact]
    public void DeletePolicy_ShouldProducePolicyDeletionNotAllowedCode()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, true, StaffAlpha, null, now);

        var act = () => offer.DeletePolicy(policy.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "POLICY_DELETION_NOT_ALLOWED");
    }

    [Fact]
    public void AddPolicy_DuplicateType_ShouldProducePolicyTypeAlreadyActiveCode()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        var act = () => offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, false, StaffAlpha, null, now);

        act.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "POLICY_TYPE_ALREADY_ACTIVE");
    }

    // --- Submission history tracking ---

    [Fact]
    public void MarkSubmitted_ShouldSetEverSubmittedAndTrackId()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);
        var submissionId = Guid.NewGuid();

        policy.MarkSubmitted(submissionId);

        policy.EverSubmitted.Should().BeTrue();
        policy.SubmissionIds.Should().Contain(submissionId);
    }

    [Fact]
    public void IncrementAndDecrementUsage_ShouldTrackCorrectly()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);

        policy.IncrementUsage();
        policy.IncrementUsage();
        policy.UsageCount.Should().Be(2);

        policy.DecrementUsage();
        policy.UsageCount.Should().Be(1);
    }

    [Fact]
    public void DecrementUsage_WhenZero_ShouldThrowUnderflow()
    {
        var policy = CommercialPolicy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FlexibleRuleSet,
            false,
            DateTimeOffset.UtcNow);

        var act = () => policy.DecrementUsage();

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "POLICY_USAGE_UNDERFLOW");
    }
}
