using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Observability;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed record CreateCommercialRateCommand(
    Guid PropertyId,
    Guid AccommodationId,
    string Name,
    string ConditionCode,
    long? BasePriceCents,
    int? IncludedGuests,
    long? AdditionalAdultPriceCents,
    long? AdditionalChildPriceCents,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    int? MinimumNights,
    Guid? PolicyId,
    string? MealPlan,
    int? ExpectedRevision,
    string Actor) : ICommand<CommercialRateResponse>;

internal sealed record UpdateCommercialRateCommand(
    Guid PropertyId,
    Guid AccommodationId,
    Guid RateId,
    string? Name,
    bool HasName,
    string? ConditionCode,
    bool HasConditionCode,
    long? BasePriceCents,
    bool HasBasePriceCents,
    int? IncludedGuests,
    bool HasIncludedGuests,
    long? AdditionalAdultPriceCents,
    bool HasAdditionalAdultPriceCents,
    long? AdditionalChildPriceCents,
    bool HasAdditionalChildPriceCents,
    DateOnly? ValidFrom,
    bool HasValidFrom,
    DateOnly? ValidTo,
    bool HasValidTo,
    int? MinimumNights,
    bool HasMinimumNights,
    Guid? PolicyId,
    bool HasPolicyId,
    string? MealPlan,
    bool HasMealPlan,
    string? DeactivationReason,
    bool HasDeactivationReason,
    int? ExpectedRevision,
    string Actor) : ICommand<CommercialRateResponse>;

internal sealed record DeleteCommercialRateCommand(
    Guid PropertyId,
    Guid AccommodationId,
    Guid RateId,
    int? ExpectedRevision,
    string Actor) : ICommand<CommercialRateResponse>;

internal sealed record CommercialRateResponse(
    Guid Id,
    Guid AccommodationId,
    Guid PropertyId,
    string Name,
    string ConditionCode,
    long? BasePriceCents,
    int? IncludedGuests,
    long? AdditionalAdultPriceCents,
    long? AdditionalChildPriceCents,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    int? MinimumNights,
    Guid? PolicyId,
    string? MealPlan,
    string Status,
    string? DeactivationReason,
    bool EverSubmitted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed class CreateCommercialRateCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<CreateCommercialRateCommand> validator) : ICommandHandler<CreateCommercialRateCommand, CommercialRateResponse>
{
    public async Task<CommercialRateResponse> HandleAsync(
        CreateCommercialRateCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Accommodations)
            .Include(o => o.Rates)
            .Include(o => o.Policies)
            .Include(o => o.CurrentValidation)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var now = clock.UtcNow;

        var defaultPolicyId = offer.Policies
            .FirstOrDefault(policy => policy.IsDefault && policy.Status == PolicyStatus.Active)?.Id;
        var accommodation = offer.GetAccommodation(command.AccommodationId)
            ?? throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

        Domain.CommercialOffers.MealPlan? mealPlan = command.MealPlan is not null
            ? Enum.Parse<Domain.CommercialOffers.MealPlan>(command.MealPlan, true)
            : null;

        var rate = offer.AddRate(
            Guid.NewGuid(),
            command.AccommodationId,
            command.Name,
            command.ConditionCode,
            command.BasePriceCents,
            command.IncludedGuests ?? accommodation.MaxAdults,
            command.AdditionalAdultPriceCents,
            command.AdditionalChildPriceCents,
            command.ValidFrom,
            command.ValidTo,
            command.MinimumNights,
            command.PolicyId ?? defaultPolicyId,
            mealPlan,
            command.Actor,
            command.ExpectedRevision,
            now);

        var overlapping = offer.GetOverlappingRates(rate);
        if (overlapping.Count > 0)
        {
            InventoryTelemetry.OfferRateOverlap.Add(1, new KeyValuePair<string, object?>("operation", "create"));
            throw new BusinessRuleViolationException(
                $"Rate period overlaps with {overlapping.Count} existing rate(s) for the same accommodation, conditionCode, policy and mealPlan.",
                "RATE_PERIOD_OVERLAP");
        }

        offer.RecalculateCompletenessFromAccommodations(now);

        if (offer.CurrentValidation is not null)
        {
            InventoryTelemetry.OfferValidationInvalidated.Add(1);
        }

        InventoryTelemetry.OfferMutation.Add(1, new KeyValuePair<string, object?>("operation", "rate_created"));

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "CommercialRateCreated",
            $"Commercial rate '{command.Name}' created.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["accommodationId"] = command.AccommodationId.ToString(),
                ["rateId"] = rate.Id.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(rate);
    }

    private static CommercialRateResponse ToResponse(CommercialRate rate) => new(
        rate.Id,
        rate.AccommodationId,
        rate.PropertyId,
        rate.Name,
        rate.ConditionCode,
        rate.BasePriceCents,
        rate.IncludedGuests,
        rate.AdditionalAdultPriceCents,
        rate.AdditionalChildPriceCents,
        rate.ValidFrom,
        rate.ValidTo,
        rate.MinimumNights,
        rate.PolicyId,
        rate.MealPlan?.ToString(),
        rate.Status.ToString().ToLowerInvariant(),
        rate.DeactivationReason,
        rate.EverSubmitted,
        rate.CreatedAt,
        rate.UpdatedAt);
}

internal sealed class UpdateCommercialRateCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<UpdateCommercialRateCommand> validator) : ICommandHandler<UpdateCommercialRateCommand, CommercialRateResponse>
{
    public async Task<CommercialRateResponse> HandleAsync(
        UpdateCommercialRateCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Accommodations)
            .Include(o => o.Rates)
            .Include(o => o.Policies)
            .Include(o => o.CurrentValidation)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var now = clock.UtcNow;

        offer.UpdateRate(
            command.RateId,
            command.Name,
            command.HasName,
            command.ConditionCode,
            command.HasConditionCode,
            command.BasePriceCents,
            command.HasBasePriceCents,
            command.IncludedGuests,
            command.HasIncludedGuests,
            command.AdditionalAdultPriceCents,
            command.HasAdditionalAdultPriceCents,
            command.AdditionalChildPriceCents,
            command.HasAdditionalChildPriceCents,
            command.ValidFrom,
            command.HasValidFrom,
            command.ValidTo,
            command.HasValidTo,
            command.MinimumNights,
            command.HasMinimumNights,
            command.PolicyId,
            command.HasPolicyId,
            command.MealPlan,
            command.HasMealPlan,
            command.DeactivationReason,
            command.HasDeactivationReason,
            command.Actor,
            command.ExpectedRevision,
            now);

        var rate = offer.GetRate(command.RateId)
            ?? throw new NotFoundException("Commercial rate was not found.", "RATE_NOT_FOUND");

        var overlapping = offer.GetOverlappingRates(rate);
        if (overlapping.Count > 0)
        {
            InventoryTelemetry.OfferRateOverlap.Add(1, new KeyValuePair<string, object?>("operation", "update"));
            throw new BusinessRuleViolationException(
                $"Rate period overlaps with {overlapping.Count} existing rate(s) for the same accommodation, conditionCode, policy and mealPlan.",
                "RATE_PERIOD_OVERLAP");
        }

        offer.RecalculateCompletenessFromAccommodations(now);

        if (offer.CurrentValidation is not null)
        {
            InventoryTelemetry.OfferValidationInvalidated.Add(1);
        }

        InventoryTelemetry.OfferMutation.Add(1, new KeyValuePair<string, object?>("operation", "rate_updated"));

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "CommercialRateUpdated",
            $"Commercial rate '{rate.Name}' updated.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["accommodationId"] = command.AccommodationId.ToString(),
                ["rateId"] = rate.Id.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(rate);
    }

    private static CommercialRateResponse ToResponse(CommercialRate rate) => new(
        rate.Id,
        rate.AccommodationId,
        rate.PropertyId,
        rate.Name,
        rate.ConditionCode,
        rate.BasePriceCents,
        rate.IncludedGuests,
        rate.AdditionalAdultPriceCents,
        rate.AdditionalChildPriceCents,
        rate.ValidFrom,
        rate.ValidTo,
        rate.MinimumNights,
        rate.PolicyId,
        rate.MealPlan?.ToString(),
        rate.Status.ToString().ToLowerInvariant(),
        rate.DeactivationReason,
        rate.EverSubmitted,
        rate.CreatedAt,
        rate.UpdatedAt);
}

internal sealed class DeleteCommercialRateCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<DeleteCommercialRateCommand> validator) : ICommandHandler<DeleteCommercialRateCommand, CommercialRateResponse>
{
    public async Task<CommercialRateResponse> HandleAsync(
        DeleteCommercialRateCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Accommodations)
            .Include(o => o.Rates)
            .Include(o => o.Policies)
            .Include(o => o.CurrentValidation)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var rate = offer.GetRate(command.RateId)
            ?? throw new NotFoundException("Commercial rate was not found.", "RATE_NOT_FOUND");

        var response = new CommercialRateResponse(
            rate.Id,
            rate.AccommodationId,
            rate.PropertyId,
            rate.Name,
            rate.ConditionCode,
            rate.BasePriceCents,
            rate.IncludedGuests,
            rate.AdditionalAdultPriceCents,
            rate.AdditionalChildPriceCents,
            rate.ValidFrom,
            rate.ValidTo,
            rate.MinimumNights,
            rate.PolicyId,
            rate.MealPlan?.ToString(),
            rate.Status.ToString().ToLowerInvariant(),
            rate.DeactivationReason,
            rate.EverSubmitted,
            rate.CreatedAt,
            rate.UpdatedAt);

        var now = clock.UtcNow;

        offer.DeleteRate(command.RateId, command.Actor, command.ExpectedRevision, now);

        offer.RecalculateCompletenessFromAccommodations(now);

        if (offer.CurrentValidation is not null)
        {
            InventoryTelemetry.OfferValidationInvalidated.Add(1);
        }

        InventoryTelemetry.OfferMutation.Add(1, new KeyValuePair<string, object?>("operation", "rate_deleted"));

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "CommercialRateDeleted",
            $"Commercial rate '{rate.Name}' deleted.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["accommodationId"] = command.AccommodationId.ToString(),
                ["rateId"] = command.RateId.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }
}
