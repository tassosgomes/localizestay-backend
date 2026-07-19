namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed record BlockingReason(BlockingReasonCode Code, string Message, string? RelatedResourceId);
