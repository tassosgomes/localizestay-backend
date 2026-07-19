namespace LocalizeStay.SharedKernel.Events;

/// <summary>Base record for integration events. Owning modules derive from this in their Contracts assembly.</summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public virtual int Version => 1;
}
