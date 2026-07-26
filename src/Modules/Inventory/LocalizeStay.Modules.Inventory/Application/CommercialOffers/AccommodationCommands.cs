using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.LegalPolicies;
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

internal sealed record PatchAccommodationChildAgeRange(Guid PropertyId, Accommodation? AccommodationEntry);

internal sealed record CreateAccommodationCommand(
    Guid PropertyId,
    string CommercialName,
    int? MaxAdults,
    int? MaxChildren,
    int? TotalCapacity,
    List<BedEntryInput>? BedConfiguration,
    string? MealPlan,
    ChildAgeRangeInput? ChildAgeRange,
    List<string>? StructuralFeatures,
    Guid? PolicyId,
    int? ExpectedRevision,
    string Actor) : ICommand<AccommodationResponse>;

internal sealed record UpdateAccommodationCommand(
    Guid PropertyId,
    Guid AccommodationId,
    string? CommercialName,
    bool HasCommercialName,
    int? MaxAdults,
    bool HasMaxAdults,
    int? MaxChildren,
    bool HasMaxChildren,
    int? TotalCapacity,
    bool HasTotalCapacity,
    string? MealPlan,
    bool HasMealPlan,
    List<BedEntryInput>? BedConfiguration,
    bool HasBedConfiguration,
    List<string>? StructuralFeatures,
    bool HasStructuralFeatures,
    Guid? PolicyId,
    bool HasPolicyId,
    ChildAgeRangeUpdateInput? ChildAgeRange,
    int? ExpectedRevision,
    string Actor) : ICommand<AccommodationResponse>;

internal sealed record DeleteAccommodationCommand(
    Guid PropertyId,
    Guid AccommodationId,
    int? ExpectedRevision,
    string Actor) : ICommand<AccommodationResponse>;

internal sealed record AccommodationResponse(
    Guid Id,
    Guid PropertyId,
    string CommercialName,
    string Status,
    bool EverSubmitted,
    string? DeactivationReason,
    int? MaxAdults,
    int? MaxChildren,
    int? TotalCapacity,
    List<BedEntryDto> BedConfiguration,
    string? MealPlan,
    string ChildAgeRangeSource,
    int? ChildMinimumAge,
    int? ChildMaximumAge,
    List<string> StructuralFeatures,
    Guid? PolicyId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record BedEntryInput(string BedType, int Count);
internal sealed record BedEntryDto(string Type, int Count);
internal sealed record ChildAgeRangeUpdateInput(int? MinimumAge, int? MaximumAge, bool IsNull);
internal sealed record ChildAgeRangeInput(int? MinimumAge, int? MaximumAge);

internal sealed class CreateAccommodationCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<CreateAccommodationCommand> validator) : ICommandHandler<CreateAccommodationCommand, AccommodationResponse>
{
    public async Task<AccommodationResponse> HandleAsync(
        CreateAccommodationCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Accommodations)
            .Include(o => o.Rates)
            .Include(o => o.Policies)
            .Include(o => o.CurrentValidation)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var now = clock.UtcNow;

        var defaultPolicyId = offer.Policies.FirstOrDefault(p => p.IsDefault && p.Status == PolicyStatus.Active)?.Id;
        var policyId = command.PolicyId ?? defaultPolicyId;

        var propertyDefaultChildAgeRange = offer.GetDefaultChildAgeRange();

        var bedEntries = MapBedEntries(command.BedConfiguration);

        var accommodation = offer.AddAccommodation(
            Guid.NewGuid(),
            command.CommercialName,
            policyId,
            propertyDefaultChildAgeRange,
            command.Actor,
            command.ExpectedRevision,
            now);

        if (command.MaxAdults.HasValue || command.MaxChildren.HasValue || command.TotalCapacity.HasValue)
        {
            accommodation.SetOccupancy(command.MaxAdults, command.MaxChildren, command.TotalCapacity);
        }

        if (bedEntries.Count > 0)
        {
            accommodation.SetBedConfiguration(bedEntries);
        }

        if (command.MealPlan is not null)
        {
            accommodation.SetMealPlan(Enum.Parse<MealPlan>(command.MealPlan, true));
        }

        if (command.ChildAgeRange is not null)
        {
            var range = ChildAgeRange.Create(
                command.ChildAgeRange.MinimumAge ?? 0,
                command.ChildAgeRange.MaximumAge ?? 17);
            accommodation.SetChildAgeRangeOverride(range);
        }

        if (command.StructuralFeatures is { Count: > 0 })
        {
            accommodation.SetStructuralFeatures(command.StructuralFeatures);
        }

        offer.RecalculateCompletenessFromAccommodations(now);

        if (offer.CurrentValidation is not null)
        {
            InventoryTelemetry.OfferValidationInvalidated.Add(1);
        }

        InventoryTelemetry.OfferMutation.Add(1, new KeyValuePair<string, object?>("operation", "accommodation_created"));

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "AccommodationCreated",
            $"Accommodation '{command.CommercialName}' created.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["accommodationId"] = accommodation.Id.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(accommodation);
    }

    private static AccommodationResponse ToResponse(Accommodation accommodation) => new(
        accommodation.Id,
        accommodation.PropertyId,
        accommodation.CommercialName,
        accommodation.Status.ToString().ToLowerInvariant(),
        accommodation.EverSubmitted,
        accommodation.DeactivationReason,
        accommodation.MaxAdults,
        accommodation.MaxChildren,
        accommodation.TotalCapacity,
        accommodation.BedConfiguration.Select(b => new BedEntryDto(b.Type.ToString().ToLowerInvariant(), b.Count)).ToList(),
        accommodation.MealPlan?.ToString(),
        accommodation.ChildAgeRangeSource.ToString(),
        accommodation.ChildMinimumAge,
        accommodation.ChildMaximumAge,
        accommodation.StructuralFeatures.ToList(),
        accommodation.PolicyId,
        accommodation.CreatedAt,
        accommodation.UpdatedAt);

    private static List<BedEntry> MapBedEntries(List<BedEntryInput>? inputs)
    {
        if (inputs is not { Count: > 0 })
            return [];

        return inputs.Select(i =>
        {
            var bedType = Enum.Parse<BedType>(i.BedType, true);
            return BedEntry.Create(bedType, i.Count);
        }).ToList();
    }
}

internal sealed class UpdateAccommodationCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<UpdateAccommodationCommand> validator) : ICommandHandler<UpdateAccommodationCommand, AccommodationResponse>
{
    public async Task<AccommodationResponse> HandleAsync(
        UpdateAccommodationCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Accommodations)
            .Include(o => o.Rates)
            .Include(o => o.Policies)
            .Include(o => o.Submissions)
            .Include(o => o.CurrentValidation)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var accommodation = offer.GetAccommodation(command.AccommodationId)
            ?? throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

        var now = clock.UtcNow;
        var propertyDefaultChildAgeRange = offer.GetDefaultChildAgeRange();

        offer.UpdateAccommodation(accommodation.Id, command.Actor, command.ExpectedRevision, now, acc =>
        {
            if (command.HasCommercialName && command.CommercialName is not null)
            {
                acc.UpdateCommercialName(command.CommercialName);
            }

            if (command.HasMaxAdults || command.HasMaxChildren || command.HasTotalCapacity)
            {
                int? adults = command.HasMaxAdults ? command.MaxAdults : acc.MaxAdults;
                int? children = command.HasMaxChildren ? command.MaxChildren : acc.MaxChildren;
                int? capacity = command.HasTotalCapacity ? command.TotalCapacity : acc.TotalCapacity;
                acc.SetOccupancy(adults, children, capacity);
            }

            if (command.HasBedConfiguration)
            {
                var bedEntries = command.BedConfiguration is { Count: > 0 }
                    ? command.BedConfiguration.Select(i =>
                    {
                        var bedType = Enum.Parse<BedType>(i.BedType, true);
                        return BedEntry.Create(bedType, i.Count);
                    }).ToList()
                    : new List<BedEntry>();
                acc.SetBedConfiguration(bedEntries);
            }

            if (command.HasMealPlan)
            {
                acc.SetMealPlan(command.MealPlan is not null
                    ? Enum.Parse<MealPlan>(command.MealPlan, true)
                    : null);
            }

            if (command.ChildAgeRange is not null)
            {
                if (command.ChildAgeRange.IsNull)
                {
                    acc.RevertChildAgeRangeToPropertyDefault(propertyDefaultChildAgeRange);
                }
                else
                {
                    var range = ChildAgeRange.Create(
                        command.ChildAgeRange.MinimumAge ?? 0,
                        command.ChildAgeRange.MaximumAge ?? 17);
                    acc.SetChildAgeRangeOverride(range);
                }
            }

            if (command.HasStructuralFeatures)
            {
                acc.SetStructuralFeatures(command.StructuralFeatures ?? []);
            }

            if (command.HasPolicyId)
            {
                if (command.PolicyId.HasValue)
                {
                    var policy = offer.Policies.SingleOrDefault(p => p.Id == command.PolicyId.Value && p.Status == PolicyStatus.Active)
                        ?? throw new BusinessRuleViolationException(
                            "The specified policy is not active or does not belong to this property.",
                            "POLICY_NOT_FOUND");
                }
                acc.SetPolicy(command.PolicyId);
            }
        });

        offer.RecalculateCompletenessFromAccommodations(now);

        if (offer.CurrentValidation is not null)
        {
            InventoryTelemetry.OfferValidationInvalidated.Add(1);
        }

        InventoryTelemetry.OfferMutation.Add(1, new KeyValuePair<string, object?>("operation", "accommodation_updated"));

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "AccommodationUpdated",
            $"Accommodation '{accommodation.CommercialName}' updated.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["accommodationId"] = accommodation.Id.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccommodationResponse(
            accommodation.Id,
            accommodation.PropertyId,
            accommodation.CommercialName,
            accommodation.Status.ToString().ToLowerInvariant(),
            accommodation.EverSubmitted,
            accommodation.DeactivationReason,
            accommodation.MaxAdults,
            accommodation.MaxChildren,
            accommodation.TotalCapacity,
            accommodation.BedConfiguration.Select(b => new BedEntryDto(b.Type.ToString().ToLowerInvariant(), b.Count)).ToList(),
            accommodation.MealPlan?.ToString(),
            accommodation.ChildAgeRangeSource.ToString(),
            accommodation.ChildMinimumAge,
            accommodation.ChildMaximumAge,
            accommodation.StructuralFeatures.ToList(),
            accommodation.PolicyId,
            accommodation.CreatedAt,
            accommodation.UpdatedAt);
    }
}

internal sealed class DeleteAccommodationCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<DeleteAccommodationCommand> validator) : ICommandHandler<DeleteAccommodationCommand, AccommodationResponse>
{
    public async Task<AccommodationResponse> HandleAsync(
        DeleteAccommodationCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Accommodations)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var accommodation = offer.GetAccommodation(command.AccommodationId)
            ?? throw new NotFoundException("Accommodation was not found.", "ACCOMMODATION_NOT_FOUND");

        var response = new AccommodationResponse(
            accommodation.Id,
            accommodation.PropertyId,
            accommodation.CommercialName,
            accommodation.Status.ToString().ToLowerInvariant(),
            accommodation.EverSubmitted,
            accommodation.DeactivationReason,
            accommodation.MaxAdults,
            accommodation.MaxChildren,
            accommodation.TotalCapacity,
            accommodation.BedConfiguration.Select(b => new BedEntryDto(b.Type.ToString().ToLowerInvariant(), b.Count)).ToList(),
            accommodation.MealPlan?.ToString(),
            accommodation.ChildAgeRangeSource.ToString(),
            accommodation.ChildMinimumAge,
            accommodation.ChildMaximumAge,
            accommodation.StructuralFeatures.ToList(),
            accommodation.PolicyId,
            accommodation.CreatedAt,
            accommodation.UpdatedAt);

        var now = clock.UtcNow;
        offer.DeleteAccommodation(command.AccommodationId, command.Actor, command.ExpectedRevision, now);

        offer.RecalculateCompletenessFromAccommodations(now);

        if (offer.CurrentValidation is not null)
        {
            InventoryTelemetry.OfferValidationInvalidated.Add(1);
        }

        InventoryTelemetry.OfferMutation.Add(1, new KeyValuePair<string, object?>("operation", "accommodation_deleted"));

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "AccommodationDeleted",
            $"Accommodation '{command.AccommodationId}' deleted.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["accommodationId"] = command.AccommodationId.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }
}
