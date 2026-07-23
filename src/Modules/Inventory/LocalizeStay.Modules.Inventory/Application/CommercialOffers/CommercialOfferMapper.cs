using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal static class CommercialOfferMapper
{
    internal static string ContractValue<TEnum>(TEnum value) where TEnum : struct, Enum =>
        char.ToLowerInvariant(value.ToString()[0]) + value.ToString()[1..];

    internal static string ContractValue(PendingIssueType issue) => issue switch
    {
        PendingIssueType.MissingPolicy => "missingPolicy",
        PendingIssueType.IncompleteAccommodation => "incompleteAccommodation",
        PendingIssueType.MissingActiveRate => "missingActiveRate",
        PendingIssueType.OccupancyIncoherent => "occupancyIncoherent",
        PendingIssueType.RatePeriodOverlap => "ratePeriodOverlap",
        PendingIssueType.ValidationRequired => "validationRequired",
        PendingIssueType.PublishedOfferNotModifiable => "publishedOfferNotModifiable",
        _ => issue.ToString(),
    };

    internal static PendingIssueResponse ToPendingIssue(PendingIssueType type) => type switch
    {
        PendingIssueType.MissingPolicy => new("MISSING_POLICY", "A commercial policy is required for the offer.", "blocking", "offer", null, null),
        PendingIssueType.IncompleteAccommodation => new("INCOMPLETE_ACCOMMODATION", "At least one complete accommodation with active rate is required.", "blocking", "offer", null, null),
        PendingIssueType.MissingActiveRate => new("MISSING_ACTIVE_RATE", "At least one active rate is required.", "blocking", "offer", null, null),
        PendingIssueType.OccupancyIncoherent => new("OCCUPANCY_INCOHERENT", "The sum of max adults and max children exceeds total capacity.", "blocking", "accommodation", null, null),
        PendingIssueType.RatePeriodOverlap => new("RATE_PERIOD_OVERLAP", "Rates with the same condition must not have overlapping periods.", "blocking", "rate", null, null),
        PendingIssueType.ValidationRequired => new("VALIDATION_REQUIRED", "A valid validation is required before submission.", "blocking", "offer", null, null),
        PendingIssueType.PublishedOfferNotModifiable => new("PUBLISHED_OFFER_NOT_MODIFIABLE", "Published offers cannot be modified through F02.", "blocking", "offer", null, null),
        _ => new(type.ToString().ToUpperInvariant(), $"Pending issue: {type}.", "warning", "offer", null, null),
    };

    internal static IReadOnlyList<PendingIssueResponse> ToPendingIssues(IReadOnlyList<PendingIssueType> types) =>
        types.Select(ToPendingIssue).ToList();

    internal static int CompletenessPercentage(IReadOnlyList<PendingIssueType> issues)
    {
        if (issues.Count == 0)
            return 100;

        var items = new HashSet<PendingIssueType>(issues);

        if (items.Contains(PendingIssueType.MissingPolicy)
            && items.Contains(PendingIssueType.IncompleteAccommodation)
            && items.Contains(PendingIssueType.MissingActiveRate))
            return 0;

        if (items.Count >= 3)
            return 0;

        if (items.Count == 2)
            return 33;

        if (items.Count == 1)
            return 66;

        return 0;
    }

    internal static int AccommodationCompletenessPercentage(Accommodation accommodation)
    {
        var total = 6;
        var filled = 0;

        if (!string.IsNullOrWhiteSpace(accommodation.CommercialName)) filled++;
        if (accommodation.MaxAdults.HasValue) filled++;
        if (accommodation.TotalCapacity.HasValue) filled++;
        if (accommodation.MealPlan.HasValue) filled++;
        if (accommodation.PolicyId.HasValue) filled++;
        if (accommodation.BedConfiguration.Count > 0) filled++;

        return filled * 100 / total;
    }

    internal static List<PendingIssueResponse> AccommodationPendingIssues(Accommodation accommodation)
    {
        var issues = new List<PendingIssueResponse>();

        if (string.IsNullOrWhiteSpace(accommodation.CommercialName))
            issues.Add(new("NAME_REQUIRED", "Commercial name is required.", "blocking", "accommodation", accommodation.Id.ToString(), "commercialName"));

        if (!accommodation.MaxAdults.HasValue)
            issues.Add(new("MAX_ADULTS_REQUIRED", "Maximum number of adults is required.", "blocking", "accommodation", accommodation.Id.ToString(), "maxAdults"));

        if (!accommodation.TotalCapacity.HasValue)
            issues.Add(new("TOTAL_CAPACITY_REQUIRED", "Total capacity is required.", "blocking", "accommodation", accommodation.Id.ToString(), "totalCapacity"));

        if (!accommodation.MealPlan.HasValue)
            issues.Add(new("MEAL_PLAN_REQUIRED", "Meal plan is required.", "blocking", "accommodation", accommodation.Id.ToString(), "mealPlan"));

        if (!accommodation.PolicyId.HasValue)
            issues.Add(new("POLICY_REQUIRED", "A policy is required for the accommodation.", "blocking", "accommodation", accommodation.Id.ToString(), "policyId"));

        if (accommodation.BedConfiguration.Count == 0)
            issues.Add(new("BED_CONFIGURATION_REQUIRED", "Bed configuration is required.", "blocking", "accommodation", accommodation.Id.ToString(), "bedConfiguration"));

        if (accommodation.MaxAdults.HasValue && accommodation.TotalCapacity.HasValue)
        {
            var children = accommodation.MaxChildren ?? 0;
            if (accommodation.MaxAdults.Value + children > accommodation.TotalCapacity.Value)
                issues.Add(new("OCCUPANCY_EXCEEDS_CAPACITY", "The sum of max adults and max children exceeds total capacity.", "blocking", "accommodation", accommodation.Id.ToString(), "maxAdults"));
        }

        return issues;
    }

    internal static int RateCompletenessPercentage(CommercialRate rate)
    {
        var total = 9;
        var filled = 0;

        if (!string.IsNullOrWhiteSpace(rate.Name)) filled++;
        if (!string.IsNullOrWhiteSpace(rate.ConditionCode)) filled++;
        if (rate.BasePriceCents.HasValue) filled++;
        if (rate.IncludedGuests.HasValue) filled++;
        if (rate.ValidFrom.HasValue) filled++;
        if (rate.ValidTo.HasValue) filled++;
        if (rate.MinimumNights.HasValue) filled++;
        if (rate.PolicyId.HasValue) filled++;
        if (rate.MealPlan.HasValue) filled++;

        return filled * 100 / total;
    }

    internal static List<PendingIssueResponse> RatePendingIssues(CommercialRate rate)
    {
        var issues = new List<PendingIssueResponse>();

        if (string.IsNullOrWhiteSpace(rate.Name))
            issues.Add(new("RATE_NAME_REQUIRED", "Rate name is required.", "blocking", "rate", rate.Id.ToString(), "name"));

        if (string.IsNullOrWhiteSpace(rate.ConditionCode))
            issues.Add(new("CONDITION_CODE_REQUIRED", "Condition code is required.", "blocking", "rate", rate.Id.ToString(), "conditionCode"));

        if (!rate.BasePriceCents.HasValue)
            issues.Add(new("BASE_PRICE_REQUIRED", "Base price is required.", "blocking", "rate", rate.Id.ToString(), "basePriceCents"));

        if (!rate.IncludedGuests.HasValue)
            issues.Add(new("INCLUDED_GUESTS_REQUIRED", "Number of included guests is required.", "blocking", "rate", rate.Id.ToString(), "includedGuests"));

        if (!rate.ValidFrom.HasValue)
            issues.Add(new("VALID_FROM_REQUIRED", "Start date of the validity period is required.", "blocking", "rate", rate.Id.ToString(), "validFrom"));

        if (!rate.ValidTo.HasValue)
            issues.Add(new("VALID_TO_REQUIRED", "End date of the validity period is required.", "blocking", "rate", rate.Id.ToString(), "validTo"));

        if (!rate.MinimumNights.HasValue)
            issues.Add(new("MINIMUM_NIGHTS_REQUIRED", "Minimum number of nights is required.", "blocking", "rate", rate.Id.ToString(), "minimumNights"));

        if (!rate.PolicyId.HasValue)
            issues.Add(new("RATE_POLICY_REQUIRED", "Select a policy for the rate.", "blocking", "rate", rate.Id.ToString(), "policyId"));

        if (!rate.MealPlan.HasValue)
            issues.Add(new("MEAL_PLAN_REQUIRED", "Meal plan is required.", "blocking", "rate", rate.Id.ToString(), "mealPlan"));

        return issues;
    }

    internal static BedConfigurationItemResponse ToBedConfigurationItem(BedEntry entry) =>
        new(ContractValue(entry.Type), entry.Count);

    internal static ChildAgeRangeResponse? ToChildAgeRange(Accommodation accommodation)
    {
        if (!accommodation.ChildMinimumAge.HasValue || !accommodation.ChildMaximumAge.HasValue)
            return null;

        return new ChildAgeRangeResponse(accommodation.ChildMinimumAge.Value, accommodation.ChildMaximumAge.Value);
    }

    internal static StaffActorResponse ToStaffActor(string id, string displayName) => new(id, displayName);

    internal static OfferValidationResponse ToResponse(OfferValidation validation) => new(
        validation.Id,
        validation.PropertyId,
        validation.Revision,
        ContractValue(validation.Status),
        ToStaffActor(validation.ValidatedBy, validation.ValidatedBy),
        validation.ValidatedAt,
        validation.InvalidatedAt,
        validation.InvalidationReason,
        validation.Comment);

    internal static OfferSubmissionResponse ToResponse(OfferSubmission submission) => new(
        submission.Id,
        submission.PropertyId,
        submission.Revision,
        submission.ValidationId,
        "accepted",
        "oferta-inventario.oferta-estruturada",
        ToStaffActor(submission.SubmittedBy, submission.SubmittedBy),
        submission.SubmittedAt);

    internal static CommercialOfferResponse ToResponse(CommercialOffer offer) => new(
        offer.PropertyId,
        offer.Revision,
        offer.RevisionAuthor,
        ContractValue(offer.State),
        offer.AccommodationCount,
        offer.BlockingIssueCount,
        offer.EverSubmitted,
        offer.CompleteInformationReceivedAt,
        offer.TargetSubmissionAt,
        offer.CreatedAt,
        offer.UpdatedAt);
}
