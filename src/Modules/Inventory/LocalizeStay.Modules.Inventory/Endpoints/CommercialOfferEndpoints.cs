using System.Security.Claims;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class CommercialOfferEndpoints
{
    public static void MapCommercialOfferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var offers = endpoints.MapGroup("/api/v1/commercial-offers").WithTags("Commercial Offers");
        offers.MapGet(string.Empty, ListAsync)
            .WithName("listCommercialOffers")
            .WithContractResponses<CommercialOfferListResponse>(200, 400, 401, 403, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Read);

        var propertyOffer = endpoints.MapGroup("/api/v1/properties/{propertyId:guid}/commercial-offer").WithTags("Commercial Offers");
        propertyOffer.MapGet(string.Empty, GetAsync)
            .WithName("getCommercialOffer")
            .WithContractResponses<CommercialOfferDetailDto>(200, 400, 401, 403, 404, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Read);
    }

    private static Task<CommercialOfferListResponse> ListAsync(
        int _page, int _size, Guid? propertyId, string? status, bool? hasBlockingIssues,
        bool? overdue, string? sort, string? order, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new ListCommercialOffersQuery(
                _page == 0 ? 1 : _page, _size == 0 ? 20 : _size,
                propertyId, status, hasBlockingIssues, overdue, sort, order),
            cancellationToken);

    private static Task<CommercialOfferDetailDto> GetAsync(
        Guid propertyId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetCommercialOfferQuery(propertyId), cancellationToken);
}
