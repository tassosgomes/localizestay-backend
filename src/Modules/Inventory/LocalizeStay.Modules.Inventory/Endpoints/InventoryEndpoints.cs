using LocalizeStay.Modules.Inventory.Application.Queries;
using LocalizeStay.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

/// <summary>
/// Minimal API surface for the Inventory module. Only a trivial status endpoint exists today, to
/// prove the module-to-host wiring; real capabilities are added as the module's use cases are built.
/// </summary>
internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/inventory/status", GetModuleStatusAsync)
            .WithName("GetInventoryModuleStatus")
            .WithTags("Inventory")
            .Produces<InventoryModuleStatusResponse>();

        endpoints.MapPartnerEndpoints();
        endpoints.MapPropertyOnboardingEndpoints();
        endpoints.MapPropertyOnboardingSubresourceEndpoints();
        endpoints.MapPropertyOnboardingReadEndpoints();
    }

    private static async Task<InventoryModuleStatusResponse> GetModuleStatusAsync(
        IDispatcher dispatcher, CancellationToken cancellationToken) =>
        await dispatcher.QueryAsync(new GetInventoryModuleStatusQuery(), cancellationToken);
}
