namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class OfferReturn
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal Guid SubmissionId { get; private set; }
    internal int Revision { get; private set; }
    internal string ReasonCode { get; private set; } = string.Empty;
    internal string Reason { get; private set; } = string.Empty;
    internal string ReturnedBy { get; private set; } = string.Empty;
    internal DateTimeOffset ReturnedAt { get; private set; }

    private OfferReturn()
    {
    }

    internal static OfferReturn Create(
        Guid id,
        Guid propertyId,
        Guid submissionId,
        int revision,
        string reasonCode,
        string reason,
        string returnedBy,
        DateTimeOffset returnedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnedBy);

        return new OfferReturn
        {
            Id = id,
            PropertyId = propertyId,
            SubmissionId = submissionId,
            Revision = revision,
            ReasonCode = reasonCode,
            Reason = reason.Trim(),
            ReturnedBy = returnedBy.Trim(),
            ReturnedAt = returnedAt.ToUniversalTime(),
        };
    }
}
