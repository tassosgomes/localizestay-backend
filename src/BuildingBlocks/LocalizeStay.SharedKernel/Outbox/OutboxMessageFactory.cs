using System.Text.Json;
using LocalizeStay.SharedKernel.Events;

namespace LocalizeStay.SharedKernel.Outbox;

/// <summary>
/// Builds an <see cref="OutboxMessage"/> from an integration event, to be added to the module's own
/// <see cref="IHasOutbox.OutboxMessages"/> set inside the same transaction as the business change
/// that produced it.
/// </summary>
public static class OutboxMessageFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static OutboxMessage FromIntegrationEvent<TEvent>(TEvent integrationEvent) where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();
        var assemblyQualifiedName = eventType.AssemblyQualifiedName
            ?? throw new InvalidOperationException($"Integration event type '{eventType}' has no assembly-qualified name.");
        var content = JsonSerializer.Serialize(integrationEvent, eventType, SerializerOptions);

        return OutboxMessage.Create(assemblyQualifiedName, content, integrationEvent.CorrelationId, integrationEvent.OccurredOnUtc);
    }
}
