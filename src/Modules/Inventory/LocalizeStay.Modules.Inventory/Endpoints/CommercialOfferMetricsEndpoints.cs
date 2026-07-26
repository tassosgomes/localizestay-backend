using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class CommercialOfferMetricsEndpoints
{
    public static void MapCommercialOfferMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var metrics = endpoints.MapGroup("/api/v1/commercial-offer-metrics").WithTags("Metrics");
        metrics.MapGet(string.Empty, GetAsync)
            .WithName("getCommercialOfferMetrics")
            .WithContractResponses<CommercialOfferMetricsResponse>(200, 400, 401, 403, 404, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Metrics);
    }

    private static Task<CommercialOfferMetricsResponse> GetAsync(
        string from, string to, string? destinationId,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var fromDate = ParseDateTime(from, "from");
        var toDate = ParseDateTime(to, "to");
        return dispatcher.QueryAsync(
            new GetCommercialOfferMetricsQuery(fromDate, toDate, destinationId),
            cancellationToken);
    }

    private static DateTimeOffset ParseDateTime(string value, string field)
    {
        var queryDecodedValue = value.Replace(' ', '+');

        if (DateTimeOffset.TryParse(
            queryDecodedValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            return parsed;
        }

        throw new ValidationException(
        [
            new ValidationFailure(field, $"{field} must use a valid RFC 3339 date-time format.")
            {
                ErrorCode = "INVALID_DATE_TIME_FORMAT",
            },
        ]);
    }
}
