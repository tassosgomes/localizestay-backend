using FluentValidation;

namespace LocalizeStay.Modules.Inventory.Application.CommercialOffers;

internal sealed class CreateCommercialPolicyCommandValidator : AbstractValidator<CreateCommercialPolicyCommand>
{
    public CreateCommercialPolicyCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.PolicyType).NotEmpty().Must(t => t is "flexible" or "nonRefundable")
            .WithMessage("Policy type must be 'flexible' or 'nonRefundable'.");
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class SetDefaultCommercialPolicyCommandValidator : AbstractValidator<SetDefaultCommercialPolicyCommand>
{
    public SetDefaultCommercialPolicyCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.PolicyId).NotEmpty();
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateCommercialPolicyCommandValidator : AbstractValidator<UpdateCommercialPolicyCommand>
{
    public UpdateCommercialPolicyCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.PolicyId).NotEmpty();
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class DeleteCommercialPolicyCommandValidator : AbstractValidator<DeleteCommercialPolicyCommand>
{
    public DeleteCommercialPolicyCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.PolicyId).NotEmpty();
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateAccommodationCommandValidator : AbstractValidator<CreateAccommodationCommand>
{
    public CreateAccommodationCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.CommercialName).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(c => c.MaxAdults).GreaterThanOrEqualTo(1).When(c => c.MaxAdults.HasValue);
        RuleFor(c => c.MaxChildren).GreaterThanOrEqualTo(0).When(c => c.MaxChildren.HasValue);
        RuleFor(c => c.TotalCapacity).GreaterThanOrEqualTo(1).When(c => c.TotalCapacity.HasValue);
        RuleFor(c => c.MealPlan).Must(m => m is null or "roomOnly" or "breakfast" or "halfBoard" or "fullBoard")
            .WithMessage("Meal plan must be one of: roomOnly, breakfast, halfBoard, fullBoard.");
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateAccommodationCommandValidator : AbstractValidator<UpdateAccommodationCommand>
{
    public UpdateAccommodationCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.AccommodationId).NotEmpty();
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class DeleteAccommodationCommandValidator : AbstractValidator<DeleteAccommodationCommand>
{
    public DeleteAccommodationCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.AccommodationId).NotEmpty();
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateCommercialRateCommandValidator : AbstractValidator<CreateCommercialRateCommand>
{
    public CreateCommercialRateCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.AccommodationId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(c => c.ConditionCode).NotEmpty().MaximumLength(60).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(c => c.BasePriceCents).GreaterThanOrEqualTo(0).When(c => c.BasePriceCents.HasValue);
        RuleFor(c => c.IncludedGuests).InclusiveBetween(1, 30).When(c => c.IncludedGuests.HasValue);
        RuleFor(c => c.AdditionalAdultPriceCents).GreaterThanOrEqualTo(0).When(c => c.AdditionalAdultPriceCents.HasValue);
        RuleFor(c => c.AdditionalChildPriceCents).GreaterThanOrEqualTo(0).When(c => c.AdditionalChildPriceCents.HasValue);
        RuleFor(c => c.MinimumNights).InclusiveBetween(1, 365).When(c => c.MinimumNights.HasValue);
        RuleFor(c => c.MealPlan).Must(m => m is null or "roomOnly" or "breakfast" or "halfBoard" or "fullBoard")
            .WithMessage("Meal plan must be one of: roomOnly, breakfast, halfBoard, fullBoard.");
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateCommercialRateCommandValidator : AbstractValidator<UpdateCommercialRateCommand>
{
    public UpdateCommercialRateCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.AccommodationId).NotEmpty();
        RuleFor(c => c.RateId).NotEmpty();
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class DeleteCommercialRateCommandValidator : AbstractValidator<DeleteCommercialRateCommand>
{
    public DeleteCommercialRateCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.AccommodationId).NotEmpty();
        RuleFor(c => c.RateId).NotEmpty();
        RuleFor(c => c.Actor).NotEmpty().MaximumLength(200);
    }
}

internal sealed class GetCommercialOfferQueryValidator : AbstractValidator<GetCommercialOfferQuery>
{
    public GetCommercialOfferQueryValidator()
    {
        RuleFor(q => q.PropertyId).NotEmpty();
    }
}
