using LocalizeStay.Modules.Discovery.Infrastructure;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.Discovery;

/// <summary>
/// Composition-root entry point for the Discovery module. Scaffolded and ready to receive its first
/// capability; no business rules are invented here (architecture baseline: guardrails against
/// premature coupling and speculative behavior).
/// </summary>
public sealed class DiscoveryModule : IModule
{
    public string Name => "Discovery";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<DiscoveryDbContext>(configuration, DiscoveryDbContext.SchemaName);
        services.AddModuleHandlers(typeof(DiscoveryModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No public endpoints yet — this module is scaffolded and awaits its first capability.
    }
}
