using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.Partners;

internal sealed record CreatePartnerCommand(
    string PreselectionId,
    string LegalName,
    string? TradeName,
    LegalIdentifierInput LegalIdentifier,
    ContactInput PrimaryContact,
    string Actor) : ICommand<PartnerResponse>;

internal sealed record UpdatePartnerCommand(
    Guid PartnerId,
    string? LegalName,
    bool HasTradeName,
    string? TradeName,
    LegalIdentifierInput? LegalIdentifier,
    ContactInput? PrimaryContact,
    string Actor) : ICommand<PartnerResponse>;

internal sealed record LegalIdentifierInput(string Type, string CountryCode, string Value);
internal sealed record ContactInput(string Name, string Email, string Phone);

internal sealed class CreatePartnerCommandHandler(
    InventoryDbContext dbContext,
    IPartnerPreselectionValidator preselectionValidator,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<CreatePartnerCommand> validator) : ICommandHandler<CreatePartnerCommand, PartnerResponse>
{
    public async Task<PartnerResponse> HandleAsync(CreatePartnerCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await preselectionValidator.EnsureEligibleAsync(command.PreselectionId, cancellationToken);
        var identifier = PartnerMapper.ToLegalIdentifier(command.LegalIdentifier);
        await PartnerConflictGuard.EnsureAvailableAsync(dbContext, identifier, null, cancellationToken);
        var now = clock.UtcNow;
        var partner = Partner.Create(Guid.NewGuid(), command.PreselectionId, command.LegalName, command.TradeName, identifier, PartnerMapper.ToContact(command.PrimaryContact), now);
        await dbContext.Partners.AddAsync(partner, cancellationToken);
        auditWriter.Record(BusinessAuditEntry.Create("Partner", partner.Id.ToString(), command.Actor, "PartnerCreated", "Partner registration created.", now, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["partnerId"] = partner.Id.ToString() }));
        await SaveWithDuplicateConflictAsync(dbContext, identifier, partner.Id, cancellationToken);
        return PartnerMapper.ToResponse(partner);
    }

    private static async Task SaveWithDuplicateConflictAsync(InventoryDbContext dbContext, LegalIdentifier identifier, Guid partnerId, CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            await PartnerConflictGuard.EnsureAvailableAsync(dbContext, identifier, partnerId, cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePartnerCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<UpdatePartnerCommand> validator) : ICommandHandler<UpdatePartnerCommand, PartnerResponse>
{
    public async Task<PartnerResponse> HandleAsync(UpdatePartnerCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var partner = await dbContext.Partners.SingleOrDefaultAsync(item => item.Id == command.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner was not found.", "PARTNER_NOT_FOUND");
        var now = clock.UtcNow;
        if (command.LegalName is not null) partner.UpdateLegalName(command.LegalName, now);
        if (command.HasTradeName) partner.UpdateTradeName(command.TradeName, now);
        if (command.PrimaryContact is not null) partner.UpdatePrimaryContact(PartnerMapper.ToContact(command.PrimaryContact), now);
        if (command.LegalIdentifier is not null)
        {
            var identifier = PartnerMapper.ToLegalIdentifier(command.LegalIdentifier);
            await PartnerConflictGuard.EnsureAvailableAsync(dbContext, identifier, partner.Id, cancellationToken);
            partner.ChangeLegalIdentifier(identifier, now);
        }
        auditWriter.Record(BusinessAuditEntry.Create("Partner", partner.Id.ToString(), command.Actor, "PartnerUpdated", "Partner registration updated.", now, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["partnerId"] = partner.Id.ToString() }));
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) when (command.LegalIdentifier is not null)
        {
            dbContext.ChangeTracker.Clear();
            var identifier = PartnerMapper.ToLegalIdentifier(command.LegalIdentifier);
            await PartnerConflictGuard.EnsureAvailableAsync(dbContext, identifier, partner.Id, cancellationToken);
            throw;
        }
        return PartnerMapper.ToResponse(partner);
    }
}

internal static class PartnerConflictGuard
{
    internal static async Task EnsureAvailableAsync(InventoryDbContext dbContext, LegalIdentifier identifier, Guid? currentPartnerId, CancellationToken cancellationToken)
    {
        var conflict = await dbContext.Partners.AsNoTracking().FirstOrDefaultAsync(partner => partner.LegalIdentifier.CountryCode == identifier.CountryCode && partner.LegalIdentifier.Type == identifier.Type && partner.LegalIdentifier.NormalizedValue == identifier.NormalizedValue && (!currentPartnerId.HasValue || partner.Id != currentPartnerId), cancellationToken);
        if (conflict is not null) throw new ConflictException("A partner already uses this legal identifier.", "DUPLICATE_LEGAL_IDENTIFIER", conflict.Id);
    }
}
