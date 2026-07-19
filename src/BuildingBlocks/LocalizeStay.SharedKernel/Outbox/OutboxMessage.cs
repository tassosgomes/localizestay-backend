namespace LocalizeStay.SharedKernel.Outbox;

/// <summary>
/// Transactional outbox row. Written in the same database transaction as the business change that
/// produced the event, guaranteeing at-least-once publication even if the process crashes right
/// after commit (ADR-0002: outbox transacional no PostgreSQL, sem broker). Every module owns its own
/// outbox table in its own schema.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    /// <summary>Assembly-qualified name of the integration event type, used to deserialize <see cref="Content"/>.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>JSON payload of the integration event.</summary>
    public string Content { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public DateTimeOffset? ProcessedOnUtc { get; private set; }

    public int RetryCount { get; private set; }

    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(string type, string content, string correlationId, DateTimeOffset occurredOnUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Content = content,
            CorrelationId = correlationId,
            OccurredOnUtc = occurredOnUtc,
        };

    public void MarkProcessed(DateTimeOffset processedOnUtc) => ProcessedOnUtc = processedOnUtc;

    public void RegisterFailure(string error)
    {
        RetryCount++;
        LastError = error;
    }
}
