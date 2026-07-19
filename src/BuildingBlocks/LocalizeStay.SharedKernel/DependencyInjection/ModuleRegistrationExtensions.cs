using System.Reflection;
using FluentValidation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Events;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

namespace LocalizeStay.SharedKernel.DependencyInjection;

/// <summary>
/// Scans a single module assembly for its own CQRS command/query handlers, integration event
/// handlers and FluentValidation validators, and registers them with a scoped lifetime. Each module
/// calls this once, from its own <c>IModule.RegisterServices</c>, with its own assembly — never a
/// foreign one — so handler discovery cannot cross module boundaries.
/// </summary>
public static class ModuleRegistrationExtensions
{
    public static IServiceCollection AddModuleHandlers(this IServiceCollection services, Assembly moduleAssembly)
    {
        ArgumentNullException.ThrowIfNull(moduleAssembly);

        // publicOnly: false — handlers stay internal by design (architecture baseline: only Contracts
        // is public), so the scan must not skip them the way Scrutor's default filter would.
        services.Scan(selector => selector.FromAssemblies(moduleAssembly)
            .AddClasses(
                classes => classes.AssignableToAny(
                    typeof(ICommandHandler<,>),
                    typeof(IQueryHandler<,>),
                    typeof(IIntegrationEventHandler<>)),
                publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssembly(moduleAssembly, lifetime: ServiceLifetime.Scoped, includeInternalTypes: true);

        return services;
    }
}
