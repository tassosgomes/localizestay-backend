namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class OfferValidation
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal int Revision { get; private set; }
    internal string ValidatedBy { get; private set; } = string.Empty;
    internal DateTimeOffset ValidatedAt { get; private set; }
    internal ValidationStatus Status { get; private set; }

    private OfferValidation()
    {
    }

    internal static OfferValidation Create(
        Guid id,
        Guid propertyId,
        int revision,
        string validatedBy,
        DateTimeOffset validatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(validatedBy);

        return new OfferValidation
        {
            Id = id,
            PropertyId = propertyId,
            Revision = revision,
            ValidatedBy = validatedBy.Trim(),
            ValidatedAt = validatedAt.ToUniversalTime(),
            Status = ValidationStatus.Valid,
        };
    }

    internal void Invalidate(DateTimeOffset invalidatedAt)
    {
        if (Status != ValidationStatus.Valid)
            return;

        Status = ValidationStatus.Invalidated;
    }
}
