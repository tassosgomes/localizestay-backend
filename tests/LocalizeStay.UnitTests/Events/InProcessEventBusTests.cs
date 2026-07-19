using AwesomeAssertions;
using LocalizeStay.SharedKernel.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace LocalizeStay.UnitTests.Events;

public class InProcessEventBusTests
{
    [Fact]
    public async Task PublishAsync_should_invoke_every_registered_handler()
    {
        var handlerMock = new Mock<IIntegrationEventHandler<SampleIntegrationEvent>>();
        handlerMock
            .Setup(handler => handler.HandleAsync(It.IsAny<SampleIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(handlerMock.Object);
        await using var provider = services.BuildServiceProvider();

        var bus = new InProcessEventBus(provider, Mock.Of<ILogger<InProcessEventBus>>());
        var integrationEvent = new SampleIntegrationEvent { CorrelationId = "corr-1" };

        await bus.PublishAsync(integrationEvent, CancellationToken.None);

        handlerMock.Verify(handler => handler.HandleAsync(integrationEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_should_not_throw_when_a_handler_fails()
    {
        var failingHandlerMock = new Mock<IIntegrationEventHandler<SampleIntegrationEvent>>();
        failingHandlerMock
            .Setup(handler => handler.HandleAsync(It.IsAny<SampleIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var services = new ServiceCollection();
        services.AddSingleton(failingHandlerMock.Object);
        await using var provider = services.BuildServiceProvider();

        var bus = new InProcessEventBus(provider, Mock.Of<ILogger<InProcessEventBus>>());
        var integrationEvent = new SampleIntegrationEvent { CorrelationId = "corr-2" };

        var act = async () => await bus.PublishAsync(integrationEvent, CancellationToken.None);

        await act.Should().NotThrowAsync(
            "a failing handler must be logged, not bubbled up: consumers are idempotent and the outbox retries the event.");
    }

    // Public (not private) so Moq/Castle can generate a proxy for IIntegrationEventHandler<SampleIntegrationEvent>.
    public sealed record SampleIntegrationEvent : IntegrationEvent;
}
