using LocalizeStay.SharedKernel.Events;

namespace LocalizeStay.Contracts.Curation;

public sealed record CurationOfferReturnedV1 : IntegrationEvent
{
    public const string EventType = "curadoria.oferta-devolvida";

    public required Guid PropertyId { get; init; }
    public required Guid SubmissionId { get; init; }
    public required int Revision { get; init; }
    public required string ReasonCode { get; init; }
    public required string Reason { get; init; }
    public required string ReturnedBy { get; init; }
    public required DateTimeOffset ReturnedAt { get; init; }
}
