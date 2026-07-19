namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed record BlockingReason(BlockingReasonCode Code, string Message, string? RelatedResourceId);
