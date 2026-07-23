namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class OfferSubmission
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal int Revision { get; private set; }
    internal string SnapshotJson { get; private set; } = string.Empty;
    internal string SubmittedBy { get; private set; } = string.Empty;
    internal DateTimeOffset SubmittedAt { get; private set; }

    private OfferSubmission()
    {
    }

    internal static OfferSubmission Create(
        Guid id,
        Guid propertyId,
        int revision,
        string snapshotJson,
        string submittedBy,
        DateTimeOffset submittedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);

        return new OfferSubmission
        {
            Id = id,
            PropertyId = propertyId,
            Revision = revision,
            SnapshotJson = snapshotJson,
            SubmittedBy = submittedBy.Trim(),
            SubmittedAt = submittedAt.ToUniversalTime(),
        };
    }
}
