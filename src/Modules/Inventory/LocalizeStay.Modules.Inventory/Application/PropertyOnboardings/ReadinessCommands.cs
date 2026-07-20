using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Observability;
using LocalizeStay.Modules.Inventory.Application.Partners;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;

internal sealed record EvidenceReferenceInput(string Kind, string Reference, string Description);
internal sealed record ContractReferenceInput(string RepositoryReference, string? ContractNumber, DateTimeOffset SignedAt, IReadOnlyList<string> ResponsibleParties);
internal sealed record OperationalChannelTestInput(string Channel, string Contact, DateTimeOffset TestedAt, string ResultSummary);
internal sealed record UpdateReadinessGateCommand(Guid OnboardingId, string GateType, string Status, string? Notes, IReadOnlyList<EvidenceReferenceInput>? Evidence, ContractReferenceInput? ContractReference, ContactInput? AuthorizedContact, OperationalChannelTestInput? OperationalChannelTest, string Actor) : ICommand<PropertyOnboardingResponse>;
internal sealed record CreatePendingIssueCommand(Guid OnboardingId, string Description, string OwnerType, string? AssigneeId, string? RelatedGateType, DateTimeOffset? TargetAt, string Actor) : ICommand<PendingIssueResponse>;
internal sealed record UpdatePendingIssueCommand(Guid OnboardingId, Guid PendingIssueId, string? Description, string? OwnerType, string? AssigneeId, bool HasAssigneeId, DateTimeOffset? TargetAt, bool HasTargetAt, string? Status, string? ResolutionNote, string Actor) : ICommand<PendingIssueResponse>;
internal sealed record CreateCommunicationRecordCommand(Guid OnboardingId, string Channel, DateTimeOffset ReceivedAt, DateTimeOffset ProcessedAt, string ResultSummary, string Actor) : ICommand<CommunicationRecordResponse>;

internal abstract class ReadinessCommandHandlerBase(InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor)
{
    protected async Task<PropertyOnboarding> LoadAsync(Guid onboardingId, CancellationToken cancellationToken) =>
        await dbContext.PropertyOnboardings.Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.CommunicationRecords).Include(item => item.DuplicateReviews).SingleOrDefaultAsync(item => item.Id == onboardingId, cancellationToken)
        ?? throw new NotFoundException("Property onboarding was not found.", "PROPERTY_ONBOARDING_NOT_FOUND");

    protected void Audit(PropertyOnboarding onboarding, string actor, string type, string summary, DateTimeOffset occurredAt, IReadOnlyDictionary<string, string>? metadata = null) =>
        auditWriter.Record(BusinessAuditEntry.Create("PropertyOnboarding", onboarding.Id.ToString(), actor, type, summary, occurredAt, correlationIdAccessor.CorrelationId, metadata));

    protected Task SaveAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
    protected DateTimeOffset Now => clock.UtcNow;
}

internal sealed class UpdateReadinessGateCommandHandler(InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor, IValidator<UpdateReadinessGateCommand> validator)
    : ReadinessCommandHandlerBase(dbContext, auditWriter, clock, correlationIdAccessor), ICommandHandler<UpdateReadinessGateCommand, PropertyOnboardingResponse>
{
    public async Task<PropertyOnboardingResponse> HandleAsync(UpdateReadinessGateCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var onboarding = await LoadAsync(command.OnboardingId, cancellationToken);
        var type = Enum.Parse<ReadinessGateType>(command.GateType, true);
        var status = Enum.Parse<ReadinessGateStatus>(command.Status, true);
        var now = Now;
        switch (status)
        {
            case ReadinessGateStatus.Validated:
                try { onboarding.ValidateGate(type, BuildEvidence(command, type), ToContractReference(command.ContractReference), command.Actor, now); }
                catch (ArgumentException exception) { throw new BusinessRuleViolationException(exception.Message, "INVALID_GATE_EVIDENCE"); }
                break;
            case ReadinessGateStatus.Rejected:
                onboarding.RejectGate(type, command.Notes!, now);
                break;
            default:
                onboarding.ResetGateToPending(type, now);
                break;
        }
        Audit(onboarding, command.Actor, "GateUpdated", "Readiness gate updated.", now, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["gateType"] = command.GateType });
        await SaveAsync(cancellationToken);
        InventoryTelemetry.Gates.Add(1, new KeyValuePair<string, object?>("gateType", command.GateType), new KeyValuePair<string, object?>("result", command.Status));
        return PropertyOnboardingMapper.ToResponse(onboarding);
    }

    private static IReadOnlyList<EvidenceReference> BuildEvidence(UpdateReadinessGateCommand command, ReadinessGateType type)
    {
        if (type == ReadinessGateType.SignedContract && command.ContractReference is null || type == ReadinessGateType.AuthorizedContact && command.AuthorizedContact is null || type == ReadinessGateType.OperationalChannel && command.OperationalChannelTest is null)
            throw new BusinessRuleViolationException("The validated gate is missing its required evidence.", "INVALID_GATE_EVIDENCE");
        var evidence = (command.Evidence ?? []).Select(item => new EvidenceReference(Enum.Parse<EvidenceKind>(item.Kind, true), item.Reference, item.Description)).ToList();
        if (type == ReadinessGateType.SignedContract && command.ContractReference is not null)
            evidence.Add(new EvidenceReference(EvidenceKind.Contract, command.ContractReference.RepositoryReference, "Signed contract reference."));
        if (type == ReadinessGateType.OperationalChannel && command.OperationalChannelTest is not null)
            evidence.Add(new EvidenceReference(EvidenceKind.Communication, "operational-channel-test", "Operational channel test confirmed."));
        return evidence;
    }

    private static ContractReference? ToContractReference(ContractReferenceInput? input) => input is null ? null : new ContractReference(input.RepositoryReference, input.ContractNumber, input.SignedAt, input.ResponsibleParties);
}

internal sealed class CreatePendingIssueCommandHandler(InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor, IValidator<CreatePendingIssueCommand> validator)
    : ReadinessCommandHandlerBase(dbContext, auditWriter, clock, correlationIdAccessor), ICommandHandler<CreatePendingIssueCommand, PendingIssueResponse>
{
    public async Task<PendingIssueResponse> HandleAsync(CreatePendingIssueCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var onboarding = await LoadAsync(command.OnboardingId, cancellationToken);
        var now = Now;
        var issue = onboarding.AddPendingIssue(Guid.NewGuid(), command.Description, Enum.Parse<PendingOwnerType>(command.OwnerType, true), command.AssigneeId, command.RelatedGateType is null ? null : Enum.Parse<ReadinessGateType>(command.RelatedGateType, true), command.TargetAt, now, command.Actor);
        Audit(onboarding, command.Actor, "IssueOpened", "Pending issue opened.", now, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString() });
        await SaveAsync(cancellationToken);
        return PropertyOnboardingMapper.ToPendingIssueResponse(issue);
    }
}

internal sealed class UpdatePendingIssueCommandHandler(InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor, IValidator<UpdatePendingIssueCommand> validator)
    : ReadinessCommandHandlerBase(dbContext, auditWriter, clock, correlationIdAccessor), ICommandHandler<UpdatePendingIssueCommand, PendingIssueResponse>
{
    public async Task<PendingIssueResponse> HandleAsync(UpdatePendingIssueCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var onboarding = await LoadAsync(command.OnboardingId, cancellationToken);
        var issue = onboarding.PendingIssues.FindPendingIssue(command.PendingIssueId);
        var now = Now;
        if (command.Status is not null && !string.Equals(command.Status, "open", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(command.Status, "resolved", StringComparison.OrdinalIgnoreCase)) onboarding.ResolvePendingIssue(issue.Id, command.ResolutionNote!, now);
            else onboarding.CancelPendingIssue(issue.Id, command.ResolutionNote!, now);
        }
        else if (command.Description is not null || command.OwnerType is not null || command.HasAssigneeId || command.HasTargetAt)
            onboarding.UpdatePendingIssue(issue.Id, command.Description ?? issue.Description, command.OwnerType is null ? issue.OwnerType : Enum.Parse<PendingOwnerType>(command.OwnerType, true), command.HasAssigneeId ? command.AssigneeId : issue.AssigneeId, command.HasTargetAt ? command.TargetAt : issue.TargetAt, now);
        Audit(onboarding, command.Actor, "IssueUpdated", "Pending issue updated.", now, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString() });
        await SaveAsync(cancellationToken);
        return PropertyOnboardingMapper.ToPendingIssueResponse(issue);
    }
}

internal sealed class CreateCommunicationRecordCommandHandler(InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor, IBusinessCalendar businessCalendar, IValidator<CreateCommunicationRecordCommand> validator)
    : ReadinessCommandHandlerBase(dbContext, auditWriter, clock, correlationIdAccessor), ICommandHandler<CreateCommunicationRecordCommand, CommunicationRecordResponse>
{
    public async Task<CommunicationRecordResponse> HandleAsync(CreateCommunicationRecordCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var onboarding = await LoadAsync(command.OnboardingId, cancellationToken);
        var now = Now;
        var record = onboarding.RecordCommunication(Guid.NewGuid(), Enum.Parse<CommunicationChannel>(command.Channel, true), command.ReceivedAt, command.ProcessedAt, command.ResultSummary, businessCalendar.IsWithinBusinessHoursSla(command.ReceivedAt, command.ProcessedAt), command.Actor, now);
        Audit(onboarding, command.Actor, "CommunicationRecorded", "Communication result recorded.", now, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["communicationChannel"] = command.Channel });
        await SaveAsync(cancellationToken);
        InventoryTelemetry.CommunicationSla.Add(1, new KeyValuePair<string, object?>("result", record.ProcessedWithinSla ? "within_sla" : "outside_sla"));
        return PropertyOnboardingMapper.ToCommunicationRecordResponse(record);
    }
}
