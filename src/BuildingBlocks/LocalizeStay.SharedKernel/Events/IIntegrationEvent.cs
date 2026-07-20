namespace LocalizeStay.SharedKernel.Events;

/// <summary>
/// A fact that already happened, published by its owning module for other modules to react to
/// asynchronously. Integration events cross module boundaries; they carry stable, versioned data —
/// never entities, repositories or other internal state (architecture baseline: Communication
/// Patterns — Comunicação assíncrona).
/// </summary>
public interface IIntegrationEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredOnUtc { get; }

    /// <summary>Correlates this event with the request or process that originated it.</summary>
    public string CorrelationId { get; }

    /// <summary>Identifier of the message or command that caused this event, if any.</summary>
    public string? CausationId { get; }

    /// <summary>Schema version of this event's payload. Incompatible changes require a new version.</summary>
    public int Version { get; }
}
