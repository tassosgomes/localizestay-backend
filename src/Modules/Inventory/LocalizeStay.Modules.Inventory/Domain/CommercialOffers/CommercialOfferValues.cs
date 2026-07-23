namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal enum OfferState
{
    Draft,
    ReadyForValidation,
    Validated,
    Submitted,
    Returned,
    Published,
}

internal enum ValidationStatus
{
    Valid,
    Invalidated,
}

internal enum PendingIssueType
{
    MissingPolicy,
    IncompleteAccommodation,
    MissingActiveRate,
    OccupancyIncoherent,
    RatePeriodOverlap,
    ValidationRequired,
    PublishedOfferNotModifiable,
}

internal enum PolicyStatus
{
    Active,
    Inactive,
}

internal enum ChildAgeRangeSource
{
    None,
    PropertyDefault,
    AccommodationOverride,
}

internal enum BedType
{
    Single,
    Double,
    Queen,
    King,
    BunkBed,
    SofaBed,
    ExtraMattress,
}

internal enum MealPlan
{
    None,
    Breakfast,
    HalfBoard,
    FullBoard,
}

internal sealed record ChildAgeRange(int MinimumAge, int MaximumAge)
{
    internal static ChildAgeRange Create(int minimumAge, int maximumAge)
    {
        if (minimumAge < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumAge), "Minimum age must be non-negative.");

        if (maximumAge < minimumAge)
            throw new ArgumentOutOfRangeException(nameof(maximumAge), "Maximum age must not be less than minimum age.");

        if (maximumAge > 17)
            throw new ArgumentOutOfRangeException(nameof(maximumAge), "Child maximum age must not exceed 17.");

        return new ChildAgeRange(minimumAge, maximumAge);
    }
}

internal sealed record BedEntry(BedType Type, int Count)
{
    internal static BedEntry Create(BedType type, int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Bed count must be positive.");

        return new BedEntry(type, count);
    }
}

internal enum AccommodationStatus
{
    Active,
    Inactive,
}

internal sealed record MoneyInCents(long Cents)
{
    internal static MoneyInCents FromBRL(decimal value) => new((long)(value * 100m));

    internal decimal ToBRL() => Cents / 100m;
}
