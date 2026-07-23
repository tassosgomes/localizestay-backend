using System.Security.Claims;
using System.Text.Json;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class AccommodationEndpoints
{
    public static void MapAccommodationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accommodations = endpoints.MapGroup("/api/v1/properties/{propertyId:guid}/accommodations").WithTags("Accommodations");
        accommodations.MapGet(string.Empty, ListAsync)
            .WithName("listAccommodations")
            .WithContractResponses<AccommodationListResponse>(200, 400, 401, 403, 404, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Read);
        accommodations.MapPost(string.Empty, CreateAsync)
            .WithName("createAccommodation")
            .WithContractResponses<AccommodationResponse>(201, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
        accommodations.MapGet("/{accommodationId:guid}", GetAsync)
            .WithName("getAccommodation")
            .WithContractResponses<AccommodationDto>(200, 400, 401, 403, 404, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Read);
        accommodations.MapPatch("/{accommodationId:guid}", UpdateAsync)
            .WithName("updateAccommodation")
            .WithContractResponses<AccommodationResponse>(200, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
        accommodations.MapDelete("/{accommodationId:guid}", DeleteAsync)
            .WithName("deleteAccommodation")
            .WithContractResponses<AccommodationResponse>(204, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
    }

    private static Task<AccommodationListResponse> ListAsync(
        Guid propertyId, int _page, int _size, string? status, string? completeness,
        string? sort, string? order, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new ListAccommodationsQuery(
                propertyId, _page == 0 ? 1 : _page, _size == 0 ? 20 : _size,
                status, completeness, sort, order),
            cancellationToken);

    private static async Task<IResult> CreateAsync(
        Guid propertyId, CreateAccommodationEndpointRequest request, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var childRange = request.ChildAgeRange is not null
            ? new ChildAgeRangeInput(request.ChildAgeRange.MinAgeInclusive, request.ChildAgeRange.MaxAgeInclusive)
            : null;

        var bedConfig = request.BedConfiguration?.Select(b =>
            new BedEntryInput(b.Type, b.Quantity)).ToList();

        var command = new CreateAccommodationCommand(
            propertyId,
            request.CommercialName,
            request.MaxAdults,
            request.MaxChildren,
            request.TotalCapacity,
            bedConfig,
            request.MealPlan,
            childRange,
            request.StructuralFeatures,
            request.PolicyId,
            null,
            Actor(user));
        var response = await dispatcher.SendAsync(command, cancellationToken);
        return Results.Created(
            $"/api/v1/properties/{propertyId}/accommodations/{response.Id}", response);
    }

    private static Task<AccommodationDto> GetAsync(
        Guid propertyId, Guid accommodationId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetAccommodationQuery(propertyId, accommodationId), cancellationToken);

    private static async Task<AccommodationResponse> UpdateAsync(
        Guid propertyId, Guid accommodationId, JsonElement request, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var hasCommercialName = request.TryGetProperty("commercialName", out var commercialNameEl);
        var hasMaxAdults = request.TryGetProperty("maxAdults", out var maxAdultsEl);
        var hasMaxChildren = request.TryGetProperty("maxChildren", out var maxChildrenEl);
        var hasTotalCapacity = request.TryGetProperty("totalCapacity", out var totalCapacityEl);
        var hasMealPlan = request.TryGetProperty("mealPlan", out var mealPlanEl);
        var hasBedConfiguration = request.TryGetProperty("bedConfiguration", out var bedConfigEl);
        var hasStructuralFeatures = request.TryGetProperty("structuralFeatures", out var structuralEl);
        var hasPolicyId = request.TryGetProperty("policyId", out var policyIdEl);

        var hasChildAgeRange = request.TryGetProperty("childAgeRange", out var childAgeRangeEl);
        ChildAgeRangeUpdateInput? childAgeRange = null;
        if (hasChildAgeRange)
        {
            if (childAgeRangeEl.ValueKind == JsonValueKind.Null)
                childAgeRange = new ChildAgeRangeUpdateInput(null, null, true);
            else
                childAgeRange = new ChildAgeRangeUpdateInput(
                    childAgeRangeEl.TryGetProperty("minAgeInclusive", out var min) && min.ValueKind != JsonValueKind.Null ? min.GetInt32() : 0,
                    childAgeRangeEl.TryGetProperty("maxAgeInclusive", out var max) && max.ValueKind != JsonValueKind.Null ? max.GetInt32() : 17,
                    false);
        }

        var expectedRevision = request.TryGetProperty("expectedRevision", out var rev)
            ? rev.GetInt32()
            : (int?)null;

        List<BedEntryInput>? bedConfig = null;
        if (hasBedConfiguration && bedConfigEl.ValueKind != JsonValueKind.Null)
            bedConfig = bedConfigEl.EnumerateArray()
                .Select(b => new BedEntryInput(
                    b.GetProperty("type").GetString()!,
                    b.GetProperty("quantity").GetInt32()))
                .ToList();

        List<string>? structuralFeatures = null;
        if (hasStructuralFeatures && structuralEl.ValueKind != JsonValueKind.Null)
            structuralFeatures = structuralEl.EnumerateArray().Select(s => s.GetString()!).ToList();

        var command = new UpdateAccommodationCommand(
            propertyId,
            accommodationId,
            hasCommercialName && commercialNameEl.ValueKind != JsonValueKind.Null ? commercialNameEl.GetString() : null,
            hasCommercialName,
            hasMaxAdults && maxAdultsEl.ValueKind != JsonValueKind.Null ? maxAdultsEl.GetInt32() : null,
            hasMaxAdults,
            hasMaxChildren && maxChildrenEl.ValueKind != JsonValueKind.Null ? maxChildrenEl.GetInt32() : null,
            hasMaxChildren,
            hasTotalCapacity && totalCapacityEl.ValueKind != JsonValueKind.Null ? totalCapacityEl.GetInt32() : null,
            hasTotalCapacity,
            hasMealPlan && mealPlanEl.ValueKind != JsonValueKind.Null ? mealPlanEl.GetString() : null,
            hasMealPlan,
            bedConfig,
            hasBedConfiguration,
            structuralFeatures,
            hasStructuralFeatures,
            hasPolicyId && policyIdEl.ValueKind != JsonValueKind.Null ? policyIdEl.GetGuid() : null,
            hasPolicyId,
            childAgeRange,
            expectedRevision,
            Actor(user));

        return await dispatcher.SendAsync(command, cancellationToken);
    }

    private static async Task<IResult> DeleteAsync(
        Guid propertyId, Guid accommodationId, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(
            new DeleteAccommodationCommand(propertyId, accommodationId, null, Actor(user)),
            cancellationToken);
        return Results.NoContent();
    }

    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Authenticated subject is required.");

    internal sealed record BedConfigItem(string Type, int Quantity);
    internal sealed record ChildAgeRangeBody(int MinAgeInclusive, int MaxAgeInclusive);
    internal sealed record CreateAccommodationEndpointRequest(
        string CommercialName,
        int? MaxAdults,
        int? MaxChildren,
        int? TotalCapacity,
        List<BedConfigItem>? BedConfiguration,
        string? MealPlan,
        ChildAgeRangeBody? ChildAgeRange,
        List<string>? StructuralFeatures,
        Guid? PolicyId);
}
