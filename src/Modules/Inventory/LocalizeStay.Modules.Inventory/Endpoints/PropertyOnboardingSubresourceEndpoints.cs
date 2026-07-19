using System.Security.Claims;
using System.Text.Json.Serialization;
using LocalizeStay.Modules.Inventory.Application.Partners;
using LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class PropertyOnboardingSubresourceEndpoints
{
    public static void MapPropertyOnboardingSubresourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/property-onboardings/{onboardingId:guid}").WithTags("Readiness").RequireAuthorization(PortfolioOnboardingPermissions.Write);
        group.MapPatch("/readiness-gates/{gateType}", UpdateGateAsync);
        group.MapPost("/pending-issues", CreateIssueAsync);
        group.MapPatch("/pending-issues/{pendingIssueId:guid}", UpdateIssueAsync);
        group.MapPost("/communication-records", CreateCommunicationAsync);
        group.MapPost("/duplicate-reviews", CreateDuplicateReviewAsync);
    }

    private static Task<PropertyOnboardingResponse> UpdateGateAsync(Guid onboardingId, string gateType, UpdateReadinessGateRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.SendAsync(new UpdateReadinessGateCommand(onboardingId, gateType, request.Status, request.Notes, request.Evidence, request.ContractReference, request.AuthorizedContact, request.OperationalChannelTest, Actor(user)), cancellationToken);
    private static async Task<IResult> CreateIssueAsync(Guid onboardingId, CreatePendingIssueRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new CreatePendingIssueCommand(onboardingId, request.Description, request.OwnerType, request.AssigneeId, request.RelatedGateType, request.TargetAt, Actor(user)), cancellationToken);
        return Results.Created($"/api/v1/property-onboardings/{onboardingId}/pending-issues/{result.Id}", result);
    }
    private static Task<PendingIssueResponse> UpdateIssueAsync(Guid onboardingId, Guid pendingIssueId, UpdatePendingIssueRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) => dispatcher.SendAsync(new UpdatePendingIssueCommand(onboardingId, pendingIssueId, request.Description, request.OwnerType, request.AssigneeId, request.HasAssigneeId, request.TargetAt, request.HasTargetAt, request.Status, request.ResolutionNote, Actor(user)), cancellationToken);
    private static async Task<IResult> CreateCommunicationAsync(Guid onboardingId, CreateCommunicationRecordRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new CreateCommunicationRecordCommand(onboardingId, request.Channel, request.ReceivedAt, request.ProcessedAt, request.ResultSummary, Actor(user)), cancellationToken);
        return Results.Created($"/api/v1/property-onboardings/{onboardingId}/communication-records/{result.Id}", result);
    }
    private static async Task<IResult> CreateDuplicateReviewAsync(Guid onboardingId, CreateDuplicateReviewRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new CreateDuplicateReviewCommand(onboardingId, request.IdempotencyKey, request.Decision, request.ExistingPropertyId, request.Justification, Actor(user)), cancellationToken);
        return Results.Created($"/api/v1/property-onboardings/{onboardingId}/duplicate-reviews/{result.Review.Id}", result);
    }
    private static string Actor(ClaimsPrincipal user) => user.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Authenticated subject is required.");
}

internal sealed record UpdateReadinessGateRequest(string Status, string? Notes, IReadOnlyList<EvidenceReferenceInput>? Evidence, ContractReferenceInput? ContractReference, ContactInput? AuthorizedContact, OperationalChannelTestInput? OperationalChannelTest);
internal sealed record CreatePendingIssueRequest(string Description, string OwnerType, string? AssigneeId, string? RelatedGateType, DateTimeOffset? TargetAt);
internal sealed class UpdatePendingIssueRequest
{
    private string? _assigneeId;
    private DateTimeOffset? _targetAt;

    public string? Description { get; init; }
    public string? OwnerType { get; init; }
    public string? AssigneeId
    {
        get => _assigneeId;
        set
        {
            _assigneeId = value;
            HasAssigneeId = true;
        }
    }
    public DateTimeOffset? TargetAt
    {
        get => _targetAt;
        set
        {
            _targetAt = value;
            HasTargetAt = true;
        }
    }
    public string? Status { get; init; }
    public string? ResolutionNote { get; init; }
    [JsonIgnore]
    public bool HasAssigneeId { get; private set; }
    [JsonIgnore]
    public bool HasTargetAt { get; private set; }
}
internal sealed record CreateCommunicationRecordRequest(string Channel, DateTimeOffset ReceivedAt, DateTimeOffset ProcessedAt, string ResultSummary);
internal sealed record CreateDuplicateReviewRequest(Guid IdempotencyKey, string Decision, Guid? ExistingPropertyId, string Justification);
