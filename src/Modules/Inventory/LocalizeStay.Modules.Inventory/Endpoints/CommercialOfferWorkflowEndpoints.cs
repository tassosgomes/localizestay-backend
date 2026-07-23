using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class CommercialOfferWorkflowEndpoints
{
    public static void MapCommercialOfferWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var validations = endpoints.MapGroup("/api/v1/properties/{propertyId:guid}/commercial-offer-validations").WithTags("Offer Workflow");
        validations.MapPost(string.Empty, ValidateAsync)
            .WithName("createCommercialOfferValidation")
            .WithContractResponses<OfferValidationResponse>(201, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Review);

        var submissions = endpoints.MapGroup("/api/v1/properties/{propertyId:guid}/commercial-offer-submissions").WithTags("Offer Workflow");
        submissions.MapPost(string.Empty, SubmitAsync)
            .WithName("createCommercialOfferSubmission")
            .WithContractResponses<OfferSubmissionResponse>(201, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);

        var history = endpoints.MapGroup("/api/v1/properties/{propertyId:guid}/commercial-offer-history").WithTags("Offer Workflow");
        history.MapGet(string.Empty, HistoryAsync)
            .WithName("listCommercialOfferHistory")
            .WithContractResponses<OfferHistoryListResponse>(200, 400, 401, 403, 404, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Read);
    }

    private static async Task<IResult> ValidateAsync(
        Guid propertyId, CreateOfferValidationEndpointRequest request, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var validationId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(
            new ValidateCommercialOfferCommand(
                propertyId,
                validationId,
                Actor(user),
                request.ExpectedRevision,
                request.Comment),
            cancellationToken);
        return Results.Created(
            $"/api/v1/properties/{propertyId}/commercial-offer-validations/{validationId}", response);
    }

    private static async Task<IResult> SubmitAsync(
        Guid propertyId, CreateOfferSubmissionEndpointRequest request, ClaimsPrincipal user,
        IDispatcher dispatcher, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(httpContext.Request.Headers["Idempotency-Key"], out var idempotencyKey))
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "Idempotency-Key",
                    "The Idempotency-Key header must be a valid UUID.")
                {
                    ErrorCode = "INVALID_IDEMPOTENCY_KEY",
                },
            ]);
        }

        var submissionId = idempotencyKey;
        var response = await dispatcher.SendAsync(
            new SubmitCommercialOfferCommand(
                propertyId,
                submissionId,
                request.ValidationId,
                Actor(user),
                request.ExpectedRevision),
            cancellationToken);
        return Results.Created(
            $"/api/v1/properties/{propertyId}/commercial-offer-submissions/{submissionId}", response);
    }

    private static Task<OfferHistoryListResponse> HistoryAsync(
        Guid propertyId, int _page, int _size, string? eventType,
        IDispatcher dispatcher, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new ListCommercialOfferHistoryQuery(
                propertyId, _page == 0 ? 1 : _page, _size == 0 ? 20 : _size, eventType),
            cancellationToken);

    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Authenticated subject is required.");

    internal sealed record CreateOfferValidationEndpointRequest(int ExpectedRevision, string? Comment);
    internal sealed record CreateOfferSubmissionEndpointRequest(int ExpectedRevision, Guid ValidationId);
}
