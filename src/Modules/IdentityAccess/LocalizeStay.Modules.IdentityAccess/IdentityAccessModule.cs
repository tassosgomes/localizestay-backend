using LocalizeStay.Modules.IdentityAccess.Infrastructure;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.IdentityAccess;

/// <summary>
/// Composition-root entry point for the IdentityAccess module. Scaffolded and ready to receive its first
/// capability; no business rules are invented here (architecture baseline: guardrails against
/// premature coupling and speculative behavior).
/// </summary>
public sealed class IdentityAccessModule : IModule
{
    public string Name => "IdentityAccess";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<IdentityAccessDbContext>(configuration, IdentityAccessDbContext.SchemaName);
        services.AddModuleHandlers(typeof(IdentityAccessModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No public endpoints yet — this module is scaffolded and awaits its first capability.
    }
}
