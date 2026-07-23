using LocalizeStay.SharedKernel.Cqrs;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed record StaffActorResponse(string Id, string DisplayName);

internal sealed record PaginationResponse(int Page, int Size, int Total, int TotalPages);

internal sealed record ChildAgeRangeResponse(int MinAgeInclusive, int MaxAgeInclusive);

internal sealed record BedConfigurationItemResponse(string Type, int Quantity);

internal sealed record PendingIssueResponse(string Code, string Message, string Severity, string ResourceType, string? ResourceId, string? Field);

internal sealed record OfferValidationResponse(Guid Id, Guid PropertyId, int Revision, string Status, StaffActorResponse ValidatedBy, DateTimeOffset ValidatedAt, DateTimeOffset? InvalidatedAt, string? InvalidationReason, string? Comment);

internal sealed record OfferReturnResponse(Guid Id, Guid SubmissionId, string ReasonCode, string Reason, string ReturnedByDomain, DateTimeOffset ReturnedAt);

internal sealed record OfferSubmissionResponse(Guid Id, Guid PropertyId, int Revision, Guid ValidationId, string Status, string EventName, StaffActorResponse SubmittedBy, DateTimeOffset SubmittedAt);

internal sealed record CommercialOfferSummaryDto(Guid PropertyId, string PropertyName, string? DestinationId, string Status, int Revision, int CompletenessPercentage, int BlockingIssueCount, int AccommodationCount, int CompleteAccommodationCount, bool EverSubmitted, StaffActorResponse AuthoredBy, DateTimeOffset? CompleteInformationReceivedAt, DateTimeOffset? TargetSubmissionAt, DateTimeOffset? LastSubmittedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

internal sealed record CommercialOfferDetailDto(Guid PropertyId, string PropertyName, string? DestinationId, string Status, int Revision, int CompletenessPercentage, int BlockingIssueCount, int AccommodationCount, int CompleteAccommodationCount, bool EverSubmitted, StaffActorResponse AuthoredBy, DateTimeOffset? CompleteInformationReceivedAt, DateTimeOffset? TargetSubmissionAt, DateTimeOffset? LastSubmittedAt, Guid? DefaultPolicyId, IReadOnlyList<CommercialPolicyDto> Policies, IReadOnlyList<AccommodationDto> Accommodations, IReadOnlyList<PendingIssueResponse> PendingIssues, OfferValidationResponse? CurrentValidation, OfferReturnResponse? LatestReturn, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

internal sealed record CommercialOfferListResponse(IReadOnlyList<CommercialOfferSummaryDto> Data, PaginationResponse Pagination);

internal sealed record CommercialPolicyDto(Guid Id, Guid PropertyId, string Type, string Title, string RulesSummary, string RuleSetVersion, bool IsDefault, string Status, int UsageCount, bool EverSubmitted, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

internal sealed record CommercialPolicyListResponse(IReadOnlyList<CommercialPolicyDto> Data);

internal sealed record AccommodationDto(Guid Id, Guid PropertyId, string CommercialName, string? Category, IReadOnlyList<BedConfigurationItemResponse> BedConfiguration, IReadOnlyList<string> StructuralFeatures, int? TotalCapacity, int? MaxAdults, int? MaxChildren, ChildAgeRangeResponse? ChildAgeRange, string ChildAgeRangeSource, Guid? PolicyId, string Status, string? DeactivationReason, int CompletenessPercentage, IReadOnlyList<PendingIssueResponse> PendingIssues, int RateCount, int ActiveRateCount, bool EverSubmitted, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

internal sealed record AccommodationListResponse(IReadOnlyList<AccommodationDto> Data, PaginationResponse Pagination);

internal sealed record CommercialRateDto(Guid Id, Guid AccommodationId, string Name, string ConditionCode, long? BasePriceCents, int? IncludedGuests, long? AdditionalAdultPriceCents, long? AdditionalChildPriceCents, string? ValidFrom, string? ValidTo, int? MinimumNights, Guid? PolicyId, string? MealPlan, string Currency, bool MandatoryFeesIncluded, string Status, string? DeactivationReason, int CompletenessPercentage, IReadOnlyList<PendingIssueResponse> PendingIssues, bool EverSubmitted, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

internal sealed record CommercialRateListResponse(IReadOnlyList<CommercialRateDto> Data, PaginationResponse Pagination);

internal sealed record OfferHistoryEntryResponse(Guid Id, string EventType, int Revision, string Summary, string ActorType, StaffActorResponse? Actor, string? Reason, DateTimeOffset OccurredAt);

internal sealed record OfferHistoryListResponse(IReadOnlyList<OfferHistoryEntryResponse> Data, PaginationResponse Pagination);

internal sealed record CommercialOfferMetricsResponse(DateTimeOffset From, DateTimeOffset To, int TotalOffers, int CompleteProperties, double FirstReviewAcceptanceRate, double SubmissionWithinTwoBusinessDaysRate, double DualValidationRate, double RequestsProcessedWithinFourBusinessHoursRate, int ReturnedOfferCount, double AverageReworkCount);

internal sealed record ListCommercialOffersQuery(int Page, int Size, Guid? PropertyId, string? Status, bool? HasBlockingIssues, bool? Overdue, string? Sort, string? Order) : IQuery<CommercialOfferListResponse>;

internal sealed record GetCommercialOfferQuery(Guid PropertyId) : IQuery<CommercialOfferDetailDto>;

internal sealed record ListCommercialPoliciesQuery(Guid PropertyId, string? Status) : IQuery<CommercialPolicyListResponse>;

internal sealed record ListAccommodationsQuery(Guid PropertyId, int Page, int Size, string? Status, string? Completeness, string? Sort, string? Order) : IQuery<AccommodationListResponse>;

internal sealed record GetAccommodationQuery(Guid PropertyId, Guid AccommodationId) : IQuery<AccommodationDto>;

internal sealed record ListCommercialRatesQuery(Guid PropertyId, Guid AccommodationId, int Page, int Size, string? Status, string? ActiveOn, string? ValidFrom, string? ValidTo, string? Sort, string? Order) : IQuery<CommercialRateListResponse>;

internal sealed record ListCommercialOfferHistoryQuery(Guid PropertyId, int Page, int Size, string? EventType) : IQuery<OfferHistoryListResponse>;

internal sealed record GetCommercialOfferMetricsQuery(DateTimeOffset From, DateTimeOffset To, string? DestinationId) : IQuery<CommercialOfferMetricsResponse>;
