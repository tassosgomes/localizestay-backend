using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Events;
using LocalizeStay.SharedKernel.Time;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.Modules;

/// <summary>
/// Composition-root helpers that wire every registered <see cref="IModule"/> — and the technical
/// capabilities every module relies on — into the host, without the host knowing any module's
/// internals.
/// </summary>
public static class ModuleHostExtensions
{
    public static IServiceCollection AddLocalizeStayModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        services.AddSingleton(modules);
        services.AddDispatcher();
        services.AddSingleton<CorrelationIdAccessor>();
        services.AddSingleton<ICorrelationIdAccessor>(provider => provider.GetRequiredService<CorrelationIdAccessor>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IEventBus, InProcessEventBus>();

        foreach (var module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapLocalizeStayModules(
        this IEndpointRouteBuilder endpoints,
        IReadOnlyCollection<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
