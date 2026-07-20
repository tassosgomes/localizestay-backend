using System.Security.Claims;
using System.Text.Json;
using LocalizeStay.Modules.Inventory.Application.Partners;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class PartnerEndpoints
{
    public static void MapPartnerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var partners = endpoints.MapGroup("/api/v1/partners").WithTags("Partners");
        partners.MapGet(string.Empty, ListAsync).WithName("listPartners").WithContractResponses<PartnerListResponse>(200, 400, 401, 403, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Read);
        partners.MapPost(string.Empty, CreateAsync).WithName("createPartner").WithContractResponses<PartnerResponse>(201, 400, 401, 403, 409, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Write);
        partners.MapGet("/{partnerId:guid}", GetAsync).WithName("getPartner").WithContractResponses<PartnerResponse>(200, 400, 401, 403, 404, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Read);
        partners.MapPatch("/{partnerId:guid}", UpdateAsync).WithName("updatePartner").WithContractResponses<PartnerResponse>(200, 400, 401, 403, 404, 409, 422, 429, 500).RequireAuthorization(PortfolioOnboardingPermissions.Write);
    }

    private static Task<PartnerListResponse> ListAsync(int _page, int _size, string? search, string? legalIdentifierType, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.QueryAsync(new ListPartnersQuery(_page == 0 ? 1 : _page, _size == 0 ? 20 : _size, search, legalIdentifierType), cancellationToken);

    private static async Task<IResult> CreateAsync(CreatePartnerRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(new CreatePartnerCommand(request.PreselectionId, request.LegalName, request.TradeName, request.LegalIdentifier, request.PrimaryContact, Actor(user)), cancellationToken);
        return Results.Created($"/api/v1/partners/{response.Id}", response);
    }

    private static Task<PartnerResponse> GetAsync(Guid partnerId, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.QueryAsync(new GetPartnerQuery(partnerId), cancellationToken);

    private static async Task<PartnerResponse> UpdateAsync(Guid partnerId, JsonElement request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var update = request.Deserialize<UpdatePartnerRequest>(options) ?? throw new JsonException("Request body is required.");
        var hasTradeName = request.TryGetProperty("tradeName", out _);
        return await dispatcher.SendAsync(new UpdatePartnerCommand(partnerId, update.LegalName, hasTradeName, update.TradeName, update.LegalIdentifier, update.PrimaryContact, Actor(user)), cancellationToken);
    }

    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Authenticated subject is required.");
}

internal sealed record CreatePartnerRequest(string PreselectionId, string LegalName, string? TradeName, LegalIdentifierInput LegalIdentifier, ContactInput PrimaryContact);
internal sealed record UpdatePartnerRequest(string? LegalName, string? TradeName, LegalIdentifierInput? LegalIdentifier, ContactInput? PrimaryContact);
