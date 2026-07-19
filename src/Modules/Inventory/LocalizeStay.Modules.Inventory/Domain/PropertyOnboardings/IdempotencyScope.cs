namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal enum IdempotencyScope
{
    SubmitToCuration,
    CurationReturn,
    DuplicateReview,
}
