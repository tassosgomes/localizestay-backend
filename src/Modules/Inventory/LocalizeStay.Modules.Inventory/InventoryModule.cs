using LocalizeStay.Modules.Inventory.Endpoints;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.Inventory;

/// <summary>
/// Composition-root entry point for the Inventory module. Maps a single trivial status endpoint to
/// prove the module-to-host wiring (dispatcher, DI, minimal API); no inventory business rule is
/// implemented yet (architecture baseline: guardrails against speculative behavior).
/// </summary>
public sealed class InventoryModule : IModule
{
    public string Name => "Inventory";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<InventoryDbContext>(configuration, InventoryDbContext.SchemaName);
        services.AddModuleHandlers(typeof(InventoryModule).Assembly);
        // BusinessAuditWriter<InventoryDbContext> tracks entries on this module's own DbContext
        // without committing, so mutations and their audit rows share a single SaveChangesAsync
        // (ADR-003: ownership da auditoria por módulo).
        services.AddBusinessAuditWriter<InventoryDbContext>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapInventoryEndpoints();
    }
}
