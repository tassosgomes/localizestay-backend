using LocalizeStay.Modules.Operations.Infrastructure;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.Operations;

/// <summary>
/// Composition-root entry point for the Operations module. Scaffolded and ready to receive its first
/// capability; no business rules are invented here (architecture baseline: guardrails against
/// premature coupling and speculative behavior).
/// </summary>
public sealed class OperationsModule : IModule
{
    public string Name => "Operations";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<OperationsDbContext>(configuration, OperationsDbContext.SchemaName);
        services.AddModuleHandlers(typeof(OperationsModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No public endpoints yet — this module is scaffolded and awaits its first capability.
    }
}
