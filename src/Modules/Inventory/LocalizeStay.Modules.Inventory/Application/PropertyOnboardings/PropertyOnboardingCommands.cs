using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using LocalizeStay.Contracts.Inventory;
using LocalizeStay.Modules.Inventory.Application.Observability;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Outbox;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;

// Compatibility instruments retained while dashboards migrate to InventoryTelemetry's richer names.
internal static class InventoryLifecycleTelemetry
{
    internal static readonly ActivitySource ActivitySource = InventoryTelemetry.ActivitySource;
    private static readonly Meter _meter = new(InventoryTelemetry.SourceName);
    internal static readonly Counter<long> SubmitSuccess = _meter.CreateCounter<long>("inventory.onboarding.submit.success");
    internal static readonly Counter<long> SubmitBlocked = _meter.CreateCounter<long>("inventory.onboarding.submit.blocked");
    internal static readonly Counter<long> OutboxFailure = _meter.CreateCounter<long>("inventory.onboarding.outbox.failure");
}

internal sealed record AddressInput(string Street, string Number, string? Complement, string District, string City, string State, string PostalCode, string CountryCode);
internal sealed record PropertyInput(string Name, string DestinationId, AddressInput Address);
internal sealed record CreatePropertyOnboardingCommand(Guid PartnerId, string PreselectionId, PropertyInput Property, string Actor) : ICommand<PropertyOnboardingResponse>;
internal sealed record UpdatePropertyOnboardingCommand(Guid OnboardingId, string? Name, AddressInput? Address, string Actor) : ICommand<PropertyOnboardingResponse>;
internal sealed record SubmitToCurationCommand(Guid OnboardingId, Guid IdempotencyKey, string DecisionNote, string Actor) : ICommand<SubmissionResultResponse>;
internal sealed record CreateCurationReturnCommand(Guid OnboardingId, Guid IdempotencyKey, string? CurationReference, string ReasonCode, string Reason, IReadOnlyList<CurationReturnIssueInput> Issues, string Actor) : ICommand<CurationReturnResultResponse>;
internal sealed record ClosePropertyOnboardingCommand(Guid OnboardingId, string ReasonCode, string Reason, string Actor) : ICommand<PropertyOnboardingResponse>;
internal sealed record CurationReturnIssueInput(string Description, string OwnerType, string? RelatedGateType);
internal sealed record IntegrationEventReferenceResponse(Guid Id, string Type, int Version, DateTimeOffset OccurredAt);
internal sealed record SubmissionResultResponse(PropertyOnboardingResponse Onboarding, IntegrationEventReferenceResponse IntegrationEvent);
internal sealed record CurationReturnResponse(Guid Id, string? CurationReference, string ReasonCode, string Reason, IReadOnlyList<CurationReturnIssueInput> Issues, DateTimeOffset ReturnedAt, string ReturnedBy);
internal sealed record CurationReturnResultResponse(CurationReturnResponse CurationReturn, PropertyOnboardingResponse Onboarding);

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
        InventoryTelemetry.OnboardingsOpened.Add(1, new KeyValuePair<string, object?>("destinationId", property.DestinationId));
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

internal sealed class SubmitToCurationCommandHandler(
    InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor,
    IValidator<SubmitToCurationCommand> validator) : ICommandHandler<SubmitToCurationCommand, SubmissionResultResponse>
{
    public async Task<SubmissionResultResponse> HandleAsync(SubmitToCurationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        using var activity = InventoryTelemetry.ActivitySource.StartActivity("inventory.onboarding.submit");
        activity?.SetTag("inventory.onboarding.id", command.OnboardingId);
        var existing = await dbContext.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(key => key.Key == command.IdempotencyKey, cancellationToken);
        var fingerprint = Fingerprint(command.DecisionNote);
        if (existing is not null)
        {
            return await ReplayAsync(existing, command, fingerprint, cancellationToken);
        }
        var onboarding = await LoadAsync(command.OnboardingId, cancellationToken);
        var now = clock.UtcNow;
        try { onboarding.SubmitToCuration(command.IdempotencyKey, command.DecisionNote, now, command.Actor); }
        catch (BusinessRuleViolationException) { InventoryTelemetry.Submitted.Add(1, new KeyValuePair<string, object?>("result", "blocked")); throw; }
        var contract = onboarding.ReadinessGates.Single(gate => gate.Type == ReadinessGateType.SignedContract).ContractReference!;
        var integrationEvent = new InventoryPropertyOnboardedV1 { OnboardingId = onboarding.Id, PartnerId = onboarding.PartnerId, DestinationId = onboarding.Property.DestinationId, ContractRepositoryReference = contract.RepositoryReference, SubmittedAt = now, OccurredOnUtc = now, CorrelationId = command.IdempotencyKey.ToString(), CausationId = command.IdempotencyKey.ToString() };
        await dbContext.IdempotencyKeys.AddAsync(IdempotencyKey.Create(onboarding.Id, command.IdempotencyKey, IdempotencyScope.SubmitToCuration, now, fingerprint), cancellationToken);
        dbContext.OutboxMessages.Add(OutboxMessageFactory.FromIntegrationEvent(integrationEvent));
        activity?.SetTag("inventory.event.id", integrationEvent.EventId);
        auditWriter.Record(BusinessAuditEntry.Create("PropertyOnboarding", onboarding.Id.ToString(), command.Actor, "SubmittedToCuration", "Property onboarding submitted to curation.", now, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["idempotencyKey"] = command.IdempotencyKey.ToString() }));
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentKey = await dbContext.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(key => key.Key == command.IdempotencyKey, cancellationToken);
            if (concurrentKey is not null) return await ReplayAsync(concurrentKey, command, fingerprint, cancellationToken);
            InventoryTelemetry.OutboxFailures.Add(1, new KeyValuePair<string, object?>("result", "persistence_failure"));
            throw;
        }
        catch { InventoryTelemetry.OutboxFailures.Add(1, new KeyValuePair<string, object?>("result", "persistence_failure")); throw; }
        InventoryTelemetry.Submitted.Add(1, new KeyValuePair<string, object?>("result", "success"));
        InventoryTelemetry.SubmissionDuration.Record((now - onboarding.OpenedAt).TotalSeconds);
        return new SubmissionResultResponse(PropertyOnboardingMapper.ToResponse(onboarding), new IntegrationEventReferenceResponse(integrationEvent.EventId, InventoryPropertyOnboardedV1.EventType, integrationEvent.Version, integrationEvent.OccurredOnUtc));
    }

    private async Task<PropertyOnboarding> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PropertyOnboardings.Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new NotFoundException("Property onboarding was not found.", "PROPERTY_ONBOARDING_NOT_FOUND");

    private async Task<SubmissionResultResponse> ReplayAsync(IdempotencyKey existing, SubmitToCurationCommand command, string fingerprint, CancellationToken cancellationToken)
    {
        if (existing.PropertyOnboardingId != command.OnboardingId || existing.Scope != IdempotencyScope.SubmitToCuration || existing.PayloadFingerprint != fingerprint)
            throw new ConflictException("Idempotency key was already used for a different operation.", "STATE_CONFLICT");
        var replay = await LoadAsync(command.OnboardingId, cancellationToken);
        var message = await dbContext.OutboxMessages.AsNoTracking().OrderByDescending(item => item.OccurredOnUtc).FirstAsync(item => item.CorrelationId == command.IdempotencyKey.ToString(), cancellationToken);
        var replayEvent = JsonSerializer.Deserialize<InventoryPropertyOnboardedV1>(message.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SubmissionResultResponse(PropertyOnboardingMapper.ToResponse(replay), new IntegrationEventReferenceResponse(replayEvent.EventId, InventoryPropertyOnboardedV1.EventType, replayEvent.Version, replayEvent.OccurredOnUtc));
    }

    private static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim())));
}

internal sealed class CreateCurationReturnCommandHandler(InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor, IValidator<CreateCurationReturnCommand> validator) : ICommandHandler<CreateCurationReturnCommand, CurationReturnResultResponse>
{
    public async Task<CurationReturnResultResponse> HandleAsync(CreateCurationReturnCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var existing = await dbContext.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(key => key.Key == command.IdempotencyKey, cancellationToken);
        var fingerprintPayload = JsonSerializer.Serialize(
            new { command.CurationReference, command.ReasonCode, command.Reason, command.Issues },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload)));
        if (existing is not null)
        {
            return await ReplayAsync(existing, command, fingerprint, cancellationToken);
        }
        var onboarding = await dbContext.PropertyOnboardings.Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).Include(item => item.CurationReturns).SingleOrDefaultAsync(item => item.Id == command.OnboardingId, cancellationToken) ?? throw new NotFoundException("Property onboarding was not found.", "PROPERTY_ONBOARDING_NOT_FOUND");
        var now = clock.UtcNow;
        var issues = command.Issues.Select(issue => new CurationReturnIssue(issue.Description, Enum.Parse<PendingOwnerType>(issue.OwnerType, true), issue.RelatedGateType is null ? null : Enum.Parse<ReadinessGateType>(issue.RelatedGateType, true))).ToList();
        var returned = onboarding.RecordCurationReturn(Guid.NewGuid(), command.CurationReference, Enum.Parse<CurationReturnReasonCode>(command.ReasonCode, true), command.Reason, issues, now, command.Actor, command.IdempotencyKey);
        await dbContext.IdempotencyKeys.AddAsync(IdempotencyKey.Create(onboarding.Id, command.IdempotencyKey, IdempotencyScope.CurationReturn, now, fingerprint), cancellationToken);
        auditWriter.Record(BusinessAuditEntry.Create("PropertyOnboarding", onboarding.Id.ToString(), command.Actor, "ReturnedByCuration", "Property onboarding returned by curation.", now, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["idempotencyKey"] = command.IdempotencyKey.ToString(), ["curationReturnId"] = returned.Id.ToString() }));
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentKey = await dbContext.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(key => key.Key == command.IdempotencyKey, cancellationToken);
            if (concurrentKey is not null) return await ReplayAsync(concurrentKey, command, fingerprint, cancellationToken);
            throw;
        }
        InventoryTelemetry.Returns.Add(1, new KeyValuePair<string, object?>("result", "recorded"));
        return new CurationReturnResultResponse(ToResponse(returned), PropertyOnboardingMapper.ToResponse(onboarding));
    }

    private async Task<CurationReturnResultResponse> ReplayAsync(IdempotencyKey existing, CreateCurationReturnCommand command, string fingerprint, CancellationToken cancellationToken)
    {
        if (existing.PropertyOnboardingId != command.OnboardingId || existing.Scope != IdempotencyScope.CurationReturn || existing.PayloadFingerprint != fingerprint) throw new ConflictException("Idempotency key was already used with incompatible state.", "STATE_CONFLICT");
        var replay = await dbContext.PropertyOnboardings.AsNoTracking().Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).Include(item => item.CurationReturns).SingleAsync(item => item.Id == command.OnboardingId, cancellationToken);
        return new CurationReturnResultResponse(ToResponse(replay.CurationReturns.OrderByDescending(item => item.ReturnedAt).First()), PropertyOnboardingMapper.ToResponse(replay));
    }
    private static CurationReturnResponse ToResponse(CurationReturn item) => new(item.Id, item.CurationReference, char.ToLowerInvariant(item.ReasonCode.ToString()[0]) + item.ReasonCode.ToString()[1..], item.Reason, item.Issues.Select(issue => new CurationReturnIssueInput(issue.Description, char.ToLowerInvariant(issue.OwnerType.ToString()[0]) + issue.OwnerType.ToString()[1..], issue.RelatedGateType is null ? null : char.ToLowerInvariant(issue.RelatedGateType.Value.ToString()[0]) + issue.RelatedGateType.Value.ToString()[1..])).ToList(), item.ReturnedAt, item.ReturnedBy);
}

internal sealed class ClosePropertyOnboardingCommandHandler(InventoryDbContext dbContext, IBusinessAuditWriter auditWriter, IClock clock, ICorrelationIdAccessor correlationIdAccessor, IValidator<ClosePropertyOnboardingCommand> validator) : ICommandHandler<ClosePropertyOnboardingCommand, PropertyOnboardingResponse>
{
    public async Task<PropertyOnboardingResponse> HandleAsync(ClosePropertyOnboardingCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var onboarding = await dbContext.PropertyOnboardings.Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).SingleOrDefaultAsync(item => item.Id == command.OnboardingId, cancellationToken) ?? throw new NotFoundException("Property onboarding was not found.", "PROPERTY_ONBOARDING_NOT_FOUND");
        var now = clock.UtcNow;
        onboarding.Close(Enum.Parse<CloseReasonCode>(command.ReasonCode, true), command.Reason, now, command.Actor);
        auditWriter.Record(BusinessAuditEntry.Create("PropertyOnboarding", onboarding.Id.ToString(), command.Actor, "OnboardingClosed", "Property onboarding closed.", now, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["reasonCode"] = command.ReasonCode }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return PropertyOnboardingMapper.ToResponse(onboarding);
    }
}
