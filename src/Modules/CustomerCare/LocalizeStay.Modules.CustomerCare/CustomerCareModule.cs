using LocalizeStay.Modules.CustomerCare.Infrastructure;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.CustomerCare;

/// <summary>
/// Composition-root entry point for the CustomerCare module. Scaffolded and ready to receive its first
/// capability; no business rules are invented here (architecture baseline: guardrails against
/// premature coupling and speculative behavior).
/// </summary>
public sealed class CustomerCareModule : IModule
{
    public string Name => "CustomerCare";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<CustomerCareDbContext>(configuration, CustomerCareDbContext.SchemaName);
        services.AddModuleHandlers(typeof(CustomerCareModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No public endpoints yet — this module is scaffolded and awaits its first capability.
    }
}
