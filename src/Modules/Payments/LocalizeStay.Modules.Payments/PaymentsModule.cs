using LocalizeStay.Modules.Payments.Infrastructure;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.Payments;

/// <summary>
/// Composition-root entry point for the Payments module. Scaffolded and ready to receive its first
/// capability; no business rules are invented here (architecture baseline: guardrails against
/// premature coupling and speculative behavior).
/// </summary>
public sealed class PaymentsModule : IModule
{
    public string Name => "Payments";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<PaymentsDbContext>(configuration, PaymentsDbContext.SchemaName);
        services.AddModuleHandlers(typeof(PaymentsModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No public endpoints yet — this module is scaffolded and awaits its first capability.
    }
}
