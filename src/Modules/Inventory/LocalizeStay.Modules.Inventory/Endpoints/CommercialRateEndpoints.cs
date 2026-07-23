using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class CommercialRateEndpoints
{
    public static void MapCommercialRateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var rates = endpoints.MapGroup("/api/v1/properties/{propertyId:guid}/accommodations/{accommodationId:guid}/rates").WithTags("Rates");
        rates.MapGet(string.Empty, ListAsync)
            .WithName("listCommercialRates")
            .WithContractResponses<CommercialRateListResponse>(200, 400, 401, 403, 404, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Read);
        rates.MapPost(string.Empty, CreateAsync)
            .WithName("createCommercialRate")
            .WithContractResponses<CommercialRateResponse>(201, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
        rates.MapPatch("/{rateId:guid}", UpdateAsync)
            .WithName("updateCommercialRate")
            .WithContractResponses<CommercialRateResponse>(200, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
        rates.MapDelete("/{rateId:guid}", DeleteAsync)
            .WithName("deleteCommercialRate")
            .WithContractResponses<CommercialRateResponse>(204, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
    }

    private static Task<CommercialRateListResponse> ListAsync(
        Guid propertyId, Guid accommodationId, int _page, int _size, string? status,
        string? activeOn, string? validFrom, string? validTo, string? sort, string? order,
        IDispatcher dispatcher, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new ListCommercialRatesQuery(
                propertyId, accommodationId,
                _page == 0 ? 1 : _page, _size == 0 ? 20 : _size,
                status, activeOn, validFrom, validTo, sort, order),
            cancellationToken);

    private static async Task<IResult> CreateAsync(
        Guid propertyId, Guid accommodationId, CreateCommercialRateEndpointRequest request,
        ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var validFrom = ParseDate(request.ValidFrom, "validFrom");
        var validTo = ParseDate(request.ValidTo, "validTo");

        var command = new CreateCommercialRateCommand(
            propertyId,
            accommodationId,
            request.Name,
            request.ConditionCode,
            request.BasePriceCents,
            request.IncludedGuests,
            request.AdditionalAdultPriceCents,
            request.AdditionalChildPriceCents,
            validFrom,
            validTo,
            request.MinimumNights,
            request.PolicyId,
            request.MealPlan,
            null,
            Actor(user));
        var response = await dispatcher.SendAsync(command, cancellationToken);
        return Results.Created(
            $"/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates/{response.Id}", response);
    }

    private static async Task<CommercialRateResponse> UpdateAsync(
        Guid propertyId, Guid accommodationId, Guid rateId, JsonElement request,
        ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var hasName = request.TryGetProperty("name", out var nameEl);
        var hasConditionCode = request.TryGetProperty("conditionCode", out var conditionCodeEl);
        var hasBasePriceCents = request.TryGetProperty("basePriceCents", out var priceEl);
        var hasIncludedGuests = request.TryGetProperty("includedGuests", out var guestsEl);
        var hasAdditionalAdult = request.TryGetProperty("additionalAdultPriceCents", out var adultEl);
        var hasAdditionalChild = request.TryGetProperty("additionalChildPriceCents", out var childEl);
        var hasValidFrom = request.TryGetProperty("validFrom", out var validFromEl);
        var hasValidTo = request.TryGetProperty("validTo", out var validToEl);
        var hasMinimumNights = request.TryGetProperty("minimumNights", out var nightsEl);
        var hasPolicyId = request.TryGetProperty("policyId", out var policyIdEl);
        var hasMealPlan = request.TryGetProperty("mealPlan", out var mealPlanEl);
        var hasDeactivationReason = request.TryGetProperty("deactivationReason", out var reasonEl);

        var expectedRevision = request.TryGetProperty("expectedRevision", out var rev)
            ? rev.GetInt32()
            : (int?)null;

        DateOnly? validFrom = null;
        if (hasValidFrom && validFromEl.ValueKind != JsonValueKind.Null)
            validFrom = ParseDate(validFromEl, "validFrom");

        DateOnly? validTo = null;
        if (hasValidTo && validToEl.ValueKind != JsonValueKind.Null)
            validTo = ParseDate(validToEl, "validTo");

        var command = new UpdateCommercialRateCommand(
            propertyId,
            accommodationId,
            rateId,
            hasName && nameEl.ValueKind != JsonValueKind.Null ? nameEl.GetString() : null,
            hasName,
            hasConditionCode && conditionCodeEl.ValueKind != JsonValueKind.Null ? conditionCodeEl.GetString() : null,
            hasConditionCode,
            hasBasePriceCents && priceEl.ValueKind != JsonValueKind.Null ? priceEl.GetInt64() : null,
            hasBasePriceCents,
            hasIncludedGuests && guestsEl.ValueKind != JsonValueKind.Null ? guestsEl.GetInt32() : null,
            hasIncludedGuests,
            hasAdditionalAdult && adultEl.ValueKind != JsonValueKind.Null ? adultEl.GetInt64() : null,
            hasAdditionalAdult,
            hasAdditionalChild && childEl.ValueKind != JsonValueKind.Null ? childEl.GetInt64() : null,
            hasAdditionalChild,
            validFrom,
            hasValidFrom,
            validTo,
            hasValidTo,
            hasMinimumNights && nightsEl.ValueKind != JsonValueKind.Null ? nightsEl.GetInt32() : null,
            hasMinimumNights,
            hasPolicyId && policyIdEl.ValueKind != JsonValueKind.Null ? policyIdEl.GetGuid() : null,
            hasPolicyId,
            hasMealPlan && mealPlanEl.ValueKind != JsonValueKind.Null ? mealPlanEl.GetString() : null,
            hasMealPlan,
            hasDeactivationReason && reasonEl.ValueKind != JsonValueKind.Null ? reasonEl.GetString() : null,
            hasDeactivationReason,
            expectedRevision,
            Actor(user));

        return await dispatcher.SendAsync(command, cancellationToken);
    }

    private static async Task<IResult> DeleteAsync(
        Guid propertyId, Guid accommodationId, Guid rateId, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(
            new DeleteCommercialRateCommand(propertyId, accommodationId, rateId, null, Actor(user)),
            cancellationToken);
        return Results.NoContent();
    }

    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Authenticated subject is required.");

    private static DateOnly? ParseDate(string? value, string field)
    {
        if (value is null)
            return null;

        if (DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            return parsed;
        }

        throw InvalidDate(field);
    }

    private static DateOnly ParseDate(JsonElement element, string field)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var parsed = ParseDate(element.GetString(), field);
            if (parsed.HasValue)
                return parsed.Value;
        }

        throw InvalidDate(field);
    }

    private static ValidationException InvalidDate(string field) => new(
    [
        new ValidationFailure(field, $"{field} must use the yyyy-MM-dd format.")
        {
            ErrorCode = "INVALID_DATE_FORMAT",
        },
    ]);

    internal sealed record CreateCommercialRateEndpointRequest(
        string Name,
        string ConditionCode,
        long? BasePriceCents,
        int? IncludedGuests,
        long? AdditionalAdultPriceCents,
        long? AdditionalChildPriceCents,
        string? ValidFrom,
        string? ValidTo,
        int? MinimumNights,
        Guid? PolicyId,
        string? MealPlan);
}
