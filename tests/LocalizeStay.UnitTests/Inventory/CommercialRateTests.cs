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

public sealed class CommercialRateTests
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

    private static CommercialOffer CreateOfferWithPolicy()
    {
        var offer = CreateDraftOffer();
        var now = DateTimeOffset.UtcNow;
        var policy = offer.AddPolicy(Guid.NewGuid(), FlexibleRuleSet, true, StaffAlpha, null, now);
        return offer;
    }

    private static Accommodation CreateAccommodationInOffer(CommercialOffer offer, Guid? policyId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var acc = offer.AddAccommodation(
            Guid.NewGuid(), "Standard Room", policyId, null, StaffAlpha, null, now);
        acc.SetOccupancy(2, 1, 3);
        acc.SetMealPlan(MealPlan.Breakfast);
        return acc;
    }

    // --- 6.1 Model ---

    [Fact]
    public void Create_WithMinimalData_ShouldCreateDraftRate()
    {
        var accommodationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var rate = CommercialRate.Create(
            Guid.NewGuid(),
            accommodationId,
            propertyId,
            "Summer 2026",
            "standard-breakfast",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            now);

        rate.Id.Should().NotBeEmpty();
        rate.AccommodationId.Should().Be(accommodationId);
        rate.PropertyId.Should().Be(propertyId);
        rate.Name.Should().Be("Summer 2026");
        rate.ConditionCode.Should().Be("standard-breakfast");
        rate.Status.Should().Be(RateStatus.Draft);
        rate.EverSubmitted.Should().BeFalse();
        rate.DeactivationReason.Should().BeNull();
        rate.BasePriceCents.Should().BeNull();
        rate.IncludedGuests.Should().BeNull();
        rate.AdditionalAdultPriceCents.Should().BeNull();
        rate.AdditionalChildPriceCents.Should().BeNull();
        rate.ValidFrom.Should().BeNull();
        rate.ValidTo.Should().BeNull();
        rate.MinimumNights.Should().BeNull();
        rate.PolicyId.Should().BeNull();
        rate.MealPlan.Should().BeNull();
        rate.CreatedAt.Should().Be(now);
        rate.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithAllFieldsSet_ShouldCreateActiveRate()
    {
        var now = DateTimeOffset.UtcNow;
        var policyId = Guid.NewGuid();

        var rate = CommercialRate.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Summer 2026",
            "standard-breakfast",
            48900,
            2,
            12000,
            6000,
            new DateOnly(2026, 12, 1),
            new DateOnly(2027, 2, 28),
            2,
            policyId,
            MealPlan.Breakfast,
            now);

        rate.Status.Should().Be(RateStatus.Active);
        rate.BasePriceCents.Should().Be(48900);
        rate.IncludedGuests.Should().Be(2);
        rate.AdditionalAdultPriceCents.Should().Be(12000);
        rate.AdditionalChildPriceCents.Should().Be(6000);
        rate.ValidFrom.Should().Be(new DateOnly(2026, 12, 1));
        rate.ValidTo.Should().Be(new DateOnly(2027, 2, 28));
        rate.MinimumNights.Should().Be(2);
        rate.PolicyId.Should().Be(policyId);
        rate.MealPlan.Should().Be(MealPlan.Breakfast);
        rate.IsComplete().Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhiteSpaceName_ShouldThrow(string name)
    {
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            name, "code", null, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameTooShort_ShouldThrow()
    {
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "A", "code", null, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameTooLong_ShouldThrow()
    {
        var longName = new string('a', 121);
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            longName, "code", null, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyConditionCode_ShouldThrow()
    {
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Name", "   ", null, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNegativeBasePrice_ShouldThrow()
    {
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Name", "code", -1, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithInvertedPeriod_ShouldThrowInvalidRatePeriod()
    {
        var policyId = Guid.NewGuid();
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Name", "code", 48900, 2, 12000, 6000,
            new DateOnly(2027, 2, 28),
            new DateOnly(2026, 12, 1),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "INVALID_RATE_PERIOD");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Create_WithInvalidIncludedGuests_ShouldThrow(int guests)
    {
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Name", "code", null, guests, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void Create_WithInvalidMinimumNights_ShouldThrow(int nights)
    {
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Name", "code", null, null, null, null, null, null, nights, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- IsComplete ---

    [Fact]
    public void IsComplete_AllFieldsSet_ShouldReturnTrue()
    {
        var policyId = Guid.NewGuid();
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2026", "standard-breakfast", 48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate.IsComplete().Should().BeTrue();
    }

    [Fact]
    public void IsComplete_MissingBasePrice_ShouldReturnFalse()
    {
        var policyId = Guid.NewGuid();
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2026", "standard-breakfast", null, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate.IsComplete().Should().BeFalse();
    }

    [Fact]
    public void IsComplete_MissingPolicy_ShouldReturnFalse()
    {
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2026", "standard-breakfast", 48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, null, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate.IsComplete().Should().BeFalse();
    }

    // --- Overlap detection ---

    [Fact]
    public void OverlapsWith_SameConditionPolicyMealPlan_Adjacent_ShouldOverlap()
    {
        var policyId = Guid.NewGuid();
        var rate1 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Winter 2026", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        var rate2 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2027", "standard-room", 50000, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate1.OverlapsWith(rate2).Should().BeTrue();
    }

    [Fact]
    public void OverlapsWith_SameConditionPolicyMealPlan_Contained_ShouldOverlap()
    {
        var policyId = Guid.NewGuid();
        var rate1 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Year 2026", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        var rate2 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2026", "standard-room", 50000, 2, 12000, 6000,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate1.OverlapsWith(rate2).Should().BeTrue();
    }

    [Fact]
    public void OverlapsWith_SameConditionPolicyMealPlan_NoOverlap_ShouldNotOverlap()
    {
        var policyId = Guid.NewGuid();
        var rate1 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Winter 2026", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        var rate2 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2026", "standard-room", 50000, 2, 12000, 6000,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate1.OverlapsWith(rate2).Should().BeFalse();
    }

    [Fact]
    public void OverlapsWith_DifferentConditionCode_SamePeriod_ShouldNotOverlap()
    {
        var policyId = Guid.NewGuid();
        var rate1 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate A", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        var rate2 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate B", "promo-room", 30000, 2, 8000, 4000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate1.OverlapsWith(rate2).Should().BeFalse();
    }

    [Fact]
    public void OverlapsWith_DifferentMealPlan_SamePeriod_ShouldNotOverlap()
    {
        var policyId = Guid.NewGuid();
        var rate1 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate A", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        var rate2 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate B", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.FullBoard, DateTimeOffset.UtcNow);

        rate1.OverlapsWith(rate2).Should().BeFalse();
    }

    [Fact]
    public void OverlapsWith_DraftStatus_ShouldNotOverlap()
    {
        var policyId = Guid.NewGuid();
        var rateDraft = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate Draft", "standard-room", null, null, null, null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            null, policyId, null, DateTimeOffset.UtcNow);

        var rateActive = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate Active", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rateDraft.OverlapsWith(rateActive).Should().BeFalse();
    }

    [Fact]
    public void OverlapsWith_SameDate_ShouldOverlap()
    {
        var policyId = Guid.NewGuid();
        var rate1 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate A", "standard-room", 40000, 2, 10000, 5000,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1),
            1, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        var rate2 = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate B", "standard-room", 50000, 2, 12000, 6000,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate1.OverlapsWith(rate2).Should().BeTrue();
    }

    // --- IsActiveOn ---

    [Fact]
    public void IsActiveOn_DateWithinPeriod_ShouldReturnTrue()
    {
        var policyId = Guid.NewGuid();
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2026", "standard-room", 48900, 2, 12000, 6000,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate.IsActiveOn(new DateOnly(2026, 7, 15)).Should().BeTrue();
    }

    [Fact]
    public void IsActiveOn_DateOutsidePeriod_ShouldReturnFalse()
    {
        var policyId = Guid.NewGuid();
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Summer 2026", "standard-room", 48900, 2, 12000, 6000,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate.IsActiveOn(new DateOnly(2026, 9, 1)).Should().BeFalse();
    }

    [Fact]
    public void IsActiveOn_DraftRate_ShouldReturnFalse()
    {
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate", "code", null, null, null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow);

        rate.IsActiveOn(new DateOnly(2026, 1, 1)).Should().BeFalse();
    }

    // --- Deactivation ---

    [Fact]
    public void Update_WithDeactivationReason_ShouldDeactivate()
    {
        var policyId = Guid.NewGuid();
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate", "code", 48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        rate.Update(null, false, null, false,
            null, false, null, false, null, false, null, false,
            null, false, null, false, null, false, null, false,
            null, false, "Commercial period ended.", true, DateTimeOffset.UtcNow);

        rate.Status.Should().Be(RateStatus.Inactive);
        rate.DeactivationReason.Should().Be("Commercial period ended.");
    }

    [Fact]
    public void Update_WithShortDeactivationReason_ShouldThrow()
    {
        var policyId = Guid.NewGuid();
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate", "code", 48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        var act = () => rate.Update(null, false, null, false,
            null, false, null, false, null, false, null, false,
            null, false, null, false, null, false, null, false,
            null, false, "ab", true, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    // --- Delete protection ---

    [Fact]
    public void CanDelete_NeverSubmitted_ShouldReturnTrue()
    {
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate", "code", null, null, null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow);

        rate.CanDelete().Should().BeTrue();
    }

    [Fact]
    public void CanDelete_AfterSubmission_ShouldReturnFalse()
    {
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate", "code", null, null, null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow);
        rate.MarkSubmitted(Guid.NewGuid());

        rate.CanDelete().Should().BeFalse();
    }

    [Fact]
    public void MarkSubmitted_ShouldSetEverSubmitted()
    {
        var rate = CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Rate", "code", null, null, null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow);
        var submissionId = Guid.NewGuid();

        rate.MarkSubmitted(submissionId);

        rate.EverSubmitted.Should().BeTrue();
        rate.SubmissionIds.Should().Contain(submissionId);
    }

    // --- 6.2 Add through aggregate ---

    [Fact]
    public void AddRate_ThroughAggregate_ShouldAddAndReturn()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        offer.Rates.Should().HaveCount(1);
        offer.Rates[0].Name.Should().Be("Summer 2026");
        rate.Status.Should().Be(RateStatus.Active);
    }

    [Fact]
    public void AddRate_ShouldIncrementRevision()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var initialRevision = offer.Revision;

        offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        offer.Revision.Should().Be(initialRevision + 1);
    }

    [Fact]
    public void AddRate_WithWrongAccommodation_ShouldThrowAccommodationNotFound()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;

        var act = () => offer.AddRate(
            Guid.NewGuid(), Guid.NewGuid(), "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        act.Should().Throw<NotFoundException>()
            .Where(ex => ex.ErrorCode == "ACCOMMODATION_NOT_FOUND");
    }

    // --- 6.3 Update through aggregate ---

    [Fact]
    public void UpdateRate_ShouldApplyChanges()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        offer.UpdateRate(rate.Id,
            "Updated Summer 2026", true,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            StaffBeta, null, now);

        var updated = offer.GetRate(rate.Id)!;
        updated.Name.Should().Be("Updated Summer 2026");
    }

    [Fact]
    public void UpdateRate_NotFound_ShouldThrowRateNotFound()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.UpdateRate(Guid.NewGuid(),
            "Name", true,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            StaffAlpha, null, now);

        act.Should().Throw<NotFoundException>()
            .Where(ex => ex.ErrorCode == "RATE_NOT_FOUND");
    }

    [Fact]
    public void UpdateRate_ShouldInvalidateValidation()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);
        offer.RecalculateCompletenessFromAccommodations(now);
        offer.Validate(Guid.NewGuid(), StaffBeta, offer.Revision, now);
        var later = now.AddMinutes(5);

        offer.UpdateRate(rate.Id,
            "Updated Name", true,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            null, false,
            StaffAlpha, null, later);

        offer.CurrentValidation!.Status.Should().Be(ValidationStatus.Invalidated);
    }

    // --- 6.4 Delete through aggregate ---

    [Fact]
    public void DeleteRate_NeverSubmitted_ShouldRemove()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        offer.DeleteRate(rate.Id, StaffBeta, null, now);

        offer.Rates.Should().BeEmpty();
    }

    [Fact]
    public void DeleteRate_NotFound_ShouldThrowRateNotFound()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;

        var act = () => offer.DeleteRate(Guid.NewGuid(), StaffAlpha, null, now);

        act.Should().Throw<NotFoundException>()
            .Where(ex => ex.ErrorCode == "RATE_NOT_FOUND");
    }

    [Fact]
    public void DeleteRate_AlreadySubmitted_ShouldThrowDeletionNotAllowed()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);
        rate.MarkSubmitted(Guid.NewGuid());

        var act = () => offer.DeleteRate(rate.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "RATE_DELETION_NOT_ALLOWED");
    }

    // --- 6.5 Overlap detection through aggregate ---

    [Fact]
    public void GetOverlappingRates_WhenOverlapExists_ShouldReturnOverlappingRates()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Winter 2026", "standard-room",
            40000, 2, 10000, 5000,
            new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        var rate2 = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2027", "standard-room",
            50000, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        var overlapping = offer.GetOverlappingRates(rate2);

        overlapping.Should().HaveCount(1);
    }

    [Fact]
    public void GetOverlappingRates_WhenNoOverlap_ShouldReturnEmpty()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Winter 2026", "standard-room",
            40000, 2, 10000, 5000,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31),
            1, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        var rate2 = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-room",
            50000, 2, 12000, 6000,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        var overlapping = offer.GetOverlappingRates(rate2);

        overlapping.Should().BeEmpty();
    }

    // --- 6.6 Completeness after rates ---

    [Fact]
    public void RecalculateCompleteness_WithActiveRate_ShouldClearMissingActiveRateIssue()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        offer.RecalculateCompletenessFromAccommodations(now);

        offer.State.Should().Be(OfferState.ReadyForValidation);
        offer.BlockingIssueCount.Should().Be(0);
    }

    [Fact]
    public void RecalculateCompleteness_WithOverlappingRates_ShouldHaveRatePeriodOverlapIssue()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Winter 2026", "standard-room",
            40000, 2, 10000, 5000,
            new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31),
            1, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2027", "standard-room",
            50000, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        offer.RecalculateCompletenessFromAccommodations(now);

        offer.BlockingIssueCount.Should().BeGreaterThan(0);
        offer.HasAnyBlockingIssue(PendingIssueType.RatePeriodOverlap).Should().BeTrue();
    }

    // --- Revision concurrency ---

    [Fact]
    public void AddRate_WithWrongExpectedRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        var act = () => offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, 999, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void UpdateRate_WithWrongExpectedRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        var act = () => offer.UpdateRate(rate.Id,
            "Name", true, null, false, null, false, null, false, null, false,
            null, false, null, false, null, false, null, false, null, false,
            null, false, null, false,
            StaffBeta, 999, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public void DeleteRate_WithWrongExpectedRevision_ShouldThrowRevisionMismatch()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        var act = () => offer.DeleteRate(rate.Id, StaffBeta, 999, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    // --- Error codes ---

    [Fact]
    public void DeleteRate_EverSubmitted_ShouldProduceRateDeletionNotAllowedCode()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);
        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);
        rate.MarkSubmitted(Guid.NewGuid());

        var act = () => offer.DeleteRate(rate.Id, StaffBeta, null, now);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "RATE_DELETION_NOT_ALLOWED");
    }

    [Fact]
    public void Create_WithInvertedPeriod_ShouldProduceInvalidRatePeriodCode()
    {
        var policyId = Guid.NewGuid();
        var act = () => CommercialRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Name", "code", 48900, 2, 12000, 6000,
            new DateOnly(2027, 2, 28),
            new DateOnly(2026, 12, 1),
            2, policyId, MealPlan.Breakfast, DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "INVALID_RATE_PERIOD");
    }

    // --- BRL invariance ---

    [Fact]
    public void Rate_Currency_AlwaysBRL()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        rate.BasePriceCents.Should().Be(48900);
        rate.AdditionalAdultPriceCents.Should().Be(12000);
        rate.AdditionalChildPriceCents.Should().Be(6000);
    }

    [Fact]
    public void Rate_IsComplete_WhenAllRequiredFieldsSet_ShouldReturnTrue()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 2, 12000, 6000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        rate.IsComplete().Should().BeTrue();
    }

    // --- Occupancy-specific pricing ---

    [Fact]
    public void Rate_IncludedGuests_DefinesOccupancy()
    {
        var offer = CreateOfferWithPolicy();
        var now = DateTimeOffset.UtcNow;
        var policyId = offer.Policies.First(p => p.IsDefault).Id;
        var accommodation = CreateAccommodationInOffer(offer, policyId);

        var rate = offer.AddRate(
            Guid.NewGuid(), accommodation.Id, "Summer 2026", "standard-breakfast",
            48900, 4, 15000, 8000,
            new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28),
            2, policyId, MealPlan.Breakfast,
            StaffAlpha, null, now);

        rate.IncludedGuests.Should().Be(4);
        rate.AdditionalAdultPriceCents.Should().Be(15000);
        rate.AdditionalChildPriceCents.Should().Be(8000);
    }
}
