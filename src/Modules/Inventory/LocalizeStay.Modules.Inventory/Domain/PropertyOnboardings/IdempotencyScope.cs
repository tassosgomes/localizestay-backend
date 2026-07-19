namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public enum IdempotencyScope
{
    SubmitToCuration,
    CurationReturn,
    DuplicateReview,
}
