using AwesomeAssertions;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.UnitTests.DependencyInjection;

public class ModuleRegistrationExtensionsTests
{
    [Fact]
    public async Task AddModuleHandlers_should_register_query_handlers_resolvable_by_the_dispatcher()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddModuleHandlers(typeof(ModuleRegistrationExtensionsTests).Assembly);
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.QueryAsync(new PingQuery(), CancellationToken.None);

        result.Should().Be("pong");
    }

    internal sealed record PingQuery : IQuery<string>;

    internal sealed class PingQueryHandler : IQueryHandler<PingQuery, string>
    {
        public Task<string> HandleAsync(PingQuery query, CancellationToken cancellationToken) => Task.FromResult("pong");
    }
}
