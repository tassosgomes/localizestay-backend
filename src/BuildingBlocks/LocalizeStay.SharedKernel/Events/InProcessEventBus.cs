using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.SharedKernel.Events;

/// <summary>
/// Default <see cref="IEventBus"/>: resolves every <see cref="IIntegrationEventHandler{TEvent}"/>
/// registered for the event's type from a fresh DI scope and invokes them. A handler failure is
/// logged and does not stop the remaining handlers — consumers must be idempotent and the outbox
/// processor is what guarantees the event is retried, not this bus (architecture baseline: eventos
/// não substituem validação síncrona; falhas de um consumidor não podem quebrar os demais).
/// </summary>
public sealed class InProcessEventBus(IServiceProvider serviceProvider, ILogger<InProcessEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await using var scope = serviceProvider.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            await InvokeHandlerSafelyAsync(handler, integrationEvent, cancellationToken);
        }
    }

    private async Task InvokeHandlerSafelyAsync<TEvent>(
        IIntegrationEventHandler<TEvent> handler,
        TEvent integrationEvent,
        CancellationToken cancellationToken) where TEvent : IIntegrationEvent
    {
        try
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Integration event handler {HandlerType} failed for event {EventId} ({EventType}). CorrelationId: {CorrelationId}.",
                handler.GetType().Name,
                integrationEvent.EventId,
                typeof(TEvent).Name,
                integrationEvent.CorrelationId);
        }
    }
}
