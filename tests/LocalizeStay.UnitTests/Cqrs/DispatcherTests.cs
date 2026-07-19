using AwesomeAssertions;
using LocalizeStay.SharedKernel.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.UnitTests.Cqrs;

public class DispatcherTests
{
    [Fact]
    public async Task SendAsync_should_resolve_registered_handler_and_return_its_result()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<CreateGreetingCommand, string>, CreateGreetingCommandHandler>();
        services.AddScoped<IDispatcher, Dispatcher>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new CreateGreetingCommand("Tasso"), CancellationToken.None);

        result.Should().Be("Hello, Tasso!");
    }

    [Fact]
    public async Task QueryAsync_should_resolve_registered_handler_and_return_its_result()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryHandler<CountLettersQuery, int>, CountLettersQueryHandler>();
        services.AddScoped<IDispatcher, Dispatcher>();
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.QueryAsync(new CountLettersQuery("Tasso"), CancellationToken.None);

        result.Should().Be(5);
    }

    [Fact]
    public async Task SendAsync_should_throw_when_no_handler_is_registered()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        var act = async () => await dispatcher.SendAsync(new CreateGreetingCommand("Tasso"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed record CreateGreetingCommand(string Name) : ICommand<string>;

    private sealed class CreateGreetingCommandHandler : ICommandHandler<CreateGreetingCommand, string>
    {
        public Task<string> HandleAsync(CreateGreetingCommand command, CancellationToken cancellationToken) =>
            Task.FromResult($"Hello, {command.Name}!");
    }

    private sealed record CountLettersQuery(string Text) : IQuery<int>;

    private sealed class CountLettersQueryHandler : IQueryHandler<CountLettersQuery, int>
    {
        public Task<int> HandleAsync(CountLettersQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(query.Text.Length);
    }
}
