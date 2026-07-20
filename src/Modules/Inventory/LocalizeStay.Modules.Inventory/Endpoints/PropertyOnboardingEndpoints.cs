using System.Security.Claims;
using System.Text.Json;
using LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class PropertyOnboardingEndpoints
{
    public static void MapPropertyOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var onboardings = endpoints.MapGroup("/api/v1/property-onboardings").WithTags("Property Onboardings");
        onboardings.MapGet(string.Empty, ListAsync).WithName("listPropertyOnboardings").WithContractResponses<PropertyOnboardingListResponse>(200, 400, 401, 403, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Read);
        onboardings.MapPost(string.Empty, CreateAsync).WithName("createPropertyOnboarding").WithContractResponses<PropertyOnboardingResponse>(201, 400, 401, 403, 404, 409, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Write);
        onboardings.MapGet("/{onboardingId:guid}", GetAsync).WithName("getPropertyOnboarding").WithContractResponses<PropertyOnboardingResponse>(200, 400, 401, 403, 404, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Read);
        onboardings.MapPatch("/{onboardingId:guid}", UpdateAsync).WithName("updatePropertyOnboarding").WithContractResponses<PropertyOnboardingResponse>(200, 400, 401, 403, 404, 409, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Write);
        onboardings.MapPost("/{onboardingId:guid}/submit-to-curation", SubmitAsync).WithName("submitPropertyOnboardingToCuration").WithContractResponses<SubmissionResultResponse>(200, 400, 401, 403, 404, 409, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Submit);
        onboardings.MapPost("/{onboardingId:guid}/curation-returns", ReturnAsync).WithName("createCurationReturn").WithContractResponses<CurationReturnResultResponse>(201, 400, 401, 403, 404, 409, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Write);
        onboardings.MapPost("/{onboardingId:guid}/close", CloseAsync).WithName("closePropertyOnboarding").WithContractResponses<PropertyOnboardingResponse>(200, 400, 401, 403, 404, 409, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Close);
    }

    private static Task<PropertyOnboardingListResponse> ListAsync(int _page, int _size, Guid? partnerId, string? destinationId, string? lifecycleStatus, string? readinessStatus, string? pendingOwnerType, bool? overdue, string? sort, string? order, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.QueryAsync(new ListPropertyOnboardingsQuery(_page == 0 ? 1 : _page, _size == 0 ? 20 : _size, partnerId, destinationId, lifecycleStatus, readinessStatus, pendingOwnerType, overdue, sort, order), cancellationToken);
    private static async Task<IResult> CreateAsync(CreatePropertyOnboardingRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(new CreatePropertyOnboardingCommand(request.PartnerId, request.PreselectionId, request.Property, Actor(user)), cancellationToken);
        return Results.Created($"/api/v1/property-onboardings/{response.Id}", response);
    }
    private static Task<PropertyOnboardingResponse> GetAsync(Guid onboardingId, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.QueryAsync(new GetPropertyOnboardingQuery(onboardingId), cancellationToken);
    private static async Task<PropertyOnboardingResponse> UpdateAsync(Guid onboardingId, JsonElement request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var update = request.Deserialize<UpdatePropertyOnboardingRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new JsonException("Request body is required.");
        return await dispatcher.SendAsync(new UpdatePropertyOnboardingCommand(onboardingId, update.Name, update.Address, Actor(user)), cancellationToken);
    }
    private static Task<SubmissionResultResponse> SubmitAsync(Guid onboardingId, SubmitToCurationRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.SendAsync(new SubmitToCurationCommand(onboardingId, request.IdempotencyKey, request.DecisionNote, Actor(user)), cancellationToken);
    private static async Task<IResult> ReturnAsync(Guid onboardingId, CreateCurationReturnRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new CreateCurationReturnCommand(onboardingId, request.IdempotencyKey, request.CurationReference, request.ReasonCode, request.Reason, request.Issues, Actor(user)), cancellationToken);
        return Results.Created($"/api/v1/property-onboardings/{onboardingId}/curation-returns/{result.CurationReturn.Id}", result);
    }
    private static Task<PropertyOnboardingResponse> CloseAsync(Guid onboardingId, ClosePropertyOnboardingRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.SendAsync(new ClosePropertyOnboardingCommand(onboardingId, request.ReasonCode, request.Reason, Actor(user)), cancellationToken);
    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Authenticated subject is required.");
}

internal sealed record CreatePropertyOnboardingRequest(Guid PartnerId, string PreselectionId, PropertyInput Property);
internal sealed record UpdatePropertyOnboardingRequest(string? Name, AddressInput? Address);
internal sealed record SubmitToCurationRequest(Guid IdempotencyKey, string DecisionNote);
internal sealed record CreateCurationReturnRequest(Guid IdempotencyKey, string? CurationReference, string ReasonCode, string Reason, IReadOnlyList<CurationReturnIssueInput> Issues);
internal sealed record ClosePropertyOnboardingRequest(string ReasonCode, string Reason);
