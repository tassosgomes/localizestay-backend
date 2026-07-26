using System.Collections.ObjectModel;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class Accommodation
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal string CommercialName { get; private set; } = string.Empty;
    internal AccommodationStatus Status { get; private set; }
    internal bool EverSubmitted { get; private set; }
    internal string? DeactivationReason { get; private set; }

    internal int? MaxAdults { get; private set; }
    internal int? MaxChildren { get; private set; }
    internal int? TotalCapacity { get; private set; }

    internal MealPlan? MealPlan { get; private set; }

    internal ChildAgeRangeSource ChildAgeRangeSource { get; private set; }
    internal int? ChildMinimumAge { get; private set; }
    internal int? ChildMaximumAge { get; private set; }

    internal Guid? PolicyId { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<BedEntry> _bedConfiguration = [];
    private readonly List<string> _structuralFeatures = [];
    private readonly List<Guid> _submissionIds = [];

    internal IReadOnlyList<BedEntry> BedConfiguration => _bedConfiguration.AsReadOnly();
    internal IReadOnlyList<string> StructuralFeatures => _structuralFeatures.AsReadOnly();
    internal IReadOnlyList<Guid> SubmissionIds => _submissionIds.AsReadOnly();

    private Accommodation()
    {
    }

    internal static Accommodation Create(
        Guid id,
        Guid propertyId,
        string commercialName,
        Guid? defaultPolicyId,
        ChildAgeRange? propertyDefaultChildAgeRange,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commercialName);
        if (commercialName.Length is < 2 or > 180)
            throw new ArgumentException("CommercialName must be between 2 and 180 characters.", nameof(commercialName));

        var utcNow = now.ToUniversalTime();

        var accommodation = new Accommodation
        {
            Id = id,
            PropertyId = propertyId,
            CommercialName = commercialName.Trim(),
            Status = AccommodationStatus.Active,
            EverSubmitted = false,
            PolicyId = defaultPolicyId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        if (propertyDefaultChildAgeRange is not null)
        {
            accommodation.ChildAgeRangeSource = ChildAgeRangeSource.PropertyDefault;
            accommodation.ChildMinimumAge = propertyDefaultChildAgeRange.MinimumAge;
            accommodation.ChildMaximumAge = propertyDefaultChildAgeRange.MaximumAge;
        }
        else
        {
            accommodation.ChildAgeRangeSource = ChildAgeRangeSource.None;
        }

        return accommodation;
    }

    internal void UpdateCommercialName(string commercialName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commercialName);
        if (commercialName.Length is < 2 or > 180)
            throw new ArgumentException("CommercialName must be between 2 and 180 characters.", nameof(commercialName));

        CommercialName = commercialName.Trim();
    }

    internal void SetOccupancy(int? maxAdults, int? maxChildren, int? totalCapacity)
    {
        MaxAdults = maxAdults;
        MaxChildren = maxChildren;
        TotalCapacity = totalCapacity;

        ValidateOccupancy();
    }

    internal void SetBedConfiguration(IReadOnlyList<BedEntry> bedConfiguration)
    {
        _bedConfiguration.Clear();
        if (bedConfiguration is { Count: > 0 })
        {
            _bedConfiguration.AddRange(bedConfiguration);
        }

        ValidateCapacityMatchesBeds();
    }

    internal void SetMealPlan(MealPlan? mealPlan)
    {
        MealPlan = mealPlan;
    }

    internal void SetPolicy(Guid? policyId)
    {
        PolicyId = policyId;
    }

    internal void SetChildAgeRangeOverride(ChildAgeRange? childAgeRange)
    {
        if (childAgeRange is null)
        {
            ChildAgeRangeSource = ChildAgeRangeSource.None;
            ChildMinimumAge = null;
            ChildMaximumAge = null;
        }
        else
        {
            ChildAgeRangeSource = ChildAgeRangeSource.AccommodationOverride;
            ChildMinimumAge = childAgeRange.MinimumAge;
            ChildMaximumAge = childAgeRange.MaximumAge;
        }
    }

    internal void RevertChildAgeRangeToPropertyDefault(ChildAgeRange? propertyDefaultChildAgeRange)
    {
        if (propertyDefaultChildAgeRange is not null)
        {
            ChildAgeRangeSource = ChildAgeRangeSource.PropertyDefault;
            ChildMinimumAge = propertyDefaultChildAgeRange.MinimumAge;
            ChildMaximumAge = propertyDefaultChildAgeRange.MaximumAge;
        }
        else
        {
            ChildAgeRangeSource = ChildAgeRangeSource.None;
            ChildMinimumAge = null;
            ChildMaximumAge = null;
        }
    }

    internal void SetStructuralFeatures(IReadOnlyList<string> features)
    {
        _structuralFeatures.Clear();
        if (features is { Count: > 0 })
        {
            _structuralFeatures.AddRange(features);
        }
    }

    internal void Deactivate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 1000)
            throw new ArgumentException("Deactivation reason must be at most 1000 characters.", nameof(reason));

        if (Status != AccommodationStatus.Active)
            throw new BusinessRuleViolationException(
                "Only active accommodations can be deactivated.",
                "ACCOMMODATION_ALREADY_INACTIVE");

        Status = AccommodationStatus.Inactive;
        DeactivationReason = reason.Trim();
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

    internal void UpdateTimestamp(DateTimeOffset now)
    {
        UpdatedAt = now.ToUniversalTime();
    }

    internal bool IsCommerciallyComplete()
    {
        return !string.IsNullOrWhiteSpace(CommercialName)
               && MaxAdults.HasValue
               && TotalCapacity.HasValue
               && MealPlan.HasValue
               && PolicyId.HasValue
               && IsOccupancyValid();
    }

    private bool IsOccupancyValid()
    {
        if (MaxAdults is null || TotalCapacity is null)
            return false;

        var children = MaxChildren ?? 0;
        return MaxAdults.Value + children <= TotalCapacity.Value;
    }

    private void ValidateOccupancy()
    {
        if (MaxAdults is null || TotalCapacity is null)
            return;

        var children = MaxChildren ?? 0;
        if (MaxAdults.Value + children > TotalCapacity.Value)
        {
            throw new BusinessRuleViolationException(
                $"The sum of maxAdults ({MaxAdults}) and maxChildren ({children}) exceeds totalCapacity ({TotalCapacity}).",
                "INVALID_OCCUPANCY_CONFIGURATION");
        }
    }

    private void ValidateCapacityMatchesBeds()
    {
        var bedCapacity = _bedConfiguration.Sum(b => b.Count);
        if (bedCapacity > 0 && TotalCapacity.HasValue && bedCapacity < TotalCapacity.Value)
        {
            throw new BusinessRuleViolationException(
                $"Bed configuration total capacity ({bedCapacity}) is less than accommodation total capacity ({TotalCapacity}).",
                "INVALID_OCCUPANCY_CONFIGURATION");
        }
    }
}
