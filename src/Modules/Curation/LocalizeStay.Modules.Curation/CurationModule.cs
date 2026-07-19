using LocalizeStay.Modules.Curation.Infrastructure;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.Curation;

/// <summary>
/// Composition-root entry point for the Curation module. Scaffolded and ready to receive its first
/// capability; no business rules are invented here (architecture baseline: guardrails against
/// premature coupling and speculative behavior).
/// </summary>
public sealed class CurationModule : IModule
{
    public string Name => "Curation";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<CurationDbContext>(configuration, CurationDbContext.SchemaName);
        services.AddModuleHandlers(typeof(CurationModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No public endpoints yet — this module is scaffolded and awaits its first capability.
    }
}
