using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.LegalPolicies;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed record CreateCommercialPolicyCommand(
    Guid PropertyId,
    string PolicyType,
    bool IsDefault,
    int? ExpectedRevision,
    string Actor) : ICommand<CommercialPolicyResponse>;

internal sealed record SetDefaultCommercialPolicyCommand(
    Guid PropertyId,
    Guid PolicyId,
    bool UpdateExistingAccommodations,
    int? ExpectedRevision,
    string Actor) : ICommand<SetDefaultPolicyResponse>;

internal sealed record UpdateCommercialPolicyCommand(
    Guid PropertyId,
    Guid PolicyId,
    Guid ReplacementPolicyId,
    int? ExpectedRevision,
    string Actor) : ICommand<CommercialPolicyResponse>;

internal sealed record DeleteCommercialPolicyCommand(
    Guid PropertyId,
    Guid PolicyId,
    int? ExpectedRevision,
    string Actor) : ICommand<CommercialPolicyResponse>;

internal sealed record CommercialPolicyResponse(
    Guid Id,
    Guid PropertyId,
    string Type,
    string Title,
    string RulesSummary,
    string RuleSetVersion,
    string Status,
    bool IsDefault,
    int UsageCount,
    bool EverSubmitted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record SetDefaultPolicyResponse(
    Guid PolicyId,
    bool IsDefault,
    int UpdatedAccommodationCount);

internal sealed class CreateCommercialPolicyCommandHandler(
    InventoryDbContext dbContext,
    ILegalPolicyCatalog legalPolicyCatalog,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<CreateCommercialPolicyCommand> validator) : ICommandHandler<CreateCommercialPolicyCommand, CommercialPolicyResponse>
{
    public async Task<CommercialPolicyResponse> HandleAsync(
        CreateCommercialPolicyCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Policies)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var policyType = Enum.Parse<PolicyType>(command.PolicyType, true);
        var ruleSet = legalPolicyCatalog.GetCurrent(policyType);
        var now = clock.UtcNow;

        var policy = offer.AddPolicy(Guid.NewGuid(), ruleSet, command.IsDefault, command.Actor, command.ExpectedRevision, now);

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "PolicyCreated",
            $"Commercial policy '{ruleSet.Type}' created.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["policyType"] = policyType.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(policy);
    }

    private static CommercialPolicyResponse ToResponse(CommercialPolicy policy) => new(
        policy.Id,
        policy.PropertyId,
        char.ToLowerInvariant(policy.Type.ToString()[0]) + policy.Type.ToString()[1..],
        policy.Title,
        policy.RulesSummary,
        policy.RuleSetVersion,
        char.ToLowerInvariant(policy.Status.ToString()[0]) + policy.Status.ToString()[1..],
        policy.IsDefault,
        policy.UsageCount,
        policy.EverSubmitted,
        policy.CreatedAt,
        policy.UpdatedAt);
}

internal sealed class SetDefaultCommercialPolicyCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<SetDefaultCommercialPolicyCommand> validator) : ICommandHandler<SetDefaultCommercialPolicyCommand, SetDefaultPolicyResponse>
{
    public async Task<SetDefaultPolicyResponse> HandleAsync(
        SetDefaultCommercialPolicyCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Policies)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var now = clock.UtcNow;

        offer.SetDefaultPolicy(command.PolicyId, command.Actor, command.ExpectedRevision, now);

        var updatedAccommodationCount = 0;
        if (command.UpdateExistingAccommodations)
        {
            updatedAccommodationCount = await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE inventory.accommodations SET policy_id = {0} WHERE property_id = {1} AND policy_id != {0}",
                command.PolicyId,
                command.PropertyId);
        }

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "DefaultPolicySet",
            $"Default policy set to '{command.PolicyId}'.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["policyId"] = command.PolicyId.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        var policy = offer.GetPolicy(command.PolicyId);

        return new SetDefaultPolicyResponse(
            command.PolicyId,
            policy?.IsDefault ?? false,
            updatedAccommodationCount);
    }
}

internal sealed class UpdateCommercialPolicyCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<UpdateCommercialPolicyCommand> validator) : ICommandHandler<UpdateCommercialPolicyCommand, CommercialPolicyResponse>
{
    public async Task<CommercialPolicyResponse> HandleAsync(
        UpdateCommercialPolicyCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Policies)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var now = clock.UtcNow;

        offer.DeactivatePolicy(command.PolicyId, command.ReplacementPolicyId, command.Actor, command.ExpectedRevision, now);

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "PolicyDeactivated",
            $"Commercial policy '{command.PolicyId}' deactivated in favor of '{command.ReplacementPolicyId}'.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["policyId"] = command.PolicyId.ToString(),
                ["replacementPolicyId"] = command.ReplacementPolicyId.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        var policy = offer.GetPolicy(command.PolicyId)!;
        return ToResponse(policy);
    }

    private static CommercialPolicyResponse ToResponse(CommercialPolicy policy) => new(
        policy.Id,
        policy.PropertyId,
        char.ToLowerInvariant(policy.Type.ToString()[0]) + policy.Type.ToString()[1..],
        policy.Title,
        policy.RulesSummary,
        policy.RuleSetVersion,
        char.ToLowerInvariant(policy.Status.ToString()[0]) + policy.Status.ToString()[1..],
        policy.IsDefault,
        policy.UsageCount,
        policy.EverSubmitted,
        policy.CreatedAt,
        policy.UpdatedAt);
}

internal sealed class DeleteCommercialPolicyCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<DeleteCommercialPolicyCommand> validator) : ICommandHandler<DeleteCommercialPolicyCommand, CommercialPolicyResponse>
{
    public async Task<CommercialPolicyResponse> HandleAsync(
        DeleteCommercialPolicyCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Policies)
            .SingleOrDefaultAsync(o => o.Id == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var policy = offer.GetPolicy(command.PolicyId)
            ?? throw new NotFoundException("Commercial policy was not found.", "POLICY_NOT_FOUND");

        var response = new CommercialPolicyResponse(
            policy.Id,
            policy.PropertyId,
            char.ToLowerInvariant(policy.Type.ToString()[0]) + policy.Type.ToString()[1..],
            policy.Title,
            policy.RulesSummary,
            policy.RuleSetVersion,
            char.ToLowerInvariant(policy.Status.ToString()[0]) + policy.Status.ToString()[1..],
            policy.IsDefault,
            policy.UsageCount,
            policy.EverSubmitted,
            policy.CreatedAt,
            policy.UpdatedAt);

        var now = clock.UtcNow;
        offer.DeletePolicy(command.PolicyId, command.Actor, command.ExpectedRevision, now);

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            command.PropertyId.ToString(),
            command.Actor,
            "PolicyDeleted",
            $"Commercial policy '{command.PolicyId}' deleted.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = command.PropertyId.ToString(),
                ["policyId"] = command.PolicyId.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }
}
