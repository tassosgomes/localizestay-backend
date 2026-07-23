using System.Collections.ObjectModel;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class CommercialRate
{
    internal Guid Id { get; private set; }
    internal Guid AccommodationId { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal string Name { get; private set; } = string.Empty;
    internal string ConditionCode { get; private set; } = string.Empty;
    internal long? BasePriceCents { get; private set; }
    internal int? IncludedGuests { get; private set; }
    internal long? AdditionalAdultPriceCents { get; private set; }
    internal long? AdditionalChildPriceCents { get; private set; }
    internal DateOnly? ValidFrom { get; private set; }
    internal DateOnly? ValidTo { get; private set; }
    internal int? MinimumNights { get; private set; }
    internal Guid? PolicyId { get; private set; }
    internal MealPlan? MealPlan { get; private set; }
    internal RateStatus Status { get; private set; }
    internal string? DeactivationReason { get; private set; }
    internal bool EverSubmitted { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Guid> _submissionIds = [];

    internal IReadOnlyList<Guid> SubmissionIds => _submissionIds.AsReadOnly();

    private CommercialRate()
    {
    }

    internal static CommercialRate Create(
        Guid id,
        Guid accommodationId,
        Guid propertyId,
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
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length is < 2 or > 120)
            throw new ArgumentException("Name must be between 2 and 120 characters.", nameof(name));

        ArgumentException.ThrowIfNullOrWhiteSpace(conditionCode);
        if (conditionCode.Length > 60)
            throw new ArgumentException("ConditionCode must be at most 60 characters.", nameof(conditionCode));

        if (basePriceCents.HasValue && basePriceCents.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(basePriceCents), "Base price must not be negative.");

        if (includedGuests is < 1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(includedGuests), "Included guests must be between 1 and 30.");

        if (additionalAdultPriceCents.HasValue && additionalAdultPriceCents.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(additionalAdultPriceCents), "Additional adult price must not be negative.");

        if (additionalChildPriceCents.HasValue && additionalChildPriceCents.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(additionalChildPriceCents), "Additional child price must not be negative.");

        if (validFrom.HasValue && validTo.HasValue && validTo.Value < validFrom.Value)
            throw new BusinessRuleViolationException(
                "ValidTo must not be earlier than ValidFrom.",
                "INVALID_RATE_PERIOD");

        if (minimumNights is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(minimumNights), "Minimum nights must be between 1 and 365.");

        var isActivation = basePriceCents.HasValue
            && includedGuests.HasValue
            && validFrom.HasValue
            && validTo.HasValue
            && minimumNights.HasValue
            && policyId.HasValue
            && mealPlan.HasValue;

        var utcNow = now.ToUniversalTime();

        return new CommercialRate
        {
            Id = id,
            AccommodationId = accommodationId,
            PropertyId = propertyId,
            Name = name.Trim(),
            ConditionCode = conditionCode.Trim(),
            BasePriceCents = basePriceCents,
            IncludedGuests = includedGuests,
            AdditionalAdultPriceCents = additionalAdultPriceCents,
            AdditionalChildPriceCents = additionalChildPriceCents,
            ValidFrom = validFrom,
            ValidTo = validTo,
            MinimumNights = minimumNights,
            PolicyId = policyId,
            MealPlan = mealPlan,
            Status = isActivation ? RateStatus.Active : RateStatus.Draft,
            EverSubmitted = false,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    internal void Update(
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
        DateTimeOffset now)
    {
        if (hasName && name is not null)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 120)
                throw new ArgumentException("Name must be between 2 and 120 characters.", nameof(name));
            Name = name.Trim();
        }

        if (hasConditionCode && conditionCode is not null)
        {
            if (string.IsNullOrWhiteSpace(conditionCode) || conditionCode.Length > 60)
                throw new ArgumentException("ConditionCode must be at most 60 characters.", nameof(conditionCode));
            ConditionCode = conditionCode.Trim();
        }

        if (hasBasePriceCents && (!basePriceCents.HasValue || basePriceCents.Value < 0))
            throw new ArgumentOutOfRangeException(nameof(basePriceCents), "Base price must not be negative.");

        if (hasBasePriceCents)
            BasePriceCents = basePriceCents;

        if (hasIncludedGuests && (includedGuests is < 1 or > 30))
            throw new ArgumentOutOfRangeException(nameof(includedGuests), "Included guests must be between 1 and 30.");

        if (hasIncludedGuests)
            IncludedGuests = includedGuests;

        if (hasAdditionalAdultPriceCents && additionalAdultPriceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(additionalAdultPriceCents), "Additional adult price must not be negative.");

        if (hasAdditionalAdultPriceCents)
            AdditionalAdultPriceCents = additionalAdultPriceCents;

        if (hasAdditionalChildPriceCents && additionalChildPriceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(additionalChildPriceCents), "Additional child price must not be negative.");

        if (hasAdditionalChildPriceCents)
            AdditionalChildPriceCents = additionalChildPriceCents;

        if (hasValidFrom)
            ValidFrom = validFrom;

        if (hasValidTo)
            ValidTo = validTo;

        if (ValidFrom.HasValue && ValidTo.HasValue && ValidTo.Value < ValidFrom.Value)
            throw new BusinessRuleViolationException(
                "ValidTo must not be earlier than ValidFrom.",
                "INVALID_RATE_PERIOD");

        if (hasMinimumNights && (minimumNights is < 1 or > 365))
            throw new ArgumentOutOfRangeException(nameof(minimumNights), "Minimum nights must be between 1 and 365.");

        if (hasMinimumNights)
            MinimumNights = minimumNights;

        if (hasPolicyId)
            PolicyId = policyId;

        if (hasMealPlan)
        {
            MealPlan = mealPlan is not null
                ? Enum.Parse<MealPlan>(mealPlan, true)
                : null;
        }

        if (hasDeactivationReason)
        {
            if (!string.IsNullOrWhiteSpace(deactivationReason))
            {
                Deactivate(deactivationReason);
            }
        }

        UpdateTimestamp(now);
        ReEvaluateStatus();
    }

    internal bool CanDelete()
    {
        return !EverSubmitted;
    }

    internal void MarkSubmitted(Guid submissionId)
    {
        EverSubmitted = true;
        _submissionIds.Add(submissionId);
    }

    internal bool IsComplete()
    {
        return !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(ConditionCode)
            && BasePriceCents.HasValue
            && IncludedGuests.HasValue
            && ValidFrom.HasValue
            && ValidTo.HasValue
            && MinimumNights.HasValue
            && PolicyId.HasValue
            && MealPlan.HasValue;
    }

    internal bool IsActiveOn(DateOnly date)
    {
        if (Status != RateStatus.Active)
            return false;

        if (!ValidFrom.HasValue || !ValidTo.HasValue)
            return false;

        return date >= ValidFrom.Value && date <= ValidTo.Value;
    }

    internal bool OverlapsWith(CommercialRate other)
    {
        if (Status != RateStatus.Active)
            return false;

        if (other.Status != RateStatus.Active)
            return false;

        if (!ValidFrom.HasValue || !ValidTo.HasValue)
            return false;

        if (!other.ValidFrom.HasValue || !other.ValidTo.HasValue)
            return false;

        var overlap = ValidFrom.Value <= other.ValidTo.Value
            && other.ValidFrom.Value <= ValidTo.Value;

        if (!overlap)
            return false;

        return ConditionCode == other.ConditionCode
            && PolicyId == other.PolicyId
            && MealPlan == other.MealPlan;
    }

    internal void UpdateTimestamp(DateTimeOffset now)
    {
        UpdatedAt = now.ToUniversalTime();
    }

    private void Deactivate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length < 3 || reason.Trim().Length > 500)
            throw new ArgumentException("Deactivation reason must be between 3 and 500 characters.", nameof(reason));

        if (Status == RateStatus.Inactive)
            throw new BusinessRuleViolationException(
                "Rate is already inactive.",
                "RATE_ALREADY_INACTIVE");

        Status = RateStatus.Inactive;
        DeactivationReason = reason.Trim();
    }

    private void ReEvaluateStatus()
    {
        if (Status == RateStatus.Inactive)
            return;

        Status = IsComplete() ? RateStatus.Active : RateStatus.Draft;
    }
}
