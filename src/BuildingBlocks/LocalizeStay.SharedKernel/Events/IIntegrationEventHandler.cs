namespace LocalizeStay.SharedKernel.Events;

/// <summary>
/// Handles a single integration event. Delivery is at-least-once, so implementations must be
/// idempotent and tolerate reordering (architecture baseline: Communication Patterns).
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
