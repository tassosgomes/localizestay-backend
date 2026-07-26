using System.Diagnostics;
using System.Globalization;
using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Observability;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed class ListCommercialOffersQueryHandler(InventoryDbContext dbContext, IClock clock, IValidator<ListCommercialOffersQuery> validator) : IQueryHandler<ListCommercialOffersQuery, CommercialOfferListResponse>
{
    public async Task<CommercialOfferListResponse> HandleAsync(ListCommercialOffersQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        var items = dbContext.CommercialOffers.AsNoTracking().AsQueryable();

        if (query.PropertyId.HasValue)
            items = items.Where(o => o.PropertyId == query.PropertyId.Value);

        if (query.Status is not null && Enum.TryParse<OfferState>(query.Status, true, out var status))
            items = items.Where(o => o.State == status);

        if (query.HasBlockingIssues == true)
            items = items.Where(o => o.BlockingIssueCount > 0);
        else if (query.HasBlockingIssues == false)
            items = items.Where(o => o.BlockingIssueCount == 0);

        if (query.Overdue == true)
        {
            var now = clock.UtcNow;
            items = items.Where(o => o.TargetSubmissionAt.HasValue && o.TargetSubmissionAt < now && o.State != OfferState.Published);
        }

        items = (query.Sort ?? "targetSubmissionAt", query.Order ?? "asc") switch
        {
            ("updatedAt", "desc") => items.OrderByDescending(o => o.UpdatedAt),
            ("completeInformationReceivedAt", "desc") => items.OrderByDescending(o => o.CompleteInformationReceivedAt),
            ("propertyName", "desc") => items.OrderByDescending(o => dbContext.IncorporatedProperties.Where(p => p.Id == o.PropertyId).Select(p => p.PropertyName).FirstOrDefault()),
            ("updatedAt", _) => items.OrderBy(o => o.UpdatedAt),
            ("completeInformationReceivedAt", _) => items.OrderBy(o => o.CompleteInformationReceivedAt),
            ("propertyName", _) => items.OrderBy(o => dbContext.IncorporatedProperties.Where(p => p.Id == o.PropertyId).Select(p => p.PropertyName).FirstOrDefault()),
            _ => items.OrderBy(o => o.TargetSubmissionAt).ThenBy(o => o.Id),
        };

        var total = await items.CountAsync(cancellationToken);
        var page = await items.Skip((query.Page - 1) * query.Size).Take(query.Size).ToListAsync(cancellationToken);
        var propertyIds = page.Select(o => o.PropertyId).Distinct().ToList();
        var properties = await dbContext.IncorporatedProperties.AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);

        var data = page.Select(o =>
        {
            var property = properties.GetValueOrDefault(o.PropertyId);
            var propertyName = property?.PropertyName ?? "Unknown";
            var destinationId = property?.DestinationId;
            var completeness = 100 - o.BlockingIssueCount * 33;
            if (completeness < 0) completeness = 0;
            return new CommercialOfferSummaryDto(
                o.PropertyId,
                propertyName,
                destinationId,
                CommercialOfferMapper.ContractValue(o.State),
                o.Revision,
                completeness,
                o.BlockingIssueCount,
                o.AccommodationCount,
                0,
                o.EverSubmitted,
                CommercialOfferMapper.ToStaffActor(o.RevisionAuthor, o.RevisionAuthor),
                o.CompleteInformationReceivedAt,
                o.TargetSubmissionAt,
                o.Submissions.OrderByDescending(s => s.SubmittedAt).Select(s => (DateTimeOffset?)s.SubmittedAt).FirstOrDefault(),
                o.CreatedAt,
                o.UpdatedAt);
        }).ToList();

        return new CommercialOfferListResponse(data, new PaginationResponse(query.Page, query.Size, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.Size)));
    }
}

internal sealed class ListCommercialOffersQueryValidator : AbstractValidator<ListCommercialOffersQuery>
{
    public ListCommercialOffersQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
    }
}

internal sealed class GetCommercialOfferQueryHandler(InventoryDbContext dbContext, IBusinessCalendar businessCalendar, IClock clock) : IQueryHandler<GetCommercialOfferQuery, CommercialOfferDetailDto>
{
    public async Task<CommercialOfferDetailDto> HandleAsync(GetCommercialOfferQuery query, CancellationToken cancellationToken)
    {
        using var activity = InventoryTelemetry.ActivitySource.StartActivity(InventoryTelemetry.Spans.Load);
        activity?.SetTag(InventoryTelemetry.Tags.PropertyId, query.PropertyId.ToString());

        var property = await dbContext.IncorporatedProperties.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == query.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Property was not found.", "PROPERTY_NOT_FOUND");

        var offer = await dbContext.CommercialOffers.AsNoTracking()
            .Include(o => o.CurrentValidation)
            .Include(o => o.Submissions)
            .Include(o => o.Returns)
            .Include(o => o.Policies)
            .Include(o => o.Accommodations)
            .Include(o => o.Rates)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.PropertyId == query.PropertyId, cancellationToken);

        if (offer is null)
        {
            var utcNow = clock.UtcNow;
            var created = CommercialOffer.Create(query.PropertyId, property.InitialActor, utcNow);
            created.SetTargetSubmissionAt(businessCalendar.AddBusinessDays(utcNow, 10));
            dbContext.CommercialOffers.Add(created);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (!cancellationToken.IsCancellationRequested)
            {
                dbContext.ChangeTracker.Clear();
                offer = await dbContext.CommercialOffers.AsNoTracking()
                    .Include(o => o.CurrentValidation)
                    .Include(o => o.Submissions)
                    .Include(o => o.Returns)
                    .Include(o => o.Policies)
                    .Include(o => o.Accommodations)
                    .Include(o => o.Rates)
                    .AsSplitQuery()
                    .SingleOrDefaultAsync(o => o.PropertyId == query.PropertyId, cancellationToken);
                if (offer is not null)
                    goto OfferLoaded;
                activity?.SetStatus(ActivityStatusCode.Error, "Commercial offer draft creation lost a concurrency race and could not be reloaded.");
                throw;
            }

            InventoryTelemetry.OfferCreated.Add(1, new KeyValuePair<string, object?>("result", InventoryTelemetry.Tags.ResultSuccess));
            activity?.SetTag(InventoryTelemetry.Tags.OfferRevision, created.Revision);

            return new CommercialOfferDetailDto(
                created.PropertyId,
                property.PropertyName,
                property.DestinationId,
                CommercialOfferMapper.ContractValue(created.State),
                created.Revision,
                0,
                created.BlockingIssueCount,
                created.AccommodationCount,
                0,
                created.EverSubmitted,
                CommercialOfferMapper.ToStaffActor(created.RevisionAuthor, created.RevisionAuthor),
                created.CompleteInformationReceivedAt,
                created.TargetSubmissionAt,
                null,
                null,
                [],
                [],
                CommercialOfferMapper.ToPendingIssues(created.GetPendingIssues()),
                null,
                null,
                created.CreatedAt,
                created.UpdatedAt);
        }

    OfferLoaded:
        activity?.SetTag(InventoryTelemetry.Tags.OfferRevision, offer.Revision);
        var completeness = 100 - offer.BlockingIssueCount * 33;
        if (completeness < 0) completeness = 0;

        var completeAccommodationCount = offer.Accommodations.Count(a => a.Status == AccommodationStatus.Active && a.IsCommerciallyComplete());

        OfferValidationResponse? currentValidation = null;
        if (offer.CurrentValidation is not null)
        {
            currentValidation = CommercialOfferMapper.ToResponse(offer.CurrentValidation);
        }

        OfferReturnResponse? latestReturn = null;
        var lastReturn = offer.Returns.OrderByDescending(r => r.ReturnedAt).FirstOrDefault();
        if (lastReturn is not null)
        {
            latestReturn = new OfferReturnResponse(
                lastReturn.Id,
                lastReturn.SubmissionId,
                lastReturn.ReasonCode,
                lastReturn.Reason,
                lastReturn.ReturnedBy,
                lastReturn.ReturnedAt);
        }

        var lastSubmittedAt = offer.Submissions.OrderByDescending(s => s.SubmittedAt)
            .Select(s => (DateTimeOffset?)s.SubmittedAt).FirstOrDefault();

        var defaultPolicy = offer.Policies.FirstOrDefault(p => p.IsDefault && p.Status == PolicyStatus.Active);
        var defaultPolicyId = defaultPolicy?.Id;

        var policies = offer.Policies.Select(p => new CommercialPolicyDto(
            p.Id,
            p.PropertyId,
            CommercialOfferMapper.ContractValue(p.Type),
            p.Title,
            p.RulesSummary,
            p.RuleSetVersion,
            p.IsDefault,
            CommercialOfferMapper.ContractValue(p.Status),
            p.UsageCount,
            p.EverSubmitted,
            p.DeactivationReason,
            p.CreatedAt,
            p.UpdatedAt)).ToList();

        var accommodations = offer.Accommodations.Select(a =>
        {
            var pendingIssues = CommercialOfferMapper.AccommodationPendingIssues(a);
            return new AccommodationDto(
                a.Id,
                a.PropertyId,
                a.CommercialName,
                null,
                a.BedConfiguration.Select(CommercialOfferMapper.ToBedConfigurationItem).ToList(),
                a.StructuralFeatures.ToList(),
                a.TotalCapacity,
                a.MaxAdults,
                a.MaxChildren,
                CommercialOfferMapper.ToChildAgeRange(a),
                CommercialOfferMapper.ContractValue(a.ChildAgeRangeSource),
                a.PolicyId,
                CommercialOfferMapper.ContractValue(a.Status),
                a.DeactivationReason,
                CommercialOfferMapper.AccommodationCompletenessPercentage(a),
                pendingIssues,
                0,
                0,
                a.EverSubmitted,
                a.CreatedAt,
                a.UpdatedAt);
        }).ToList();

        return new CommercialOfferDetailDto(
            offer.PropertyId,
            property.PropertyName,
            property.DestinationId,
            CommercialOfferMapper.ContractValue(offer.State),
            offer.Revision,
            completeness,
            offer.BlockingIssueCount,
            offer.AccommodationCount,
            completeAccommodationCount,
            offer.EverSubmitted,
            CommercialOfferMapper.ToStaffActor(offer.RevisionAuthor, offer.RevisionAuthor),
            offer.CompleteInformationReceivedAt,
            offer.TargetSubmissionAt,
            lastSubmittedAt,
            defaultPolicyId,
            policies,
            accommodations,
            CommercialOfferMapper.ToPendingIssues(offer.GetPendingIssues()),
            currentValidation,
            latestReturn,
            offer.CreatedAt,
            offer.UpdatedAt);
    }
}

internal sealed class ListCommercialPoliciesQueryHandler(InventoryDbContext dbContext) : IQueryHandler<ListCommercialPoliciesQuery, CommercialPolicyListResponse>
{
    public async Task<CommercialPolicyListResponse> HandleAsync(ListCommercialPoliciesQuery query, CancellationToken cancellationToken)
    {
        var propertyExists = await dbContext.IncorporatedProperties.AsNoTracking()
            .AnyAsync(p => p.Id == query.PropertyId, cancellationToken);

        if (!propertyExists)
            throw new NotFoundException("Property was not found.", "PROPERTY_NOT_FOUND");

        var items = dbContext.CommercialPolicies.AsNoTracking()
            .Where(p => p.PropertyId == query.PropertyId);

        if (query.Status is not null && Enum.TryParse<PolicyStatus>(query.Status, true, out var status))
            items = items.Where(p => p.Status == status);

        var data = await items.Select(p => new CommercialPolicyDto(
            p.Id,
            p.PropertyId,
            CommercialOfferMapper.ContractValue(p.Type),
            p.Title,
            p.RulesSummary,
            p.RuleSetVersion,
            p.IsDefault,
            CommercialOfferMapper.ContractValue(p.Status),
            p.UsageCount,
            p.EverSubmitted,
            p.DeactivationReason,
            p.CreatedAt,
            p.UpdatedAt)).ToListAsync(cancellationToken);

        return new CommercialPolicyListResponse(data);
    }
}

internal sealed class ListAccommodationsQueryHandler(InventoryDbContext dbContext, IValidator<ListAccommodationsQuery> validator) : IQueryHandler<ListAccommodationsQuery, AccommodationListResponse>
{
    public async Task<AccommodationListResponse> HandleAsync(ListAccommodationsQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        var propertyExists = await dbContext.IncorporatedProperties.AsNoTracking()
            .AnyAsync(p => p.Id == query.PropertyId, cancellationToken);

        if (!propertyExists)
            throw new NotFoundException("Property was not found.", "PROPERTY_NOT_FOUND");

        var items = dbContext.Accommodations.AsNoTracking()
            .Where(a => a.PropertyId == query.PropertyId);

        if (query.Status is not null && Enum.TryParse<AccommodationStatus>(query.Status, true, out var status))
            items = items.Where(a => a.Status == status);

        if (query.Completeness == "complete")
            items = items.Where(a => a.MaxAdults.HasValue && a.TotalCapacity.HasValue && a.MealPlan.HasValue && a.PolicyId.HasValue);
        else if (query.Completeness == "incomplete")
            items = items.Where(a => !a.MaxAdults.HasValue || !a.TotalCapacity.HasValue || !a.MealPlan.HasValue || !a.PolicyId.HasValue);

        items = (query.Sort ?? "updatedAt", query.Order ?? "desc") switch
        {
            ("commercialName", "desc") => items.OrderByDescending(a => a.CommercialName),
            ("createdAt", "desc") => items.OrderByDescending(a => a.CreatedAt),
            ("commercialName", _) => items.OrderBy(a => a.CommercialName),
            ("createdAt", _) => items.OrderBy(a => a.CreatedAt),
            _ => items.OrderByDescending(a => a.UpdatedAt).ThenBy(a => a.Id),
        };

        var total = await items.CountAsync(cancellationToken);
        var page = await items.Skip((query.Page - 1) * query.Size).Take(query.Size).ToListAsync(cancellationToken);

        var data = page.Select(a => new AccommodationDto(
            a.Id,
            a.PropertyId,
            a.CommercialName,
            null,
            a.BedConfiguration.Select(CommercialOfferMapper.ToBedConfigurationItem).ToList(),
            a.StructuralFeatures.ToList(),
            a.TotalCapacity,
            a.MaxAdults,
            a.MaxChildren,
            CommercialOfferMapper.ToChildAgeRange(a),
            CommercialOfferMapper.ContractValue(a.ChildAgeRangeSource),
            a.PolicyId,
            CommercialOfferMapper.ContractValue(a.Status),
            a.DeactivationReason,
            CommercialOfferMapper.AccommodationCompletenessPercentage(a),
            CommercialOfferMapper.AccommodationPendingIssues(a),
            0,
            0,
            a.EverSubmitted,
            a.CreatedAt,
            a.UpdatedAt)).ToList();

        return new AccommodationListResponse(data, new PaginationResponse(query.Page, query.Size, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.Size)));
    }
}

internal sealed class ListAccommodationsQueryValidator : AbstractValidator<ListAccommodationsQuery>
{
    public ListAccommodationsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
    }
}

internal sealed class GetAccommodationQueryHandler(InventoryDbContext dbContext) : IQueryHandler<GetAccommodationQuery, AccommodationDto>
{
    public async Task<AccommodationDto> HandleAsync(GetAccommodationQuery query, CancellationToken cancellationToken)
    {
        var accommodation = await dbContext.Accommodations.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == query.AccommodationId && a.PropertyId == query.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

        return new AccommodationDto(
            accommodation.Id,
            accommodation.PropertyId,
            accommodation.CommercialName,
            null,
            accommodation.BedConfiguration.Select(CommercialOfferMapper.ToBedConfigurationItem).ToList(),
            accommodation.StructuralFeatures.ToList(),
            accommodation.TotalCapacity,
            accommodation.MaxAdults,
            accommodation.MaxChildren,
            CommercialOfferMapper.ToChildAgeRange(accommodation),
            CommercialOfferMapper.ContractValue(accommodation.ChildAgeRangeSource),
            accommodation.PolicyId,
            CommercialOfferMapper.ContractValue(accommodation.Status),
            accommodation.DeactivationReason,
            CommercialOfferMapper.AccommodationCompletenessPercentage(accommodation),
            CommercialOfferMapper.AccommodationPendingIssues(accommodation),
            0,
            0,
            accommodation.EverSubmitted,
            accommodation.CreatedAt,
            accommodation.UpdatedAt);
    }
}

internal sealed class ListCommercialRatesQueryHandler(InventoryDbContext dbContext, IValidator<ListCommercialRatesQuery> validator) : IQueryHandler<ListCommercialRatesQuery, CommercialRateListResponse>
{
    public async Task<CommercialRateListResponse> HandleAsync(ListCommercialRatesQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        var accommodationExists = await dbContext.Accommodations.AsNoTracking()
            .AnyAsync(a => a.Id == query.AccommodationId && a.PropertyId == query.PropertyId, cancellationToken);

        if (!accommodationExists)
            throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

        var items = dbContext.CommercialRates.AsNoTracking()
            .Where(r => r.AccommodationId == query.AccommodationId);

        if (query.Status is not null && Enum.TryParse<RateStatus>(query.Status, true, out var status))
            items = items.Where(r => r.Status == status);

        if (query.ActiveOn is not null && DateOnly.TryParse(query.ActiveOn, out var activeOn))
            items = items.Where(r => r.Status == RateStatus.Active && r.ValidFrom <= activeOn && r.ValidTo >= activeOn);

        if (query.ValidFrom is not null && DateOnly.TryParse(query.ValidFrom, out var validFromFilter))
            items = items.Where(r => r.ValidFrom.HasValue && r.ValidFrom.Value >= validFromFilter);

        if (query.ValidTo is not null && DateOnly.TryParse(query.ValidTo, out var validToFilter))
            items = items.Where(r => r.ValidTo.HasValue && r.ValidTo.Value <= validToFilter);

        items = (query.Sort ?? "validFrom", query.Order ?? "asc") switch
        {
            ("validTo", "desc") => items.OrderByDescending(r => r.ValidTo),
            ("basePriceCents", "desc") => items.OrderByDescending(r => r.BasePriceCents),
            ("updatedAt", "desc") => items.OrderByDescending(r => r.UpdatedAt),
            ("validTo", _) => items.OrderBy(r => r.ValidTo),
            ("basePriceCents", _) => items.OrderBy(r => r.BasePriceCents),
            ("updatedAt", _) => items.OrderBy(r => r.UpdatedAt),
            _ => items.OrderBy(r => r.ValidFrom).ThenBy(r => r.Id),
        };

        var total = await items.CountAsync(cancellationToken);
        var page = await items.Skip((query.Page - 1) * query.Size).Take(query.Size).ToListAsync(cancellationToken);

        var data = page.Select(r => new CommercialRateDto(
            r.Id,
            r.AccommodationId,
            r.Name,
            r.ConditionCode,
            r.BasePriceCents,
            r.IncludedGuests,
            r.AdditionalAdultPriceCents,
            r.AdditionalChildPriceCents,
            r.ValidFrom?.ToString("yyyy-MM-dd"),
            r.ValidTo?.ToString("yyyy-MM-dd"),
            r.MinimumNights,
            r.PolicyId,
            r.MealPlan is not null ? CommercialOfferMapper.ContractValue(r.MealPlan.Value) : null,
            "BRL",
            true,
            CommercialOfferMapper.ContractValue(r.Status),
            r.DeactivationReason,
            CommercialOfferMapper.RateCompletenessPercentage(r),
            CommercialOfferMapper.RatePendingIssues(r),
            r.EverSubmitted,
            r.CreatedAt,
            r.UpdatedAt)).ToList();

        return new CommercialRateListResponse(data, new PaginationResponse(query.Page, query.Size, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.Size)));
    }
}

internal sealed class ListCommercialRatesQueryValidator : AbstractValidator<ListCommercialRatesQuery>
{
    public ListCommercialRatesQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
        RuleFor(q => q.ActiveOn).Must(BeIsoDate).When(q => q.ActiveOn is not null);
        RuleFor(q => q.ValidFrom).Must(BeIsoDate).When(q => q.ValidFrom is not null);
        RuleFor(q => q.ValidTo).Must(BeIsoDate).When(q => q.ValidTo is not null);
    }

    private static bool BeIsoDate(string? value) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
}

internal sealed class ListCommercialOfferHistoryQueryHandler(InventoryDbContext dbContext, IValidator<ListCommercialOfferHistoryQuery> validator) : IQueryHandler<ListCommercialOfferHistoryQuery, OfferHistoryListResponse>
{
    private static readonly IReadOnlySet<string> _safeMetadataKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "propertyId",
        "revision",
        "eventType",
        "reasonCode",
        "submissionId",
    };

    public async Task<OfferHistoryListResponse> HandleAsync(ListCommercialOfferHistoryQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        var offerExists = await dbContext.CommercialOffers.AsNoTracking()
            .AnyAsync(o => o.PropertyId == query.PropertyId, cancellationToken);

        if (!offerExists)
            throw new NotFoundException("Commercial offer was not found.", "OFFER_NOT_FOUND");

        var entries = dbContext.BusinessAuditEntries.AsNoTracking()
            .Where(e => e.AggregateType == "CommercialOffer" && e.AggregateId == query.PropertyId.ToString());

        if (query.EventType is not null)
        {
            var auditType = ToAuditType(query.EventType);
            if (auditType is not null)
                entries = entries.Where(e => e.AuditType == auditType);
        }

        entries = entries.OrderByDescending(e => e.OccurredOnUtc);

        var total = await entries.CountAsync(cancellationToken);
        var data = await entries.Skip((query.Page - 1) * query.Size).Take(query.Size)
            .Select(e => new HistoryAuditProjection(e.Id, e.AuditType, e.OccurredOnUtc, e.Actor, e.Summary, e.Metadata))
            .ToListAsync(cancellationToken);

        var response = data.Select(e => new OfferHistoryEntryResponse(
            e.Id,
            ToHistoryEventType(e.AuditType),
            GetRevision(e.Metadata),
            e.Summary,
            ToActorType(e.AuditType),
            string.IsNullOrWhiteSpace(e.ActorId) ? null : CommercialOfferMapper.ToStaffActor(e.ActorId, e.ActorId),
            SanitizeMetadata(e.Metadata).TryGetValue("reasonCode", out var reasonCode) ? reasonCode : null,
            e.OccurredAt)).ToList();

        return new OfferHistoryListResponse(response, new PaginationResponse(query.Page, query.Size, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.Size)));
    }

    private static string? ToAuditType(string eventType) => eventType switch
    {
        "created" => "CommercialOfferCreated",
        "updated" => "CommercialOfferUpdated",
        "validationCreated" => "OfferValidationCreated",
        "validationInvalidated" => "OfferValidationInvalidated",
        "submitted" => "OfferSubmitted",
        "returned" => "OfferReturned",
        "deactivated" => "OfferDeactivated",
        "deleted" => "OfferDeleted",
        _ => null,
    };

    private static string ToHistoryEventType(string auditType) => auditType switch
    {
        "CommercialOfferCreated" => "created",
        "CommercialOfferUpdated" => "updated",
        "OfferValidationCreated" => "validationCreated",
        "OfferValidationInvalidated" => "validationInvalidated",
        "OfferSubmitted" => "submitted",
        "OfferReturned" => "returned",
        "OfferDeactivated" => "deactivated",
        "OfferDeleted" => "deleted",
        _ => "updated",
    };

    private static string ToActorType(string auditType) => auditType switch
    {
        "OfferReturned" => "downstreamDomain",
        "OfferDeactivated" => "system",
        _ => "staff",
    };

    private static int GetRevision(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("revision", out var revisionStr) && int.TryParse(revisionStr, out var revision))
            return revision;
        return 1;
    }

    private static IReadOnlyDictionary<string, string> SanitizeMetadata(IReadOnlyDictionary<string, string> metadata) =>
        metadata.Where(p => _safeMetadataKeys.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    private sealed record HistoryAuditProjection(Guid Id, string AuditType, DateTimeOffset OccurredAt, string ActorId, string Summary, IReadOnlyDictionary<string, string> Metadata);
}

internal sealed class ListCommercialOfferHistoryQueryValidator : AbstractValidator<ListCommercialOfferHistoryQuery>
{
    public ListCommercialOfferHistoryQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
    }
}

internal sealed class GetCommercialOfferMetricsQueryHandler(InventoryDbContext dbContext, IBusinessCalendar businessCalendar, IValidator<GetCommercialOfferMetricsQuery> validator) : IQueryHandler<GetCommercialOfferMetricsQuery, CommercialOfferMetricsResponse>
{
    public async Task<CommercialOfferMetricsResponse> HandleAsync(GetCommercialOfferMetricsQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        using var activity = InventoryTelemetry.ActivitySource.StartActivity(InventoryTelemetry.Spans.Metrics);
        activity?.SetTag("inventory.metrics.from", query.From.ToString("O"));
        activity?.SetTag("inventory.metrics.to", query.To.ToString("O"));

        var offers = dbContext.CommercialOffers.AsNoTracking()
            .Where(o => o.CreatedAt >= query.From && o.CreatedAt < query.To);

        if (!string.IsNullOrWhiteSpace(query.DestinationId))
        {
            var propertyIds = await dbContext.IncorporatedProperties.AsNoTracking()
                .Where(p => p.DestinationId == query.DestinationId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            offers = offers.Where(o => propertyIds.Contains(o.PropertyId));
        }

        var totalOffers = await offers.CountAsync(cancellationToken);

        var completeProperties = await offers.CountAsync(o => o.CompleteInformationReceivedAt.HasValue, cancellationToken);

        var submittedOffers = offers.Where(o => o.Submissions.Any());
        var totalSubmitted = await submittedOffers.CountAsync(cancellationToken);

        var returnedOffers = offers.Where(o => o.Returns.Any());
        var returnedOfferCount = await returnedOffers.CountAsync(cancellationToken);

        var firstReviewAcceptanceRate = totalSubmitted > 0
            ? (double)(totalSubmitted - returnedOfferCount) / totalSubmitted
            : 0.0;

        double submissionWithinTwoBusinessDaysRate = 0.0;
        if (totalSubmitted > 0)
        {
            var offersWithSubmission = await submittedOffers
                .Select(o => new
                {
                    o.PropertyId,
                    o.CompleteInformationReceivedAt,
                    FirstSubmittedAt = o.Submissions.OrderBy(s => s.SubmittedAt).Select(s => (DateTimeOffset?)s.SubmittedAt).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            var withinSla = offersWithSubmission.Count(o =>
                o.CompleteInformationReceivedAt.HasValue
                && o.FirstSubmittedAt.HasValue
                && o.FirstSubmittedAt.Value <= businessCalendar.AddBusinessDays(o.CompleteInformationReceivedAt.Value, 2));

            submissionWithinTwoBusinessDaysRate = (double)withinSla / totalSubmitted;
        }

        double dualValidationRate = 1.0;

        double requestsProcessedWithinFourBusinessHoursRate = 1.0;
        var propertyIdList = await offers.Select(o => o.PropertyId).Distinct().ToListAsync(cancellationToken);
        if (propertyIdList.Count > 0)
        {
            var communications = dbContext.PropertyOnboardings.AsNoTracking()
                .Where(po => propertyIdList.Contains(po.Id))
                .SelectMany(po => po.CommunicationRecords)
                .Where(c => c.ReceivedAt >= query.From && c.ReceivedAt < query.To);

            var commCount = await communications.CountAsync(cancellationToken);
            var withinCommSla = await communications.CountAsync(c => c.ProcessedWithinSla, cancellationToken);
            requestsProcessedWithinFourBusinessHoursRate = commCount > 0 ? (double)withinCommSla / commCount : 1.0;
        }

        double averageReworkCount = 0.0;
        if (totalOffers > 0)
        {
            var reworkCount = await offers.SumAsync(o => o.Returns.Count, cancellationToken);
            averageReworkCount = (double)reworkCount / totalOffers;
        }

        return new CommercialOfferMetricsResponse(
            query.From,
            query.To,
            totalOffers,
            completeProperties,
            Math.Round(firstReviewAcceptanceRate, 4),
            Math.Round(submissionWithinTwoBusinessDaysRate, 4),
            Math.Round(dualValidationRate, 4),
            Math.Round(requestsProcessedWithinFourBusinessHoursRate, 4),
            returnedOfferCount,
            Math.Round(averageReworkCount, 4));
    }
}

internal sealed class GetCommercialOfferMetricsQueryValidator : AbstractValidator<GetCommercialOfferMetricsQuery>
{
    public GetCommercialOfferMetricsQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From);
    }
}
