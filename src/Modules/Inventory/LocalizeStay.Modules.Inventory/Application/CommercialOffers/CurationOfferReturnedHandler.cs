using System.Diagnostics;
using LocalizeStay.Contracts.Curation;
using LocalizeStay.Modules.Inventory.Application.Observability;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Events;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed class CurationOfferReturnedHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<CurationOfferReturnedHandler> logger)
    : IIntegrationEventHandler<CurationOfferReturnedV1>
{
    public async Task HandleAsync(
        CurationOfferReturnedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        using var activity = InventoryTelemetry.ActivitySource.StartActivity(InventoryTelemetry.Spans.Return);
        activity?.SetTag(InventoryTelemetry.Tags.EventId, integrationEvent.EventId.ToString());
        activity?.SetTag(InventoryTelemetry.Tags.PropertyId, integrationEvent.PropertyId.ToString());
        activity?.SetTag(InventoryTelemetry.Tags.SubmissionId, integrationEvent.SubmissionId.ToString());

        logger.LogInformation(
            "CurationOfferReturnedHandler.HandleAsync called for EventId={EventId}, PropertyId={PropertyId}, SubmissionId={SubmissionId}",
            integrationEvent.EventId, integrationEvent.PropertyId, integrationEvent.SubmissionId);

        var alreadyProcessed = await dbContext.OfferReturns
            .AsNoTracking()
            .AnyAsync(r => r.EventId == integrationEvent.EventId, cancellationToken);

        if (alreadyProcessed)
        {
            logger.LogWarning(
                "Duplicate curation return event {EventId} for property {PropertyId} ignored.",
                integrationEvent.EventId,
                integrationEvent.PropertyId);

            return;
        }

        var offer = await dbContext.CommercialOffers
            .Include(o => o.Submissions)
            .SingleOrDefaultAsync(o => o.PropertyId == integrationEvent.PropertyId, cancellationToken);

        if (offer is null)
        {
            logger.LogWarning(
                "Curation return event {EventId} ignored: property {PropertyId} not found.",
                integrationEvent.EventId,
                integrationEvent.PropertyId);

            return;
        }

        logger.LogInformation(
            "Offer found: PropertyId={PropertyId}, State={State}, Expected Submitted",
            integrationEvent.PropertyId, offer.State);

        if (offer.State == OfferState.Returned)
        {
            logger.LogWarning(
                "Curation return event {EventId} ignored: offer for property {PropertyId} is already returned.",
                integrationEvent.EventId,
                integrationEvent.PropertyId);

            return;
        }

        var now = clock.UtcNow;
        var returnId = Guid.NewGuid();

        try
        {
            offer.RecordReturn(
                returnId,
                integrationEvent.SubmissionId,
                integrationEvent.EventId,
                integrationEvent.ReasonCode,
                integrationEvent.Reason,
                integrationEvent.ReturnedBy,
                now);

            logger.LogInformation("RecordReturn succeeded, offer state now: {State}", offer.State);
        }
        catch (BusinessRuleViolationException ex) when (ex.ErrorCode is "PUBLISHED_OFFER_CHANGE_REQUIRES_F04" or "STALE_SUBMISSION_RETURN")
        {
            if (ex.ErrorCode == "STALE_SUBMISSION_RETURN")
            {
                logger.LogWarning(
                    "Curation return event {EventId} ignored: submission {SubmissionId} is stale for property {PropertyId}.",
                    integrationEvent.EventId,
                    integrationEvent.SubmissionId,
                    integrationEvent.PropertyId);
                return;
            }

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        auditWriter.Record(BusinessAuditEntry.Create(
            "CommercialOffer",
            offer.Id.ToString(),
            integrationEvent.ReturnedBy,
            "OfferReturned",
            "Commercial offer returned by curation.",
            now,
            correlationIdAccessor.CorrelationId,
            new Dictionary<string, string>
            {
                ["propertyId"] = offer.Id.ToString(),
                ["submissionId"] = integrationEvent.SubmissionId.ToString(),
                ["eventId"] = integrationEvent.EventId.ToString(),
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        InventoryTelemetry.OfferReturned.Add(1, new KeyValuePair<string, object?>("result", "success"));

        logger.LogInformation(
            "Curation return processed: property {PropertyId}, submission {SubmissionId}, event {EventId}.",
            integrationEvent.PropertyId,
            integrationEvent.SubmissionId,
            integrationEvent.EventId);
    }
}
