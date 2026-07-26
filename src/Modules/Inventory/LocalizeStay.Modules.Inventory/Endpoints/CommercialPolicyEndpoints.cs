using System.Security.Claims;
using System.Text.Json;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class CommercialPolicyEndpoints
{
    public static void MapCommercialPolicyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var policies = endpoints.MapGroup("/api/v1/properties/{propertyId:guid}/commercial-policies").WithTags("Commercial Policies");
        policies.MapGet(string.Empty, ListAsync)
            .WithName("listCommercialPolicies")
            .WithContractResponses<CommercialPolicyListResponse>(200, 400, 401, 403, 404, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Read);
        policies.MapPost(string.Empty, CreateAsync)
            .WithName("createCommercialPolicy")
            .WithContractResponses<CommercialPolicyResponse>(201, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
        policies.MapPut("/default", SetDefaultAsync)
            .WithName("setDefaultCommercialPolicy")
            .WithContractResponses<SetDefaultPolicyResult>(200, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
        policies.MapPatch("/{policyId:guid}", UpdateAsync)
            .WithName("updateCommercialPolicy")
            .WithContractResponses<CommercialPolicyResponse>(200, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
        policies.MapDelete("/{policyId:guid}", DeleteAsync)
            .WithName("deleteCommercialPolicy")
            .WithContractResponses<CommercialPolicyResponse>(204, 400, 401, 403, 404, 409, 422, 429, 500)
            .RequireAuthorization(CommercialOfferPermissions.Write);
    }

    private static Task<CommercialPolicyListResponse> ListAsync(
        Guid propertyId, string? status, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new ListCommercialPoliciesQuery(propertyId, status), cancellationToken);

    private static async Task<IResult> CreateAsync(
        Guid propertyId, CreateCommercialPolicyEndpointRequest request, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(
            new CreateCommercialPolicyCommand(propertyId, request.Type, request.SetAsDefault, null, Actor(user)),
            cancellationToken);
        return Results.Created(
            $"/api/v1/properties/{propertyId}/commercial-policies/{response.Id}", response);
    }

    private static async Task<IResult> SetDefaultAsync(
        Guid propertyId, SetDefaultCommercialPolicyEndpointRequest request, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new SetDefaultCommercialPolicyCommand(
                propertyId, request.PolicyId, request.ApplyToExistingAccommodations, request.ExpectedRevision, Actor(user)),
            cancellationToken);
        var policy = await dispatcher.QueryAsync(new ListCommercialPoliciesQuery(propertyId, null), cancellationToken);
        var defaultPolicy = policy.Data.FirstOrDefault(p => p.Id == request.PolicyId)
            ?? throw new InvalidOperationException("Default policy not found after update.");
        var offer = await dispatcher.QueryAsync(new GetCommercialOfferQuery(propertyId), cancellationToken);
        return Results.Ok(new SetDefaultPolicyResult(defaultPolicy, result.UpdatedAccommodationCount, offer.Revision));
    }

    private static async Task<CommercialPolicyResponse> UpdateAsync(
        Guid propertyId, Guid policyId, JsonElement request, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var hasStatus = request.TryGetProperty("status", out var statusEl);
        var isInactive = hasStatus && statusEl.GetString() == "inactive";

        Guid? replacementPolicyId = null;
        if (isInactive && request.TryGetProperty("replacementPolicyId", out var repl))
            replacementPolicyId = repl.ValueKind == JsonValueKind.Null ? null : repl.GetGuid();

        var deactivationReason = request.TryGetProperty("deactivationReason", out var reasonEl)
            && reasonEl.ValueKind != JsonValueKind.Null
            ? reasonEl.GetString()
            : null;

        var expectedRevision = request.TryGetProperty("expectedRevision", out var rev)
            ? rev.GetInt32()
            : (int?)null;

        var command = new UpdateCommercialPolicyCommand(
            propertyId,
            policyId,
            replacementPolicyId ?? Guid.Empty,
            expectedRevision,
            Actor(user),
            deactivationReason);

        return await dispatcher.SendAsync(command, cancellationToken);
    }

    private static async Task<IResult> DeleteAsync(
        Guid propertyId, Guid policyId, ClaimsPrincipal user,
        IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(
            new DeleteCommercialPolicyCommand(propertyId, policyId, null, Actor(user)),
            cancellationToken);
        return Results.NoContent();
    }

    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Authenticated subject is required.");

    internal sealed record CreateCommercialPolicyEndpointRequest(string Type, bool SetAsDefault);
    internal sealed record SetDefaultCommercialPolicyEndpointRequest(Guid PolicyId, bool ApplyToExistingAccommodations, int ExpectedRevision);
    internal sealed record SetDefaultPolicyResult(CommercialPolicyDto DefaultPolicy, int UpdatedAccommodationCount, int Revision);
}
