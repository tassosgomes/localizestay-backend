namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class CommercialOfferIdempotencyKey
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal Guid Key { get; private set; }
    internal string Scope { get; private set; } = string.Empty;
    internal string? PayloadFingerprint { get; private set; }
    internal Guid? ResultReferenceId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }

    private CommercialOfferIdempotencyKey()
    {
    }

    internal static CommercialOfferIdempotencyKey Create(
        Guid propertyId,
        Guid key,
        string scope,
        DateTimeOffset createdAt,
        string? payloadFingerprint = null,
        Guid? resultReferenceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        return new CommercialOfferIdempotencyKey
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            Key = key,
            Scope = scope,
            PayloadFingerprint = payloadFingerprint,
            ResultReferenceId = resultReferenceId,
            CreatedAt = createdAt.ToUniversalTime(),
        };
    }
}
