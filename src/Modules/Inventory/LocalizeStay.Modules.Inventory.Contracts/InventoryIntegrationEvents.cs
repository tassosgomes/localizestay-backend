using LocalizeStay.SharedKernel.Events;

namespace LocalizeStay.Contracts.Inventory;

/// <summary>Version 1 of the fact that a property is formally ready for Curation.</summary>
public sealed record InventoryPropertyOnboardedV1 : IntegrationEvent
{
    public const string EventType = "oferta-inventario.propriedade-incorporada";

    public required Guid OnboardingId { get; init; }
    public required Guid PartnerId { get; init; }
    public required string DestinationId { get; init; }
    public required string ContractRepositoryReference { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
}
