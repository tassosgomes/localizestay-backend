using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Partners;

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
