using FluentValidation;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;

internal sealed record GetPropertyOnboardingQuery(Guid OnboardingId) : IQuery<PropertyOnboardingResponse>;
internal sealed record ListPropertyOnboardingsQuery(int Page, int Size, Guid? PartnerId, string? DestinationId, string? LifecycleStatus, string? ReadinessStatus, string? PendingOwnerType, bool? Overdue, string? Sort, string? Order) : IQuery<PropertyOnboardingListResponse>;
internal sealed record AddressResponse(string Street, string Number, string? Complement, string District, string City, string State, string PostalCode, string CountryCode);
internal sealed record PropertyResponse(string Name, string DestinationId, AddressResponse Address);
internal sealed record EvidenceReferenceResponse(string Kind, string Reference, string Description);
internal sealed record ReadinessGateResponse(Guid Id, string Type, string Status, string? Notes, IReadOnlyList<EvidenceReferenceResponse> Evidence, DateTimeOffset? ValidatedAt, string? ValidatedBy, DateTimeOffset UpdatedAt);
internal sealed record PendingIssueResponse(Guid Id, string Description, string OwnerType, string? AssigneeId, string Status, string? RelatedGateType, DateTimeOffset? TargetAt, DateTimeOffset OpenedAt, string OpenedBy, DateTimeOffset? ResolvedAt, string? ResolutionNote);
internal sealed record CommunicationRecordResponse(Guid Id, string Channel, DateTimeOffset ReceivedAt, DateTimeOffset ProcessedAt, string ResultSummary, bool ProcessedWithinSla, string CreatedBy, DateTimeOffset CreatedAt);
internal sealed record DuplicateReviewResponse(Guid Id, string Decision, Guid? ExistingPropertyId, string Justification, DateTimeOffset ReviewedAt, string ReviewedBy);
internal sealed record DuplicateReviewResultResponse(DuplicateReviewResponse Review, PropertyOnboardingResponse Onboarding);
internal sealed record DuplicateCandidateResponse(Guid PropertyId, string Name, string AddressSummary, IReadOnlyList<string> MatchReasons, decimal SimilarityScore);
internal sealed record DuplicateReviewStateResponse(bool Required, IReadOnlyList<DuplicateCandidateResponse> Candidates, string? LatestDecision);
internal sealed record BlockingReasonResponse(string Code, string Message, string? RelatedResourceId);
internal sealed record PropertyOnboardingResponse(Guid Id, Guid PartnerId, string PreselectionId, PropertyResponse Property, string LifecycleStatus, string ReadinessStatus, IReadOnlyList<ReadinessGateResponse> ReadinessGates, IReadOnlyList<PendingIssueResponse> PendingIssues, DuplicateReviewStateResponse DuplicateReview, IReadOnlyList<BlockingReasonResponse> BlockingReasons, DateTimeOffset OpenedAt, DateTimeOffset TargetSubmissionAt, DateTimeOffset? SubmittedAt, DateTimeOffset? ClosedAt, string? CloseReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
internal sealed record PropertyOnboardingSummaryResponse(Guid Id, Guid PartnerId, string PartnerDisplayName, string PropertyName, string DestinationId, string LifecycleStatus, string ReadinessStatus, int OpenPendingIssueCount, IReadOnlyList<BlockingReasonResponse> BlockingReasons, DateTimeOffset OpenedAt, DateTimeOffset TargetSubmissionAt, DateTimeOffset UpdatedAt);
internal sealed record PropertyOnboardingPaginationResponse(int Page, int Size, int Total, int TotalPages);
internal sealed record PropertyOnboardingListResponse(IReadOnlyList<PropertyOnboardingSummaryResponse> Data, PropertyOnboardingPaginationResponse Pagination);

internal sealed class GetPropertyOnboardingQueryHandler(InventoryDbContext dbContext) : IQueryHandler<GetPropertyOnboardingQuery, PropertyOnboardingResponse>
{
    public async Task<PropertyOnboardingResponse> HandleAsync(GetPropertyOnboardingQuery query, CancellationToken cancellationToken)
    {
        var onboarding = await dbContext.PropertyOnboardings.AsNoTracking().Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).SingleOrDefaultAsync(item => item.Id == query.OnboardingId, cancellationToken)
            ?? throw new NotFoundException("Property onboarding was not found.", "PROPERTY_ONBOARDING_NOT_FOUND");
        var candidates = await PropertyOnboardingMapper.GetSimilarityCandidatesAsync(dbContext, onboarding, cancellationToken);
        return PropertyOnboardingMapper.ToResponse(onboarding, candidates);
    }
}

internal sealed class ListPropertyOnboardingsQueryHandler(InventoryDbContext dbContext, IClock clock, IValidator<ListPropertyOnboardingsQuery> validator) : IQueryHandler<ListPropertyOnboardingsQuery, PropertyOnboardingListResponse>
{
    public async Task<PropertyOnboardingListResponse> HandleAsync(ListPropertyOnboardingsQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var items = dbContext.PropertyOnboardings.AsNoTracking().Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).Include(item => item.Property).AsQueryable();
        if (query.PartnerId.HasValue) items = items.Where(item => item.PartnerId == query.PartnerId);
        if (query.DestinationId is not null) items = items.Where(item => item.Property.DestinationId == query.DestinationId);
        if (query.LifecycleStatus is not null) items = items.Where(item => item.LifecycleStatus == PropertyOnboardingMapper.ParseLifecycleStatus(query.LifecycleStatus));
        if (query.ReadinessStatus is not null) items = items.Where(item => (item.ReadinessGates.All(g => g.Status == ReadinessGateStatus.Validated) && !item.PendingIssues.Any(issue => issue.Status == PendingIssueStatus.Open) && !item.DuplicateReviewRequiresDecision) == (PropertyOnboardingMapper.ParseReadinessStatus(query.ReadinessStatus) == ReadinessStatus.Ready));
        if (query.PendingOwnerType is not null) items = items.Where(item => item.PendingIssues.Any(issue => issue.Status == PendingIssueStatus.Open && issue.OwnerType == PropertyOnboardingMapper.ParsePendingOwnerType(query.PendingOwnerType)));
        if (query.Overdue == true) { var now = clock.UtcNow; items = items.Where(item => item.TargetSubmissionAt < now && item.LifecycleStatus != OnboardingLifecycleStatus.Closed && item.LifecycleStatus != OnboardingLifecycleStatus.SubmittedToCuration); }
        items = (query.Sort ?? "targetSubmissionAt", query.Order ?? "asc") switch
        {
            ("openedAt", "desc") => items.OrderByDescending(item => item.OpenedAt),
            ("targetSubmissionAt", "desc") => items.OrderByDescending(item => item.TargetSubmissionAt),
            ("updatedAt", "desc") => items.OrderByDescending(item => item.UpdatedAt),
            ("propertyName", "desc") => items.OrderByDescending(item => item.Property.Name),
            ("openedAt", _) => items.OrderBy(item => item.OpenedAt),
            ("updatedAt", _) => items.OrderBy(item => item.UpdatedAt),
            ("propertyName", _) => items.OrderBy(item => item.Property.Name),
            _ => items.OrderBy(item => item.TargetSubmissionAt),
        };
        var total = await items.CountAsync(cancellationToken);
        var page = await items.Skip((query.Page - 1) * query.Size).Take(query.Size).ToListAsync(cancellationToken);
        var partnerNames = await dbContext.Partners.AsNoTracking().Where(partner => page.Select(item => item.PartnerId).Contains(partner.Id)).ToDictionaryAsync(partner => partner.Id, partner => partner.TradeName ?? partner.LegalName, cancellationToken);
        var data = page.Select(item => PropertyOnboardingMapper.ToSummary(item, partnerNames[item.PartnerId])).ToList();
        return new PropertyOnboardingListResponse(data, new PropertyOnboardingPaginationResponse(query.Page, query.Size, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.Size)));
    }
}

internal static class PropertyOnboardingMapper
{
    internal static Property ToProperty(PropertyInput input) => new(input.Name, input.DestinationId, ToAddress(input.Address));
    internal static Address ToAddress(AddressInput input) => new(input.Street, input.Number, input.Complement, input.District, input.City, input.State, input.PostalCode, input.CountryCode);
    internal static PropertyOnboardingResponse ToResponse(PropertyOnboarding item, IReadOnlyList<DuplicateCandidateResponse>? candidates = null) => new(item.Id, item.PartnerId, item.PreselectionId, new PropertyResponse(item.Property.Name, item.Property.DestinationId, new AddressResponse(item.Property.Address.Street, item.Property.Address.Number, item.Property.Address.Complement, item.Property.Address.District, item.Property.Address.City, item.Property.Address.State, item.Property.Address.PostalCode, item.Property.Address.CountryCode)), ContractValue(item.LifecycleStatus), ContractValue(item.ReadinessStatus), item.ReadinessGates.Select(g => new ReadinessGateResponse(g.Id, ContractValue(g.Type), ContractValue(g.Status), g.Notes, g.Evidence.Select(e => new EvidenceReferenceResponse(ContractValue(e.Kind), e.Reference, e.Description)).ToList(), g.ValidatedAt, g.ValidatedBy, g.UpdatedAt)).ToList(), item.PendingIssues.Select(ToPendingIssueResponse).ToList(), new DuplicateReviewStateResponse(item.DuplicateReviewRequiresDecision, candidates ?? [], item.DuplicateReviews.OrderByDescending(review => review.ReviewedAt).Select(review => ContractValue(review.Decision)).FirstOrDefault()), ToBlockingReasons(item), item.OpenedAt, item.TargetSubmissionAt, item.SubmittedAt, item.ClosedAt, item.CloseReason, item.CreatedAt, item.UpdatedAt);
    internal static PendingIssueResponse ToPendingIssueResponse(PendingIssue item) => new(item.Id, item.Description, ContractValue(item.OwnerType), item.AssigneeId, ContractValue(item.Status), item.RelatedGateType is null ? null : ContractValue(item.RelatedGateType.Value), item.TargetAt, item.OpenedAt, item.OpenedBy, item.ResolvedAt, item.ResolutionNote);
    internal static CommunicationRecordResponse ToCommunicationRecordResponse(CommunicationRecord item) => new(item.Id, ContractValue(item.Channel), item.ReceivedAt, item.ProcessedAt, item.ResultSummary, item.ProcessedWithinSla, item.CreatedBy, item.CreatedAt);
    internal static DuplicateReviewResponse ToDuplicateReviewResponse(DuplicateReview item) => new(item.Id, ContractValue(item.Decision), item.ExistingPropertyId, item.Justification, item.ReviewedAt, item.ReviewedBy);
    internal static PropertyOnboardingSummaryResponse ToSummary(PropertyOnboarding item, string partnerName) => new(item.Id, item.PartnerId, partnerName, item.Property.Name, item.Property.DestinationId, ContractValue(item.LifecycleStatus), ContractValue(item.ReadinessStatus), item.PendingIssues.Count(i => i.Status == PendingIssueStatus.Open), ToBlockingReasons(item), item.OpenedAt, item.TargetSubmissionAt, item.UpdatedAt);
    private static IReadOnlyList<BlockingReasonResponse> ToBlockingReasons(PropertyOnboarding item) => item.GetBlockingReasons().Select(reason => new BlockingReasonResponse(ContractValue(reason.Code), reason.Message, reason.RelatedResourceId)).ToList();
    private static string ContractValue<TEnum>(TEnum value) where TEnum : struct, Enum => char.ToLowerInvariant(value.ToString()[0]) + value.ToString()[1..];
    internal static async Task<IReadOnlyList<DuplicateCandidateResponse>> GetSimilarityCandidatesAsync(InventoryDbContext dbContext, PropertyOnboarding onboarding, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.PropertyOnboardings.AsNoTracking().Where(item => item.Id != onboarding.Id && (item.PropertySimilarityKey == onboarding.PropertySimilarityKey || EF.Functions.ILike(item.Property.Name, onboarding.Property.Name))).ToListAsync(cancellationToken);
        return candidates.Select(item => new DuplicateCandidateResponse(item.Id, item.Property.Name, $"{item.Property.Address.Street}, {item.Property.Address.Number} — {item.Property.Address.City}/{item.Property.Address.State}", new[] { item.PropertySimilarityKey == onboarding.PropertySimilarityKey ? "similarAddress" : "similarName" }, 1m)).ToList();
    }
    internal static OnboardingLifecycleStatus ParseLifecycleStatus(string value) => Enum.Parse<OnboardingLifecycleStatus>(value, true);
    internal static ReadinessStatus ParseReadinessStatus(string value) => Enum.Parse<ReadinessStatus>(value, true);
    internal static PendingOwnerType ParsePendingOwnerType(string value) => Enum.Parse<PendingOwnerType>(value, true);
}
