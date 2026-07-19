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
