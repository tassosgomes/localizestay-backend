using LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class PropertyOnboardingReadEndpoints
{
    public static void MapPropertyOnboardingReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/property-onboardings")
            .MapGet("/{onboardingId:guid}/history", HistoryAsync).WithName("listPropertyOnboardingHistory").WithContractResponses<HistoryListResponse>(200, 400, 401, 403, 404, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Read);
        endpoints.MapGroup("/api/v1/property-onboarding-metrics")
            .MapGet(string.Empty, MetricsAsync).WithName("getPropertyOnboardingMetrics").WithContractResponses<PropertyOnboardingMetricsResponse>(200, 400, 401, 403, 404, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Metrics);
    }

    private static Task<HistoryListResponse> HistoryAsync(Guid onboardingId, int _page, int _size, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.QueryAsync(new ListPropertyOnboardingHistoryQuery(onboardingId, _page == 0 ? 1 : _page, _size == 0 ? 20 : _size), cancellationToken);
    private static Task<PropertyOnboardingMetricsResponse> MetricsAsync(DateTimeOffset from, DateTimeOffset to, string? destinationId, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.QueryAsync(new GetPropertyOnboardingMetricsQuery(from, to, destinationId), cancellationToken);
}
