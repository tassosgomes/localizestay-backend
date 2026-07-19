using System.Diagnostics.Metrics;
using System.Text.Json;
using LocalizeStay.SharedKernel.Events;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.SharedKernel.Outbox;

/// <summary>
/// Polls a module's outbox table and publishes pending messages to the in-process event bus, with
/// bounded retry. One instance runs per module <see cref="DbContext"/>, keeping ownership scoped to
/// the module's own schema (ADR-0002).
/// </summary>
public sealed class OutboxProcessor<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<OutboxProcessor<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext, IHasOutbox
{
    private static readonly Meter _meter = new("LocalizeStay.Outbox");
    private static readonly Counter<long> _retryExhausted = _meter.CreateCounter<long>("outbox.retry.exhausted", unit: "{message}");
    private static readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private const int MaxRetryAttempts = 5;
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await TryProcessPendingMessagesAsync(stoppingToken);

            if (!await DelayUntilNextPollAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task TryProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ProcessPendingMessagesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Outbox processor for {DbContext} failed while processing a batch.", typeof(TDbContext).Name);
        }
    }

    private async Task<bool> DelayUntilNextPollAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_pollingInterval, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedOnUtc == null && message.RetryCount < MaxRetryAttempts)
            .OrderBy(message => message.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            await PublishMessageAsync(dbContext, eventBus, message, cancellationToken);
        }
    }

    private async Task PublishMessageAsync(TDbContext dbContext, IEventBus eventBus, OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var integrationEvent = DeserializeEvent(message);
            await PublishAsync(eventBus, integrationEvent, cancellationToken);
            message.MarkProcessed(clock.UtcNow);
        }
        catch (Exception exception)
        {
            message.RegisterFailure(exception.Message);
            logger.LogWarning(
                exception,
                "Failed to publish outbox message {MessageId} (attempt {RetryCount}) for {DbContext}.",
                message.Id,
                message.RetryCount,
                typeof(TDbContext).Name);
            if (message.RetryCount >= MaxRetryAttempts)
            {
                _retryExhausted.Add(1, new KeyValuePair<string, object?>("module", typeof(TDbContext).Name));
                logger.LogError(
                    "Outbox retry alert: message {MessageId} reached {RetryCount} attempts for {DbContext}.",
                    message.Id,
                    message.RetryCount,
                    typeof(TDbContext).Name);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IIntegrationEvent DeserializeEvent(OutboxMessage message)
    {
        var eventType = Type.GetType(message.Type)
            ?? throw new InvalidOperationException($"Outbox message type '{message.Type}' could not be resolved.");

        return (IIntegrationEvent)(JsonSerializer.Deserialize(message.Content, eventType)
            ?? throw new InvalidOperationException($"Outbox message {message.Id} deserialized to null."));
    }

    private static Task PublishAsync(IEventBus eventBus, IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var publishMethod = typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))!
            .MakeGenericMethod(integrationEvent.GetType());

        return (Task)publishMethod.Invoke(eventBus, [integrationEvent, cancellationToken])!;
    }
}
