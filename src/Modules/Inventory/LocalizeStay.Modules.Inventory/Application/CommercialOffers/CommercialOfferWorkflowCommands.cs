using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentValidation;
using LocalizeStay.Contracts.Inventory;
using LocalizeStay.Modules.Inventory.Application.Observability;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Outbox;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed class CreateOfferValidationCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<ValidateCommercialOfferCommand> validator) : ICommandHandler<ValidateCommercialOfferCommand, OfferValidationResponse>
{
    public async Task<OfferValidationResponse> HandleAsync(
        ValidateCommercialOfferCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        using var activity = InventoryTelemetry.ActivitySource.StartActivity(InventoryTelemetry.Spans.Validate);
        activity?.SetTag(InventoryTelemetry.Tags.PropertyId, command.PropertyId.ToString());

        var offer = await dbContext.CommercialOffers
            .SingleOrDefaultAsync(o => o.PropertyId == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var now = clock.UtcNow;
        var previousValidationId = offer.CurrentValidation?.Id;
        offer.Validate(
            command.ValidationId,
            command.ValidatedBy,
            command.ExpectedRevision,
            now,
            command.Comment);

        activity?.SetTag(InventoryTelemetry.Tags.ValidationId, command.ValidationId.ToString());
        activity?.SetTag(InventoryTelemetry.Tags.OfferRevision, offer.Revision);

        if (previousValidationId is not null && previousValidationId != command.ValidationId)
        {
            InventoryTelemetry.OfferValidationInvalidated.Add(1);
        }

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            offer.Id.ToString(),
            command.ValidatedBy,
            "OfferValidated",
            "Commercial offer validated by second operator.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = offer.Id.ToString(),
                ["validationId"] = command.ValidationId.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        InventoryTelemetry.OfferValidation.Add(1, new KeyValuePair<string, object?>("result", "success"));
        return CommercialOfferMapper.ToResponse(offer.CurrentValidation!);
    }
}

internal sealed class SubmitCommercialOfferCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<SubmitCommercialOfferCommand> validator) : ICommandHandler<SubmitCommercialOfferCommand, OfferSubmissionResponse>
{
    public async Task<OfferSubmissionResponse> HandleAsync(
        SubmitCommercialOfferCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        using var activity = InventoryTelemetry.ActivitySource.StartActivity(InventoryTelemetry.Spans.Submit);
        activity?.SetTag(InventoryTelemetry.Tags.PropertyId, command.PropertyId.ToString());

        var idempotencyKey = command.SubmissionId;
        var existing = await dbContext.CommercialOfferIdempotencyKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(k => k.Key == idempotencyKey && k.Scope == "submission", cancellationToken);

        var fingerprint = ComputeFingerprint(command);
        if (existing is not null)
        {
            return await ReplayAsync(existing, command, fingerprint, cancellationToken);
        }

        var offer = await dbContext.CommercialOffers
            .Include(o => o.CurrentValidation)
            .Include(o => o.Accommodations)
            .Include(o => o.Rates)
            .Include(o => o.Policies)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.PropertyId == command.PropertyId, cancellationToken)
            ?? throw new NotFoundException("Commercial offer was not found.", "PROPERTY_NOT_FOUND");

        var now = clock.UtcNow;
        var snapshotJson = CommercialOfferSnapshotSerializer.Serialize(offer, now, command.SubmittedBy);

        var submission = offer.Submit(
            command.SubmissionId,
            command.ValidationId,
            snapshotJson,
            command.SubmittedBy,
            command.ExpectedRevision,
            now);

        var integrationEvent = new InventoryCommercialOfferStructuredV1
        {
            PropertyId = offer.PropertyId,
            SubmissionId = submission.Id,
            RevisionAtSubmission = submission.Revision,
            SnapshotJson = snapshotJson,
            SubmittedBy = command.SubmittedBy,
            SubmittedAt = now,
            OccurredOnUtc = now,
            CorrelationId = command.SubmissionId.ToString(),
            CausationId = command.SubmissionId.ToString(),
        };

        var key = CommercialOfferIdempotencyKey.Create(
            offer.PropertyId,
            idempotencyKey,
            "submission",
            now,
            fingerprint,
            submission.Id);

        dbContext.CommercialOfferIdempotencyKeys.Add(key);
        dbContext.OutboxMessages.Add(OutboxMessageFactory.FromIntegrationEvent(integrationEvent));

        activity?.SetTag(InventoryTelemetry.Tags.EventId, integrationEvent.EventId.ToString());
        activity?.SetTag(InventoryTelemetry.Tags.SubmissionId, submission.Id.ToString());
        activity?.SetTag(InventoryTelemetry.Tags.OfferRevision, offer.Revision);

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            offer.Id.ToString(),
            command.SubmittedBy,
            "OfferSubmitted",
            "Commercial offer submitted to curation.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = offer.Id.ToString(),
                ["submissionId"] = submission.Id.ToString(),
            }));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentKey = await dbContext.CommercialOfferIdempotencyKeys
                .AsNoTracking()
                .SingleOrDefaultAsync(k => k.Key == idempotencyKey && k.Scope == "submission", cancellationToken);

            if (concurrentKey is not null)
                return await ReplayAsync(concurrentKey, command, fingerprint, cancellationToken);

            InventoryTelemetry.OfferOutboxFailure.Add(1);
            throw;
        }
        catch
        {
            InventoryTelemetry.OfferOutboxFailure.Add(1);
            throw;
        }

        InventoryTelemetry.OfferSubmission.Add(1, new KeyValuePair<string, object?>("result", "success"));
        InventoryTelemetry.OfferSubmissionDuration.Record((now - offer.CreatedAt).TotalSeconds);
        return CommercialOfferMapper.ToResponse(submission);
    }

    private async Task<OfferSubmissionResponse> ReplayAsync(
        CommercialOfferIdempotencyKey existing,
        SubmitCommercialOfferCommand command,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        if (existing.Key != command.SubmissionId || existing.Scope != "submission")
            throw new ConflictException("Idempotency key was already used for a different operation.", "STATE_CONFLICT");

        if (existing.PayloadFingerprint != fingerprint)
            throw new ConflictException("Idempotency key was already used with a different payload.", "IDEMPOTENCY_KEY_REUSED");

        var submission = await dbContext.OfferSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                s => s.Id == existing.ResultReferenceId && s.PropertyId == command.PropertyId,
                cancellationToken)
            ?? throw new NotFoundException("Commercial offer submission was not found.", "SUBMISSION_NOT_FOUND");

        return CommercialOfferMapper.ToResponse(submission);
    }

    private static string ComputeFingerprint(SubmitCommercialOfferCommand command)
    {
        var canonicalPayload = new
        {
            command.PropertyId,
            command.SubmissionId,
            command.ValidationId,
            command.ExpectedRevision,
        };

        var json = JsonSerializer.Serialize(canonicalPayload, CanonicalJsonOptions.Options);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

internal static class CanonicalJsonOptions
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

internal static class CommercialOfferSnapshotSerializer
{
    internal static string Serialize(CommercialOffer offer, DateTimeOffset now, string submittedBy)
    {
        var accommodations = offer.Accommodations
            .Select(a => new
            {
                a.Id,
                a.CommercialName,
                a.TotalCapacity,
                a.MaxAdults,
                a.MaxChildren,
                a.Status,
                Rates = offer.Rates
                    .Where(r => r.AccommodationId == a.Id)
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.ConditionCode,
                        r.BasePriceCents,
                        r.IncludedGuests,
                        r.AdditionalAdultPriceCents,
                        r.AdditionalChildPriceCents,
                        r.ValidFrom,
                        r.ValidTo,
                        r.MinimumNights,
                        r.PolicyId,
                        r.MealPlan,
                        r.Status,
                    })
                    .ToList(),
            })
            .ToList();

        var policies = offer.Policies
            .Select(p => new
            {
                p.Id,
                p.Type,
                p.Title,
                p.RulesSummary,
                p.RuleSetVersion,
                p.IsDefault,
                p.Status,
            })
            .ToList();

        var snapshot = new
        {
            snapshotVersion = 1,
            offer.Id,
            offer.PropertyId,
            revision = offer.Revision,
            revisionAuthor = offer.RevisionAuthor,
            state = offer.State.ToString(),
            validationId = offer.CurrentValidation?.Id,
            submittedBy,
            submittedAt = now,
            accommodations,
            policies,
        };

        return JsonSerializer.Serialize(snapshot, CanonicalJsonOptions.Options);
    }
}

internal sealed class ValidateCommercialOfferCommandValidator : AbstractValidator<ValidateCommercialOfferCommand>
{
    public ValidateCommercialOfferCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.ValidationId).NotEmpty();
        RuleFor(c => c.ValidatedBy).NotEmpty().MaximumLength(200);
        RuleFor(c => c.ExpectedRevision).GreaterThan(0);
        RuleFor(c => c.Comment).MaximumLength(1_000);
    }
}

internal sealed class SubmitCommercialOfferCommandValidator : AbstractValidator<SubmitCommercialOfferCommand>
{
    public SubmitCommercialOfferCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.SubmissionId).NotEmpty();
        RuleFor(c => c.ValidationId).NotEmpty();
        RuleFor(c => c.SubmittedBy).NotEmpty().MaximumLength(200);
        RuleFor(c => c.ExpectedRevision).GreaterThan(0);
    }
}
