namespace LocalizeStay.SharedKernel.Events;

/// <summary>
/// Publishes integration events to their in-process subscribers. No broker is introduced until a
/// measured need justifies one (ADR-0002); the outbox processor is what makes publication reliable
/// across process restarts, not this bus.
/// </summary>
public interface IEventBus
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IIntegrationEvent;
}
