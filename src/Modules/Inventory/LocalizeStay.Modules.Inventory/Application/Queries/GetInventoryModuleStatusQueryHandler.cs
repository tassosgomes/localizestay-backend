using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Time;

namespace LocalizeStay.Modules.Inventory.Application.Queries;

internal sealed class GetInventoryModuleStatusQueryHandler(IClock clock)
    : IQueryHandler<GetInventoryModuleStatusQuery, InventoryModuleStatusResponse>
{
    public Task<InventoryModuleStatusResponse> HandleAsync(GetInventoryModuleStatusQuery query, CancellationToken cancellationToken)
    {
        var response = new InventoryModuleStatusResponse("Inventory", "healthy", clock.UtcNow);
        return Task.FromResult(response);
    }
}
