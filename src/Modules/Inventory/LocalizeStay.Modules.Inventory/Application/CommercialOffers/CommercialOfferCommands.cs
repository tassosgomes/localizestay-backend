using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed record CreateCommercialOfferDraftCommand(Guid PropertyId, string Actor) : ICommand<CommercialOfferResponse>;

internal sealed record ValidateCommercialOfferCommand(
    Guid PropertyId,
    Guid ValidationId,
    string ValidatedBy,
    int ExpectedRevision,
    string? Comment) : ICommand<OfferValidationResponse>;

internal sealed record SubmitCommercialOfferCommand(
    Guid PropertyId,
    Guid SubmissionId,
    Guid ValidationId,
    string SubmittedBy,
    int ExpectedRevision) : ICommand<OfferSubmissionResponse>;

internal sealed record RecordCommercialOfferReturnCommand(Guid PropertyId, Guid ReturnId, Guid SubmissionId, string ReasonCode, string Reason, string ReturnedBy) : ICommand<CommercialOfferResponse>;

internal sealed record CommercialOfferResponse(
    Guid PropertyId,
    int Revision,
    string RevisionAuthor,
    string State,
    int AccommodationCount,
    int BlockingIssueCount,
    bool EverSubmitted,
    DateTimeOffset? CompleteInformationReceivedAt,
    DateTimeOffset? TargetSubmissionAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed class CreateCommercialOfferDraftCommandHandler(
    InventoryDbContext dbContext,
    IClock clock,
    IBusinessCalendar businessCalendar) : ICommandHandler<CreateCommercialOfferDraftCommand, CommercialOfferResponse>
{
    public async Task<CommercialOfferResponse> HandleAsync(
        CreateCommercialOfferDraftCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.CommercialOffers.AsNoTracking()
            .SingleOrDefaultAsync(o => o.PropertyId == command.PropertyId, cancellationToken);

        if (existing is not null)
            return CommercialOfferMapper.ToResponse(existing);

        var utcNow = clock.UtcNow;
        var created = CommercialOffer.Create(command.PropertyId, command.Actor, utcNow);
        created.SetTargetSubmissionAt(businessCalendar.AddBusinessDays(utcNow, 10));
        dbContext.CommercialOffers.Add(created);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!cancellationToken.IsCancellationRequested)
        {
            dbContext.ChangeTracker.Clear();
            existing = await dbContext.CommercialOffers.AsNoTracking()
                .SingleOrDefaultAsync(o => o.PropertyId == command.PropertyId, cancellationToken);
            if (existing is not null)
                return CommercialOfferMapper.ToResponse(existing);
            throw;
        }

        return CommercialOfferMapper.ToResponse(created);
    }
}
