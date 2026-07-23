using LocalizeStay.SharedKernel.Cqrs;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed record CreateCommercialOfferDraftCommand(Guid PropertyId, string Actor) : ICommand<CommercialOfferResponse>;

internal sealed record ValidateCommercialOfferCommand(Guid PropertyId, Guid ValidationId, string ValidatedBy, int ExpectedRevision) : ICommand<CommercialOfferResponse>;

internal sealed record SubmitCommercialOfferCommand(Guid PropertyId, Guid SubmissionId, string SnapshotJson, string SubmittedBy, int ExpectedRevision) : ICommand<CommercialOfferResponse>;

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
