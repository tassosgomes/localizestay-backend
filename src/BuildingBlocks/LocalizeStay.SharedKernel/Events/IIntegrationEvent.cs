namespace LocalizeStay.SharedKernel.Events;

/// <summary>
/// A fact that already happened, published by its owning module for other modules to react to
/// asynchronously. Integration events cross module boundaries; they carry stable, versioned data —
/// never entities, repositories or other internal state (architecture baseline: Communication
/// Patterns — Comunicação assíncrona).
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredOnUtc { get; }

    /// <summary>Correlates this event with the request or process that originated it.</summary>
    string CorrelationId { get; }

    /// <summary>Identifier of the message or command that caused this event, if any.</summary>
    string? CausationId { get; }

    /// <summary>Schema version of this event's payload. Incompatible changes require a new version.</summary>
    int Version { get; }
}
