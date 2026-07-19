using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Partners;
using LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;

namespace LocalizeStay.Modules.Inventory.Application.Validation;

internal sealed class CreatePartnerCommandValidator : AbstractValidator<CreatePartnerCommand>
{
    public CreatePartnerCommandValidator()
    {
        RuleFor(command => command.PreselectionId).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LegalName).NotEmpty().Length(2, 180);
        RuleFor(command => command.TradeName).MaximumLength(180).When(command => command.TradeName is not null);
        RuleFor(command => command.LegalIdentifier).NotNull().SetValidator(new LegalIdentifierInputValidator());
        RuleFor(command => command.PrimaryContact).NotNull().SetValidator(new ContactInputValidator());
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdatePartnerCommandValidator : AbstractValidator<UpdatePartnerCommand>
{
    public UpdatePartnerCommandValidator()
    {
        RuleFor(command => command.PartnerId).NotEmpty();
        RuleFor(command => command).Must(command => command.LegalName is not null || command.HasTradeName || command.LegalIdentifier is not null || command.PrimaryContact is not null).WithMessage("At least one field must be supplied.");
        RuleFor(command => command.LegalName!).Length(2, 180).When(command => command.LegalName is not null);
        RuleFor(command => command.TradeName).MaximumLength(180).When(command => command.HasTradeName && command.TradeName is not null);
        RuleFor(command => command.LegalIdentifier!).SetValidator(new LegalIdentifierInputValidator()).When(command => command.LegalIdentifier is not null);
        RuleFor(command => command.PrimaryContact!).SetValidator(new ContactInputValidator()).When(command => command.PrimaryContact is not null);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class LegalIdentifierInputValidator : AbstractValidator<LegalIdentifierInput>
{
    public LegalIdentifierInputValidator()
    {
        RuleFor(input => input.Type).Must(type => type is not null && new[] { "cnpj", "cpf", "other" }.Contains(type, StringComparer.OrdinalIgnoreCase)).WithErrorCode("INVALID_LEGAL_IDENTIFIER");
        RuleFor(input => input.CountryCode).Matches("^[A-Z]{2}$").WithErrorCode("INVALID_LEGAL_IDENTIFIER");
        RuleFor(input => input.Value).NotEmpty().Length(5, 40).WithErrorCode("INVALID_LEGAL_IDENTIFIER");
    }
}

internal sealed class ContactInputValidator : AbstractValidator<ContactInput>
{
    public ContactInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().Length(2, 120);
        RuleFor(input => input.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(input => input.Phone).NotEmpty().Length(8, 30);
    }
}

internal sealed class ListPartnersQueryValidator : AbstractValidator<ListPartnersQuery>
{
    public ListPartnersQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Size).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).Length(2, 120).When(query => query.Search is not null);
        RuleFor(query => query.LegalIdentifierType).Must(type => type is null || new[] { "cnpj", "cpf", "other" }.Contains(type, StringComparer.OrdinalIgnoreCase));
    }
}

internal sealed class CreatePropertyOnboardingCommandValidator : AbstractValidator<CreatePropertyOnboardingCommand>
{
    public CreatePropertyOnboardingCommandValidator()
    {
        RuleFor(command => command.PartnerId).NotEmpty();
        RuleFor(command => command.PreselectionId).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Property).NotNull().SetValidator(new PropertyInputValidator());
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdatePropertyOnboardingCommandValidator : AbstractValidator<UpdatePropertyOnboardingCommand>
{
    public UpdatePropertyOnboardingCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty();
        RuleFor(command => command).Must(command => command.Name is not null || command.Address is not null).WithMessage("At least one field must be supplied.");
        RuleFor(command => command.Name!).Length(2, 180).When(command => command.Name is not null);
        RuleFor(command => command.Address!).SetValidator(new AddressInputValidator()).When(command => command.Address is not null);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class PropertyInputValidator : AbstractValidator<PropertyInput>
{
    public PropertyInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().Length(2, 180);
        RuleFor(input => input.DestinationId).NotEmpty().MaximumLength(120);
        RuleFor(input => input.Address).NotNull().SetValidator(new AddressInputValidator());
    }
}

internal sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(input => input.Street).NotEmpty().MaximumLength(180);
        RuleFor(input => input.Number).NotEmpty().MaximumLength(30);
        RuleFor(input => input.Complement).MaximumLength(120).When(input => input.Complement is not null);
        RuleFor(input => input.District).NotEmpty().MaximumLength(120);
        RuleFor(input => input.City).NotEmpty().MaximumLength(120);
        RuleFor(input => input.State).NotEmpty().Length(2, 80);
        RuleFor(input => input.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(input => input.CountryCode).Matches("^[A-Z]{2}$");
    }
}

internal sealed class ListPropertyOnboardingsQueryValidator : AbstractValidator<ListPropertyOnboardingsQuery>
{
    public ListPropertyOnboardingsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Size).InclusiveBetween(1, 100);
        RuleFor(query => query.DestinationId).MaximumLength(120).When(query => query.DestinationId is not null);
        RuleFor(query => query.LifecycleStatus).Must(value => value is null || Enum.TryParse<Domain.PropertyOnboardings.OnboardingLifecycleStatus>(value, true, out _));
        RuleFor(query => query.ReadinessStatus).Must(value => value is null || Enum.TryParse<Domain.PropertyOnboardings.ReadinessStatus>(value, true, out _));
        RuleFor(query => query.PendingOwnerType).Must(value => value is null || Enum.TryParse<Domain.PropertyOnboardings.PendingOwnerType>(value, true, out _));
        RuleFor(query => query.Sort).Must(value => value is null || new[] { "openedAt", "targetSubmissionAt", "updatedAt", "propertyName" }.Contains(value, StringComparer.Ordinal));
        RuleFor(query => query.Order).Must(value => value is null || new[] { "asc", "desc" }.Contains(value, StringComparer.OrdinalIgnoreCase));
    }
}

internal sealed class ListPropertyOnboardingHistoryQueryValidator : AbstractValidator<ListPropertyOnboardingHistoryQuery>
{
    public ListPropertyOnboardingHistoryQueryValidator()
    {
        RuleFor(query => query.OnboardingId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Size).InclusiveBetween(1, 100);
    }
}

internal sealed class GetPropertyOnboardingMetricsQueryValidator : AbstractValidator<GetPropertyOnboardingMetricsQuery>
{
    public GetPropertyOnboardingMetricsQueryValidator()
    {
        RuleFor(query => query.From).NotEmpty();
        RuleFor(query => query.To).GreaterThan(query => query.From);
        RuleFor(query => query.DestinationId).MaximumLength(120).When(query => query.DestinationId is not null);
    }
}

internal sealed class UpdateReadinessGateCommandValidator : AbstractValidator<UpdateReadinessGateCommand>
{
    private static readonly string[] _gateTypes = ["legalIdentification", "commercialTerms", "signedContract", "authorizedContact", "propertyBasics", "operationalChannel"];
    public UpdateReadinessGateCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty();
        RuleFor(command => command.GateType).Must(value => _gateTypes.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(command => command.Status).Must(value => new[] { "pending", "validated", "rejected" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(command => command.Notes).NotEmpty().MaximumLength(1000).When(command => string.Equals(command.Status, "rejected", StringComparison.OrdinalIgnoreCase));
        RuleFor(command => command.ContractReference!.RepositoryReference).NotEmpty().MaximumLength(500).When(command => command.ContractReference is not null);
        RuleFor(command => command.ContractReference!.ContractNumber).MaximumLength(80).When(command => command.ContractReference?.ContractNumber is not null);
        RuleFor(command => command.ContractReference!.SignedAt).NotEmpty().When(command => command.ContractReference is not null);
        RuleFor(command => command.ContractReference!.ResponsibleParties).NotEmpty().When(command => command.ContractReference is not null);
        RuleForEach(command => command.ContractReference!.ResponsibleParties).NotEmpty().MaximumLength(180).When(command => command.ContractReference is not null);
        RuleFor(command => command.AuthorizedContact!).SetValidator(new ContactInputValidator()).When(command => command.AuthorizedContact is not null);
        RuleFor(command => command.OperationalChannelTest!.Channel).Must(value => value is not null && new[] { "whatsapp", "email" }.Contains(value, StringComparer.OrdinalIgnoreCase)).When(command => command.OperationalChannelTest is not null);
        RuleFor(command => command.OperationalChannelTest!.Contact).NotEmpty().MaximumLength(180).When(command => command.OperationalChannelTest is not null);
        RuleFor(command => command.OperationalChannelTest!.TestedAt).NotEqual(default(DateTimeOffset)).When(command => command.OperationalChannelTest is not null);
        RuleFor(command => command.OperationalChannelTest!.ResultSummary).NotEmpty().MaximumLength(500).When(command => command.OperationalChannelTest is not null);
        RuleForEach(command => command.Evidence!).SetValidator(new EvidenceReferenceInputValidator()).When(command => command.Evidence is not null);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class EvidenceReferenceInputValidator : AbstractValidator<EvidenceReferenceInput>
{
    public EvidenceReferenceInputValidator()
    {
        RuleFor(input => input.Kind).Must(value => new[] { "officialDocument", "contract", "formalAuthorization", "communication", "other" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(input => input.Reference).NotEmpty().MaximumLength(500);
        RuleFor(input => input.Description).NotEmpty().MaximumLength(300);
    }
}

internal sealed class CreatePendingIssueCommandValidator : AbstractValidator<CreatePendingIssueCommand>
{
    public CreatePendingIssueCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty(); RuleFor(command => command.Description).NotEmpty().Length(3, 1000);
        RuleFor(command => command.OwnerType).Must(value => Enum.TryParse<Domain.PropertyOnboardings.PendingOwnerType>(value, true, out _));
        RuleFor(command => command.AssigneeId).MaximumLength(120).When(command => command.AssigneeId is not null);
        RuleFor(command => command.RelatedGateType).Must(value => value is null || Enum.TryParse<Domain.PropertyOnboardings.ReadinessGateType>(value, true, out _));
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdatePendingIssueCommandValidator : AbstractValidator<UpdatePendingIssueCommand>
{
    public UpdatePendingIssueCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty(); RuleFor(command => command.PendingIssueId).NotEmpty();
        RuleFor(command => command).Must(command => command.Description is not null || command.OwnerType is not null || command.HasAssigneeId || command.HasTargetAt || command.Status is not null).WithMessage("At least one field must be supplied.");
        RuleFor(command => command.Description).Length(3, 1000).When(command => command.Description is not null);
        RuleFor(command => command.OwnerType).Must(value => value is null || Enum.TryParse<Domain.PropertyOnboardings.PendingOwnerType>(value, true, out _));
        RuleFor(command => command.Status).Must(value => value is null || new[] { "open", "resolved", "cancelled" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(command => command.ResolutionNote).NotEmpty().MaximumLength(1000).When(command => string.Equals(command.Status, "resolved", StringComparison.OrdinalIgnoreCase) || string.Equals(command.Status, "cancelled", StringComparison.OrdinalIgnoreCase));
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateCommunicationRecordCommandValidator : AbstractValidator<CreateCommunicationRecordCommand>
{
    public CreateCommunicationRecordCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty(); RuleFor(command => command.Channel).Must(value => new[] { "whatsapp", "email" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(command => command.ReceivedAt).NotEqual(default(DateTimeOffset));
        RuleFor(command => command.ProcessedAt).NotEqual(default(DateTimeOffset));
        RuleFor(command => command.ProcessedAt).GreaterThanOrEqualTo(command => command.ReceivedAt);
        RuleFor(command => command.ResultSummary).NotEmpty().Length(3, 1000); RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateDuplicateReviewCommandValidator : AbstractValidator<CreateDuplicateReviewCommand>
{
    public CreateDuplicateReviewCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty(); RuleFor(command => command.IdempotencyKey).NotEmpty();
        RuleFor(command => command.Decision).Must(value => new[] { "notDuplicate", "duplicateOfExistingProperty" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(command => command.ExistingPropertyId).NotEmpty().When(command => string.Equals(command.Decision, "duplicateOfExistingProperty", StringComparison.OrdinalIgnoreCase));
        RuleFor(command => command.Justification).NotEmpty().Length(10, 1000); RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class SubmitToCurationCommandValidator : AbstractValidator<SubmitToCurationCommand>
{
    public SubmitToCurationCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty();
        RuleFor(command => command.DecisionNote).NotEmpty().Length(3, 1000);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateCurationReturnCommandValidator : AbstractValidator<CreateCurationReturnCommand>
{
    public CreateCurationReturnCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty(); RuleFor(command => command.IdempotencyKey).NotEmpty();
        RuleFor(command => command.CurationReference).MaximumLength(120).When(command => command.CurationReference is not null);
        RuleFor(command => command.ReasonCode).Must(value => new[] { "missingData", "inconsistentData" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(command => command.Reason).NotEmpty().Length(3, 1000);
        RuleFor(command => command.Issues).NotEmpty();
        RuleForEach(command => command.Issues).ChildRules(issue => { issue.RuleFor(item => item.Description).NotEmpty().Length(3, 1000); issue.RuleFor(item => item.OwnerType).Must(value => Enum.TryParse<Domain.PropertyOnboardings.PendingOwnerType>(value, true, out _)); issue.RuleFor(item => item.RelatedGateType).Must(value => value is null || Enum.TryParse<Domain.PropertyOnboardings.ReadinessGateType>(value, true, out _)); });
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class ClosePropertyOnboardingCommandValidator : AbstractValidator<ClosePropertyOnboardingCommand>
{
    public ClosePropertyOnboardingCommandValidator()
    {
        RuleFor(command => command.OnboardingId).NotEmpty();
        RuleFor(command => command.ReasonCode).Must(value => new[] { "partnerWithdrawal", "eligibilityFailure", "noResponse", "duplicateProperty", "commercialDecision", "other" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(command => command.Reason).NotEmpty().Length(10, 1000);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
    }
}
