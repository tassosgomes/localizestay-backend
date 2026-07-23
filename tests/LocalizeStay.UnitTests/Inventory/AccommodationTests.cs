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

public sealed class AccommodationTests
{
    private const string StaffAlpha = "staff-alpha";
    private const string StaffBeta = "staff-beta";

    private static CommercialOffer CreateDraftOffer()
    {
        return CommercialOffer.Create(
            Guid.NewGuid(),
            StaffAlpha,
            DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
    }

    private static ChildAgeRange SampleChildAgeRange =>
        ChildAgeRange.Create(2, 12);

    private static CommercialPolicyRuleSet FlexibleRuleSet => new(
        PolicyType.Flexible,
        "Flexible Policy",
        "Free cancellation up to 48h before check-in.",
        "v2026.01");

    private static CommercialOffer CreateOfferWithDefaultPolicy()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, true, StaffAlpha, null, now);
        return offer;
    }

    // --- 5.1 Model ---

    [Fact]
    public void Create_WithMinimalData_ShouldCreateActiveAccommodation()
    {
        var propertyId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var accommodation = Accommodation.Create(
            Guid.NewGuid(),
            propertyId,
            "Standard Room",
            null,
            null,
            now);

        accommodation.Id.Should().NotBeEmpty();
        accommodation.PropertyId.Should().Be(propertyId);
        accommodation.CommercialName.Should().Be("Standard Room");
        accommodation.Status.Should().Be(AccommodationStatus.Active);
        accommodation.EverSubmitted.Should().BeFalse();
        accommodation.DeactivationReason.Should().BeNull();
        accommodation.MaxAdults.Should().BeNull();
        accommodation.MaxChildren.Should().BeNull();
        accommodation.TotalCapacity.Should().BeNull();
        accommodation.MealPlan.Should().BeNull();
        accommodation.ChildAgeRangeSource.Should().Be(ChildAgeRangeSource.None);
        accommodation.ChildMinimumAge.Should().BeNull();
        accommodation.ChildMaximumAge.Should().BeNull();
        accommodation.PolicyId.Should().BeNull();
        accommodation.BedConfiguration.Should().BeEmpty();
        accommodation.StructuralFeatures.Should().BeEmpty();
        accommodation.CreatedAt.Should().Be(now);
        accommodation.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithDefaultPolicyAndChildAgeRange_ShouldInherit()
    {
        var propertyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var defaultPolicyId = Guid.NewGuid();
        var propertyChildAge = ChildAgeRange.Create(3, 12);

        var accommodation = Accommodation.Create(
            Guid.NewGuid(),
            propertyId,
            "Family Suite",
            defaultPolicyId,
            propertyChildAge,
            now);

        accommodation.PolicyId.Should().Be(defaultPolicyId);
        accommodation.ChildAgeRangeSource.Should().Be(ChildAgeRangeSource.PropertyDefault);
        accommodation.ChildMinimumAge.Should().Be(3);
        accommodation.ChildMaximumAge.Should().Be(12);
    }

    [Fact]
    public void Create_WithBlankCommercialName_ShouldThrow()
    {
        var act = () => Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "   ", null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhiteSpaceName_ShouldThrow(string name)
    {
        var act = () => Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), name, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameTooShort_ShouldThrow()
    {
        var act = () => Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "A", null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameTooLong_ShouldThrow()
    {
        var longName = new string('a', 181);
        var act = () => Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), longName, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    // --- Occupancy ---

    [Fact]
    public void SetOccupancy_ValidConfig_ShouldUpdate()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        accommodation.SetOccupancy(2, 1, 3);

        accommodation.MaxAdults.Should().Be(2);
        accommodation.MaxChildren.Should().Be(1);
        accommodation.TotalCapacity.Should().Be(3);
    }

    [Fact]
    public void SetOccupancy_ExceedsTotalCapacity_ShouldThrowInvalidOccupancy()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        var act = () => accommodation.SetOccupancy(3, 2, 4);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "INVALID_OCCUPANCY_CONFIGURATION");
    }

    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(1, 0, 1, true)]
    [InlineData(2, 2, 4, true)]
    [InlineData(3, 2, 4, false)]
    [InlineData(2, 1, 2, false)]
    [InlineData(20, 10, 30, true)]
    [InlineData(30, 1, 30, false)]
    public void SetOccupancy_BoundaryValues(int adults, int children, int capacity, bool shouldSucceed)
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        var act = () => accommodation.SetOccupancy(adults, children, capacity);

        if (shouldSucceed)
            act.Should().NotThrow();
        else
            act.Should().Throw<BusinessRuleViolationException>()
                .Where(ex => ex.ErrorCode == "INVALID_OCCUPANCY_CONFIGURATION");
    }

    // --- 5.5 Child Age Range Inheritance ---

    [Fact]
    public void SetChildAgeRangeOverride_SetsAccommodationOverrideSource()
    {
        var propertyChildAge = ChildAgeRange.Create(3, 12);
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, propertyChildAge, DateTimeOffset.UtcNow);

        accommodation.SetChildAgeRangeOverride(ChildAgeRange.Create(5, 10));

        accommodation.ChildAgeRangeSource.Should().Be(ChildAgeRangeSource.AccommodationOverride);
        accommodation.ChildMinimumAge.Should().Be(5);
        accommodation.ChildMaximumAge.Should().Be(10);
    }

    [Fact]
    public void SetChildAgeRangeOverride_WithNull_ClearsRange()
    {
        var propertyChildAge = ChildAgeRange.Create(3, 12);
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, propertyChildAge, DateTimeOffset.UtcNow);

        accommodation.SetChildAgeRangeOverride(null);

        accommodation.ChildAgeRangeSource.Should().Be(ChildAgeRangeSource.None);
        accommodation.ChildMinimumAge.Should().BeNull();
        accommodation.ChildMaximumAge.Should().BeNull();
    }

    [Fact]
    public void RevertChildAgeRangeToPropertyDefault_RestoresInheritedValues()
    {
        var propertyChildAge = ChildAgeRange.Create(3, 12);
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, propertyChildAge, DateTimeOffset.UtcNow);
        accommodation.SetChildAgeRangeOverride(ChildAgeRange.Create(5, 10));

        accommodation.RevertChildAgeRangeToPropertyDefault(propertyChildAge);

        accommodation.ChildAgeRangeSource.Should().Be(ChildAgeRangeSource.PropertyDefault);
        accommodation.ChildMinimumAge.Should().Be(3);
        accommodation.ChildMaximumAge.Should().Be(12);
    }

    [Fact]
    public void RevertChildAgeRangeToPropertyDefault_WhenPropertyHasNone_SetsNone()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        accommodation.SetChildAgeRangeOverride(ChildAgeRange.Create(5, 10));

        accommodation.RevertChildAgeRangeToPropertyDefault(null);

        accommodation.ChildAgeRangeSource.Should().Be(ChildAgeRangeSource.None);
        accommodation.ChildMinimumAge.Should().BeNull();
        accommodation.ChildMaximumAge.Should().BeNull();
    }

    // --- Bed Configuration ---

    [Fact]
    public void SetBedConfiguration_ShouldStoreEntries()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        var beds = new List<BedEntry>
        {
            BedEntry.Create(BedType.King, 1),
            BedEntry.Create(BedType.Single, 2),
        };

        accommodation.SetBedConfiguration(beds);

        accommodation.BedConfiguration.Should().HaveCount(2);
        accommodation.BedConfiguration[0].Type.Should().Be(BedType.King);
        accommodation.BedConfiguration[0].Count.Should().Be(1);
    }

    // --- Meal Plan ---

    [Fact]
    public void SetMealPlan_ShouldUpdate()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        accommodation.SetMealPlan(MealPlan.Breakfast);

        accommodation.MealPlan.Should().Be(MealPlan.Breakfast);
    }

    [Fact]
    public void SetMealPlan_Null_ShouldClear()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        accommodation.SetMealPlan(MealPlan.Breakfast);

        accommodation.SetMealPlan(null);

        accommodation.MealPlan.Should().BeNull();
    }

    // --- Deactivation ---

    [Fact]
    public void Deactivate_WithReason_ShouldDeactivate()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        accommodation.Deactivate("Renovation in progress");

        accommodation.Status.Should().Be(AccommodationStatus.Inactive);
        accommodation.DeactivationReason.Should().Be("Renovation in progress");
    }

    [Fact]
    public void Deactivate_WithoutReason_ShouldThrow()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        var act = () => accommodation.Deactivate("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        accommodation.Deactivate("First deactivation");

        var act = () => accommodation.Deactivate("Second deactivation");

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "ACCOMMODATION_ALREADY_INACTIVE");
    }

    // --- Delete protection ---

    [Fact]
    public void CanDelete_NeverSubmitted_ShouldReturnTrue()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        accommodation.CanDelete().Should().BeTrue();
    }

    [Fact]
    public void CanDelete_AfterSubmission_ShouldReturnFalse()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        accommodation.MarkSubmitted(Guid.NewGuid());

        accommodation.CanDelete().Should().BeFalse();
    }

    // --- Commercial completeness ---

    [Fact]
    public void IsCommerciallyComplete_AllFieldsSet_ShouldReturnTrue()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Complete Room", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        accommodation.SetOccupancy(2, 1, 3);
        accommodation.SetMealPlan(MealPlan.Breakfast);

        accommodation.IsCommerciallyComplete().Should().BeTrue();
    }

    [Fact]
    public void IsCommerciallyComplete_MissingOccupancy_ShouldReturnFalse()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Incomplete Room", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        accommodation.SetMealPlan(MealPlan.Breakfast);

        accommodation.IsCommerciallyComplete().Should().BeFalse();
    }

    [Fact]
    public void IsCommerciallyComplete_MissingPolicy_ShouldReturnFalse()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        accommodation.SetOccupancy(2, 1, 3);
        accommodation.SetMealPlan(MealPlan.Breakfast);

        accommodation.IsCommerciallyComplete().Should().BeFalse();
    }

    [Fact]
    public void IsCommerciallyComplete_MissingMealPlan_ShouldReturnFalse()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        accommodation.SetOccupancy(2, 1, 3);

        accommodation.IsCommerciallyComplete().Should().BeFalse();
    }

    [Fact]
    public void IsCommerciallyComplete_InvalidOccupancy_ShouldReturnFalse()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        accommodation.SetMealPlan(MealPlan.Breakfast);

        accommodation.IsCommerciallyComplete().Should().BeFalse();
    }

    // --- 5.2 Add through aggregate ---

    [Fact]
    public void AddAccommodation_ThroughAggregate_ShouldAddAndReturn()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Standard Room", null, null, StaffAlpha, null, now);

        offer.Accommodations.Should().HaveCount(1);
        offer.Accommodations[0].CommercialName.Should().Be("Standard Room");
        accommodation.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAccommodation_ShouldIncrementRevision()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var initialRevision = offer.Revision;

        offer.AddAccommodation(Guid.NewGuid(), "Room", null, null, StaffAlpha, null, now);

        offer.Revision.Should().Be(initialRevision + 1);
    }

    [Fact]
    public void AddAccommodation_ShouldUpdateRevisionAuthor()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        offer.AddAccommodation(Guid.NewGuid(), "Room", null, null, StaffBeta, null, now);

        offer.RevisionAuthor.Should().Be(StaffBeta);
    }

    [Fact]
    public void AddAccommodation_WithDefaultPolicy_ShouldInherit()
    {
        var offer = CreateOfferWithDefaultPolicy();
        var now = DateTimeOffset.UtcNow;
        var defaultPolicy = offer.Policies.First(p => p.IsDefault);

        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", defaultPolicy.Id, null, StaffAlpha, null, now);

        accommodation.PolicyId.Should().Be(defaultPolicy.Id);
    }

    [Fact]
    public void AddAccommodation_WithPropertyChildAge_ShouldInherit()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var childAge = ChildAgeRange.Create(3, 12);

        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", null, childAge, StaffAlpha, null, now);

        accommodation.ChildAgeRangeSource.Should().Be(ChildAgeRangeSource.PropertyDefault);
        accommodation.ChildMinimumAge.Should().Be(3);
        accommodation.ChildMaximumAge.Should().Be(12);
    }

    // --- 5.3 Update through aggregate ---

    [Fact]
    public void UpdateAccommodation_ShouldApplyChanges()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Old Name", null, null, StaffAlpha, null, now);

        offer.UpdateAccommodation(accommodation.Id, StaffBeta, null, now, acc =>
        {
            acc.UpdateCommercialName("Updated Name");
        });

        var updated = offer.GetAccommodation(accommodation.Id)!;
        updated.CommercialName.Should().Be("Updated Name");
    }

    [Fact]
    public void UpdateAccommodation_NotFound_ShouldThrowAccommodationNotFound()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.UpdateAccommodation(
            Guid.NewGuid(), StaffAlpha, null, now, _ => { });

        act.Should().Throw<NotFoundException>()
            .Where(ex => ex.ErrorCode == "ACCOMMODATION_NOT_FOUND");
    }

    [Fact]
    public void UpdateAccommodation_ShouldInvalidateValidation()
    {
        var offer = CreateOfferWithDefaultPolicy();
        var now = DateTimeOffset.UtcNow;
        var defaultPolicyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", defaultPolicyId, null, StaffAlpha, null, now);
        accommodation.SetOccupancy(2, 1, 3);
        accommodation.SetMealPlan(MealPlan.Breakfast);
        offer.RecalculateCompleteness(1, 1, 1, false, now);
        offer.Validate(Guid.NewGuid(), StaffBeta, offer.Revision, now);
        var later = now.AddMinutes(5);

        offer.UpdateAccommodation(accommodation.Id, StaffAlpha, null, later, acc =>
        {
            acc.UpdateCommercialName("New Name");
        });

        offer.CurrentValidation!.Status.Should().Be(ValidationStatus.Invalidated);
    }

    // --- 5.4 Delete through aggregate ---

    [Fact]
    public void DeleteAccommodation_NeverSubmitted_ShouldRemove()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", null, null, StaffAlpha, null, now);

        offer.DeleteAccommodation(accommodation.Id, StaffBeta, null, now);

        offer.Accommodations.Should().BeEmpty();
    }

    [Fact]
    public void DeleteAccommodation_NotFound_ShouldThrowAccommodationNotFound()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.DeleteAccommodation(Guid.NewGuid(), StaffAlpha, null, now);

        act.Should().Throw<NotFoundException>()
            .Where(ex => ex.ErrorCode == "ACCOMMODATION_NOT_FOUND");
    }

    [Fact]
    public void DeleteAccommodation_AlreadySubmitted_ShouldThrowDeletionNotAllowed()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", null, null, StaffAlpha, null, now);
        accommodation.MarkSubmitted(Guid.NewGuid());

        var act = () => offer.DeleteAccommodation(accommodation.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "ACCOMMODATION_DELETION_NOT_ALLOWED");
    }

    // --- Revision concurrency ---

    [Fact]
    public void AddAccommodation_WithWrongExpectedRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.AddAccommodation(
            Guid.NewGuid(), "Room", null, null, StaffAlpha, 999, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void UpdateAccommodation_WithWrongExpectedRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", null, null, StaffAlpha, null, now);

        var act = () => offer.UpdateAccommodation(
            accommodation.Id, StaffBeta, 999, now, _ => { });

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void DeleteAccommodation_WithWrongExpectedRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", null, null, StaffAlpha, null, now);

        var act = () => offer.DeleteAccommodation(accommodation.Id, StaffBeta, 999, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    // --- Error codes ---

    [Fact]
    public void SetOccupancy_ExceedsCapacity_ShouldProduceInvalidOccupancyConfigurationCode()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        var act = () => accommodation.SetOccupancy(5, 2, 6);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "INVALID_OCCUPANCY_CONFIGURATION");
    }

    [Fact]
    public void DeleteAccommodation_EverSubmitted_ShouldProduceAccommodationDeletionNotAllowedCode()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(), "Room", null, null, StaffAlpha, null, now);
        accommodation.MarkSubmitted(Guid.NewGuid());

        var act = () => offer.DeleteAccommodation(accommodation.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "ACCOMMODATION_DELETION_NOT_ALLOWED");
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldProduceAccommodationAlreadyInactiveCode()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        accommodation.Deactivate("First");

        var act = () => accommodation.Deactivate("Second");

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "ACCOMMODATION_ALREADY_INACTIVE");
    }

    // --- Structural features ---

    [Fact]
    public void SetStructuralFeatures_ShouldStoreFeatures()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);

        accommodation.SetStructuralFeatures(["Balcony", "Air Conditioning"]);

        accommodation.StructuralFeatures.Should().Contain(["Balcony", "Air Conditioning"]);
    }

    [Fact]
    public void SetStructuralFeatures_EmptyList_ShouldClear()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        accommodation.SetStructuralFeatures(["Balcony"]);

        accommodation.SetStructuralFeatures([]);

        accommodation.StructuralFeatures.Should().BeEmpty();
    }

    // --- Commercially complete without editorial content ---

    [Fact]
    public void IsCommerciallyComplete_WithoutPhotosDescriptionOrAmenities_ShouldReturnTrue()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Complete Room", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        accommodation.SetOccupancy(2, 0, 2);
        accommodation.SetMealPlan(MealPlan.RoomOnly);

        accommodation.IsCommerciallyComplete().Should().BeTrue();
    }

    // --- Submission tracking ---

    [Fact]
    public void MarkSubmitted_ShouldSetEverSubmittedAndTrackId()
    {
        var accommodation = Accommodation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Room", null, null, DateTimeOffset.UtcNow);
        var submissionId = Guid.NewGuid();

        accommodation.MarkSubmitted(submissionId);

        accommodation.EverSubmitted.Should().BeTrue();
        accommodation.SubmissionIds.Should().Contain(submissionId);
    }
}
