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
        onboardings.MapGet(string.Empty, ListAsync).RequireAuthorization(PortfolioOnboardingPermissions.Read);
        onboardings.MapPost(string.Empty, CreateAsync).RequireAuthorization(PortfolioOnboardingPermissions.Write);
        onboardings.MapGet("/{onboardingId:guid}", GetAsync).RequireAuthorization(PortfolioOnboardingPermissions.Read);
        onboardings.MapPatch("/{onboardingId:guid}", UpdateAsync).RequireAuthorization(PortfolioOnboardingPermissions.Write);
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
    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Authenticated subject is required.");
}

internal sealed record CreatePropertyOnboardingRequest(Guid PartnerId, string PreselectionId, PropertyInput Property);
internal sealed record UpdatePropertyOnboardingRequest(string? Name, AddressInput? Address);
