using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;

internal sealed record AddressInput(string Street, string Number, string? Complement, string District, string City, string State, string PostalCode, string CountryCode);
internal sealed record PropertyInput(string Name, string DestinationId, AddressInput Address);
internal sealed record CreatePropertyOnboardingCommand(Guid PartnerId, string PreselectionId, PropertyInput Property, string Actor) : ICommand<PropertyOnboardingResponse>;
internal sealed record UpdatePropertyOnboardingCommand(Guid OnboardingId, string? Name, AddressInput? Address, string Actor) : ICommand<PropertyOnboardingResponse>;

internal sealed class CreatePropertyOnboardingCommandHandler(
    InventoryDbContext dbContext,
    IPartnerPreselectionValidator preselectionValidator,
    IDestinationEligibilityValidator destinationEligibilityValidator,
    IBusinessCalendar businessCalendar,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<CreatePropertyOnboardingCommand> validator) : ICommandHandler<CreatePropertyOnboardingCommand, PropertyOnboardingResponse>
{
    public async Task<PropertyOnboardingResponse> HandleAsync(CreatePropertyOnboardingCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var partner = await dbContext.Partners.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner was not found.", "PARTNER_NOT_FOUND");
        if (!string.Equals(partner.PreselectionId, command.PreselectionId, StringComparison.Ordinal))
            throw new BusinessRuleViolationException("The partner does not belong to the supplied preselection.", "PRESELECTION_MISMATCH");
        await preselectionValidator.EnsureEligibleAsync(command.PreselectionId, cancellationToken);
        await destinationEligibilityValidator.EnsureApprovedAsync(command.Property.DestinationId, cancellationToken);
        var property = PropertyOnboardingMapper.ToProperty(command.Property);
        var activeCycle = await dbContext.PropertyOnboardings.AsNoTracking().AnyAsync(item => item.PropertySimilarityKey == property.SimilarityKey && item.LifecycleStatus != OnboardingLifecycleStatus.Closed, cancellationToken);
        if (activeCycle) throw new ConflictException("An active onboarding cycle already exists for this property.", "ACTIVE_ONBOARDING_CYCLE_EXISTS");
        var now = clock.UtcNow;
        var onboarding = PropertyOnboarding.Create(Guid.NewGuid(), partner.Id, command.PreselectionId, property, now, businessCalendar.AddBusinessDays(now, 10) - now);
        var candidates = await PropertyOnboardingMapper.GetSimilarityCandidatesAsync(dbContext, onboarding, cancellationToken);
        if (candidates.Count > 0) onboarding.FlagDuplicateReviewRequired(now);
        await dbContext.PropertyOnboardings.AddAsync(onboarding, cancellationToken);
        auditWriter.Record(BusinessAuditEntry.Create("PropertyOnboarding", onboarding.Id.ToString(), command.Actor, "PropertyOnboardingCreated", "Property onboarding opened.", now, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["partnerId"] = partner.Id.ToString(), ["destinationId"] = property.DestinationId }));
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { throw new ConflictException("An active onboarding cycle already exists for this property.", "ACTIVE_ONBOARDING_CYCLE_EXISTS"); }
        return PropertyOnboardingMapper.ToResponse(onboarding, candidates);
    }

    internal static Task<bool> HasSimilarityCandidateAsync(InventoryDbContext dbContext, PropertyOnboarding onboarding, CancellationToken cancellationToken) =>
        dbContext.PropertyOnboardings.AsNoTracking().AnyAsync(item => item.Id != onboarding.Id && (item.PropertySimilarityKey == onboarding.PropertySimilarityKey || EF.Functions.ILike(item.Property.Name, onboarding.Property.Name)), cancellationToken);
}

internal sealed class UpdatePropertyOnboardingCommandHandler(
    InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor,
    IValidator<UpdatePropertyOnboardingCommand> validator) : ICommandHandler<UpdatePropertyOnboardingCommand, PropertyOnboardingResponse>
{
    public async Task<PropertyOnboardingResponse> HandleAsync(UpdatePropertyOnboardingCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var onboarding = await dbContext.PropertyOnboardings.Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).SingleOrDefaultAsync(item => item.Id == command.OnboardingId, cancellationToken)
            ?? throw new NotFoundException("Property onboarding was not found.", "PROPERTY_ONBOARDING_NOT_FOUND");
        var name = command.Name ?? onboarding.Property.Name;
        var address = command.Address is null ? onboarding.Property.Address : PropertyOnboardingMapper.ToAddress(command.Address);
        onboarding.UpdateProperty(name, address, clock.UtcNow);
        var candidates = await PropertyOnboardingMapper.GetSimilarityCandidatesAsync(dbContext, onboarding, cancellationToken);
        if (candidates.Count > 0) onboarding.FlagDuplicateReviewRequired(clock.UtcNow);
        auditWriter.Record(BusinessAuditEntry.Create("PropertyOnboarding", onboarding.Id.ToString(), command.Actor, "PropertyOnboardingUpdated", "Property onboarding registration updated.", clock.UtcNow, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["partnerId"] = onboarding.PartnerId.ToString(), ["destinationId"] = onboarding.Property.DestinationId }));
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { throw new ConflictException("An active onboarding cycle already exists for this property.", "ACTIVE_ONBOARDING_CYCLE_EXISTS"); }
        return PropertyOnboardingMapper.ToResponse(onboarding, candidates);
    }
}
