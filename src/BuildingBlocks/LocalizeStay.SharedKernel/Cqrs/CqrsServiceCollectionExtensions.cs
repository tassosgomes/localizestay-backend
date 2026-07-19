using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.Cqrs;

public static class CqrsServiceCollectionExtensions
{
    /// <summary>Registers the native <see cref="IDispatcher"/>. Called once by the host composition root.</summary>
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }
}
